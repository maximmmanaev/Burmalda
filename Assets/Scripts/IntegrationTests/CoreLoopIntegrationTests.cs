using Burmalda.Altar;
using Burmalda.Artifacts;
using Burmalda.Boss;
using Burmalda.Camp;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Decay;
using Burmalda.Movement;
using Burmalda.Progression;
using NUnit.Framework;

namespace Burmalda.IntegrationTests
{
    /// <summary>
    /// E2e-интеграция core loop (PRD раздел 4, issue #29): "Проверить полную
    /// связку систем: тоннель → ловушки → множитель → Алтарь/Ритуал → Зал
    /// Реликвии → возврат/кэш-аут → Лагерь." "Зал Реликвии" — устаревший
    /// v4/v5-термин; с v6 эта стадия core loop — встреча с Боссом (см.
    /// docs/wiki/roadmap.md, «продолжение» о замене Зала Реликвии Боссом),
    /// проверяется здесь как <see cref="BossEncounterSystem"/>.
    ///
    /// В проекте нет ни настоящего Unity PlayMode (эта сессия работает без
    /// живого Unity Editor, см. changelog Спринта 9), ни единого
    /// MonoBehaviour, который бы собирал ВСЕ системы разом (Currency/Altar/
    /// Boss/Camp — отдельные Controller'ы, каждый ссылается на соседние
    /// через <c>GetComponent</c>). Эти тесты воспроизводят ТОЧНО ТУ ЖЕ
    /// последовательность конструирования, что и реальные Controller'ы
    /// (см. <c>Currencies.CurrencyController.RebuildRunSystems</c>,
    /// <c>Boss.BossController.RebuildEncounter</c>,
    /// <c>Camp.CampController.RebuildRunSystems</c>), но напрямую —
    /// EditMode-тест уровня "весь core loop одним прогоном", а не Play Mode.
    ///
    /// Числа (требуемая энергия Босса, стоимость апгрейда Тотема) — НЕ
    /// реальные балансные константы (`Boss.BossController.BaseRequiredEnergy`
    /// и т.п., черновые/предмет Спринта 10) — тест проверяет ПРОВОДКУ систем
    /// друг к другу, а не баланс, поэтому использует маленькие
    /// детерминированные значения, устойчивые к будущей балансировке.
    /// </summary>
    public class CoreLoopIntegrationTests
    {
        private const int Width = 5;

        [Test]
        public void FullCoreLoop_TunnelTrapsMultiplierAltarBossReturnCamp_AllSystemsIntegrate()
        {
            // --- Персистентные объекты (переживают "забег") ---
            var coinsWallet = new PersistentWallet();
            var crystalsWallet = new PersistentWallet();
            var collection = new ArtifactCollection();
            var pool = new ArtifactPool();
            var depthRecord = new DepthRecord();
            var tracker = new FirstBossVictoryTracker();
            // Симулирует "уже разблокировано ранее" — иначе PrimaryChest Ритуала пуст (issue #19).
            pool.Unlock(ArtifactCatalog.Amulets[0].Id);

            // --- Тоннель и трейл (PRD 4.1) ---
            var grid = new TunnelGrid(Width);
            var start = new GridCoordinate(0, 2);
            var trail = new GridTraceTrail(grid, start);

            // --- Разметка плит: источники валют, ловушка, Алтарь, Босс ---
            var manaSource = new GridCoordinate(1, 2);
            var keySource = new GridCoordinate(2, 2);
            var trap = new GridCoordinate(3, 2); // ловушка на прямом пути — трейл должен её обойти
            var safeDetour = new GridCoordinate(3, 1);
            var altar = new GridCoordinate(4, 2);
            var boss = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(manaSource).MarkManaSource();
            grid.GetOrCreateTile(keySource).MarkKeySource();
            grid.GetOrCreateTile(trap).MarkLethalTrap(LethalTrapType.Pit);
            grid.GetOrCreateTile(altar).MarkAltar();
            grid.GetOrCreateTile(boss).MarkBoss();

            // --- Забеговые системы (порядок конструирования — как в CurrencyController.RebuildRunSystems) ---
            var runCoins = new RunCurrencyAccumulator();
            var runMana = new RunCurrencyAccumulator();
            var runKeys = new RunCurrencyAccumulator();
            using var multiplier = new TrailMultiplierSystem(trail);
            using var coinSystem = new TrailCoinSystem(trail, multiplier, runCoins);
            using var manaSystem = new TrailTileCurrencySystem(grid, trail, t => t.IsManaSource, 1, runMana);
            using var keySystem = new TrailTileCurrencySystem(grid, trail, t => t.IsKeySource, 1, runKeys);
            using var decay = new TrailDecaySystem(grid, trail);
            using var altarTrigger = new AltarTriggerSystem(grid, trail, runKeys, pool, () => 0f);
            var depthTier = new RunDepthTier();
            depthTier.Advanced += depthRecord.ReportTier;
            using var bossEncounter = new BossEncounterSystem(grid, trail, runMana, runCoins, pool, tracker, depthTier,
                _ => Assert.Fail("Босс не должен был поражать игрока в этом сценарии — недостаточно Маны."), _ => 50);
            using var cashOut = new CashOutSystem(grid, trail, runCoins, runMana, runKeys);
            using var journey = new ReturnJourneySystem(trail, runCoins, runMana, runKeys, coinsWallet);
            var loadout = new RunArtifactLoadout(collection);

            Ritual ritualOpened = null;
            altarTrigger.RitualOpened += r => ritualOpened = r;
            BossEncounterOutcome bossOutcome = default;
            bossEncounter.EncounterResolved += (outcome, relic) => bossOutcome = outcome;

            // === Тоннель → Ловушки (PRD 4.1/4.2) ===
            // Прямой путь на ловушку — ход НЕ засчитывается, событие поднимается, но не убивает тест (см. reportBossDefeat выше — сюда не относится).
            LethalTrapType? triggeredTrap = null;
            trail.LethalTrapTriggered += (coord, type) => triggeredTrap = type;
            var steppedOnTrap = trail.TryAdvanceTo(trap);
            Assert.IsFalse(steppedOnTrap, "Шаг на смертельную ловушку не должен засчитываться.");
            Assert.AreEqual(LethalTrapType.Pit, triggeredTrap);
            Assert.AreEqual(start, trail.CurrentPosition, "Позиция не должна была измениться после отказа шагнуть на ловушку.");

            // Обходим ловушку по диагонали.
            Assert.IsTrue(trail.TryAdvanceTo(manaSource));
            Assert.IsTrue(trail.TryAdvanceTo(keySource));
            Assert.IsTrue(trail.TryAdvanceTo(safeDetour));

            // === Множитель (PRD 4.3) ===
            Assert.Greater(multiplier.CurrentMultiplier, 0, "Множитель должен расти по мере прохождения уникальных плит.");
            Assert.AreEqual(1, runMana.Total, "Кристалл Маны должен был собраться с плитки-источника.");
            Assert.AreEqual(1, runKeys.Total, "Ключ должен был собраться с плитки-источника.");

            // Топ-ап валют — тест проверяет проводку систем, а не баланс: обеспечиваем гарантированную платёжеспособность на Алтаре/Боссе независимо от черновых чисел Спринта 10.
            runKeys.Add(1000);
            runMana.Add(1000);

            // === Алтарь / Ритуал (PRD раздел 7) ===
            Assert.IsTrue(trail.TryAdvanceTo(altar));
            Assert.IsNotNull(ritualOpened, "Ритуал должен был открыться при достижении Алтаря с Ключами на руках.");
            var keysBeforePurchase = runKeys.Total;
            var purchased = ritualOpened.TryPurchasePrimary(runKeys, loadout);
            Assert.IsTrue(purchased);
            Assert.Less(runKeys.Total, keysBeforePurchase, "Покупка должна была потратить Ключи.");
            Assert.AreEqual(1, loadout.Acquired.Count);
            Assert.IsTrue(collection.Contains(loadout.Acquired[0].Id), "Покупка должна была зафиксировать артефакт в постоянной Коллекции (issue #18).");

            // CashOutSystem фиксирует чекпоинт безусловно при достижении Алтаря (issue #25).
            var coinsAtCheckpoint = runCoins.Total;

            // === Босс ("Зал Реликвии" v4/v5 → Босс v6+, PRD раздел 8) ===
            Assert.IsTrue(trail.TryAdvanceTo(boss));
            Assert.IsTrue(bossOutcome.IsVictory, "С 1000 Маны и требуемой энергией 50 Босс должен быть побеждён.");
            Assert.AreEqual(1, depthTier.CurrentTier, "Победа над Боссом должна продвинуть Ярус Глубины забега.");
            Assert.AreEqual(1, depthRecord.BestTier, "Постоянный рекорд глубины должен обновиться вслед за Ярусом.");
            Assert.IsTrue(tracker.HasWonBefore);
            Assert.IsTrue(pool.IsUnlocked(ArtifactCatalog.Talismans[0].Id), "Первая победа над Боссом должна разблокировать Талисманы/Амулеты каталога в общем Пуле.");
            Assert.Greater(runCoins.Total, coinsAtCheckpoint, "Перелив энергии Босса должен был добавить Монеты в накопитель забега.");

            // === Возврат и кэш-аут (PRD раздел 10) ===
            journey.BeginReturn();
            int? returnedCoins = null;
            journey.Returned += amount => returnedCoins = amount;
            // Путь назад — тем же безопасным маршрутом, в обход ловушки.
            Assert.IsTrue(trail.TryAdvanceTo(altar));
            Assert.IsTrue(trail.TryAdvanceTo(safeDetour));
            Assert.IsTrue(trail.TryAdvanceTo(keySource));
            Assert.IsTrue(trail.TryAdvanceTo(manaSource));
            Assert.IsTrue(trail.TryAdvanceTo(start));

            Assert.IsNotNull(returnedCoins, "Достижение стартового ряда во время возврата должно было зафиксировать Монеты.");
            Assert.AreEqual(returnedCoins.Value, coinsWallet.Balance);
            Assert.AreEqual(runCoins.Total, coinsWallet.Balance);

            // === Лагерь (PRD раздел 11) ===
            var camp = new Camp(coinsWallet, crystalsWallet, pool);
            var totem = new Totem("totem-test", "Тестовый Тотем", TotemAbilityType.Breach);
            var coinsBeforeUpgrade = coinsWallet.Balance;
            var upgraded = camp.TryUpgradeTotem(totem, cost: 1);
            Assert.IsTrue(upgraded, "В Лагере должно было хватить Монет, зафиксированных при возврате, на минимальный апгрейд.");
            Assert.AreEqual(1, totem.Level);
            Assert.AreEqual(coinsBeforeUpgrade - 1, coinsWallet.Balance);
        }

        [Test]
        public void CoreLoop_AltarWithoutKeys_PassesThroughWithoutOpeningRitual()
        {
            // "Если у игрока нет Ключей — проходит мимо без потерь" (issue #19).
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var altar = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(altar).MarkAltar();

            var runKeys = new RunCurrencyAccumulator(); // 0 Ключей
            var pool = new ArtifactPool();
            using var altarTrigger = new AltarTriggerSystem(grid, trail, runKeys, pool, () => 0f);
            var opened = false;
            altarTrigger.RitualOpened += _ => opened = true;

            var advanced = trail.TryAdvanceTo(altar);

            Assert.IsTrue(advanced, "Плита-Алтарь сама по себе не препятствие — ход должен засчитаться.");
            Assert.IsFalse(opened);
        }

        [Test]
        public void CoreLoop_BossEncounterWithInsufficientMana_ReportsDefeatAndDoesNotAdvanceDepth()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var boss = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(boss).MarkBoss();

            var runMana = new RunCurrencyAccumulator(); // 0 Маны — заведомо недостаточно
            var runCoins = new RunCurrencyAccumulator();
            var pool = new ArtifactPool();
            var tracker = new FirstBossVictoryTracker();
            var depthTier = new RunDepthTier();

            string defeatReason = null;
            using var bossEncounter = new BossEncounterSystem(grid, trail, runMana, runCoins, pool, tracker, depthTier,
                reason => defeatReason = reason, _ => 50);

            trail.TryAdvanceTo(boss);

            Assert.IsNotNull(defeatReason, "Поражение от Босса должно было сообщиться через колбэк.");
            Assert.AreEqual(0, depthTier.CurrentTier, "Поражение не должно продвигать Ярус Глубины.");
            Assert.IsFalse(tracker.HasWonBefore);
        }
    }
}
