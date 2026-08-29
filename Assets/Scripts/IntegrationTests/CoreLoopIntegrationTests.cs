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
    ///
    /// <b>Переработан задачей по экономике "Мана как доход забега, Монеты
    /// только в Лагере" (PRD v9)</b>: в забеге больше нет отдельного
    /// накопителя Монет (обычные плиты тоже начисляют Ману через
    /// <see cref="TrailManaIncomeSystem"/>) и нет постоянного кошелька
    /// Кристаллов — точные числа Маны/Ключей ПОСЛЕ обхода ловушки намеренно
    /// не проверяются равенством (зависят от множителя на каждом шаге,
    /// который сам по себе не предмет этого теста) — только тем, что они
    /// выросли.
    /// </summary>
    public class CoreLoopIntegrationTests
    {
        private const int Width = 5;

        [Test]
        public void FullCoreLoop_TunnelTrapsMultiplierAltarBossReturnCamp_AllSystemsIntegrate()
        {
            // --- Персистентные объекты (переживают "забег") ---
            var coinsWallet = new PersistentWallet();
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
            var runMana = new RunCurrencyAccumulator();
            var runKeys = new RunCurrencyAccumulator();
            using var multiplier = new TrailMultiplierSystem(trail);
            using var manaIncomeSystem = new TrailManaIncomeSystem(trail, multiplier, runMana); // обычные плиты — база Маны (v9, было Монеты)
            using var manaSystem = new TrailTileCurrencySystem(grid, trail, t => t.IsManaSource, 1, runMana);
            using var keySystem = new TrailTileCurrencySystem(grid, trail, t => t.IsKeySource, 1, runKeys);
            using var decay = new TrailDecaySystem(grid, trail);
            using var altarTrigger = new AltarTriggerSystem(grid, trail, runKeys, pool, () => 0f);
            var depthTier = new RunDepthTier();
            depthTier.Advanced += depthRecord.ReportTier;
            using var bossEncounter = new BossEncounterSystem(grid, trail, runMana, pool, tracker, depthTier,
                _ => Assert.Fail("Босс не должен был поражать игрока в этом сценарии — недостаточно Маны."), _ => 50);
            using var cashOut = new CashOutSystem(grid, trail, runMana, runKeys);
            using var journey = new ReturnJourneySystem(trail, runMana, runKeys, coinsWallet);
            var loadout = new RunArtifactLoadout(collection);

            Ritual ritualOpened = null;
            altarTrigger.RitualOpened += r => ritualOpened = r;
            BossEncounterOutcome bossOutcome = default;
            bossEncounter.EncounterResolved += (outcome, relic) => bossOutcome = outcome;

            // === Тоннель → Ловушки (PRD 4.1/4.2) ===
            // Собираем валюты по пути к ловушке — сама trap=(3,2) на 3 ряда
            // впереди старта (0,2), не соседняя ему напрямую (IsAdjacentTo —
            // 8-направлено, максимум 1 клетка); шаг на неё нужно пробовать
            // с соседней клетки (keySource=(2,2)), иначе TryAdvanceTo даже
            // не доходит до проверки LethalTrap (реальный баг теста, не
            // гипотеза — найдено 2026-08-14: LethalTrapTriggered не поднимался
            // вовсе, "Expected: Pit, But was: null", потому что и
            // TryAdvanceTo/CanAdvanceTo короткое замыкание на adjacency
            // раньше, чем успевали проверить сам трап).
            Assert.IsTrue(trail.TryAdvanceTo(manaSource));
            Assert.IsTrue(trail.TryAdvanceTo(keySource));

            // Прямой путь на ловушку — ход НЕ засчитывается, событие поднимается, но не убивает тест (см. reportBossDefeat выше — сюда не относится).
            LethalTrapType? triggeredTrap = null;
            trail.LethalTrapTriggered += (coord, type) => triggeredTrap = type;
            var steppedOnTrap = trail.TryAdvanceTo(trap);
            Assert.IsFalse(steppedOnTrap, "Шаг на смертельную ловушку не должен засчитываться.");
            Assert.AreEqual(LethalTrapType.Pit, triggeredTrap);
            Assert.AreEqual(keySource, trail.CurrentPosition, "Позиция не должна была измениться после отказа шагнуть на ловушку.");

            // Обходим ловушку по диагонали.
            Assert.IsTrue(trail.TryAdvanceTo(safeDetour));

            // === Множитель (PRD 4.3) ===
            Assert.Greater(multiplier.CurrentMultiplier, 0, "Множитель должен расти по мере прохождения уникальных плит.");
            // v9: обычные плиты тоже дают Ману (TrailManaIncomeSystem) — точное число зависит от множителя на каждом шаге, не предмет этого теста.
            Assert.Greater(runMana.Total, 0, "Мана должна была накопиться — и с обычных плит, и с плитки-источника.");
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

            // === Босс ("Зал Реликвии" v4/v5 → Босс v6+, PRD раздел 8) ===
            Assert.IsTrue(trail.TryAdvanceTo(boss));
            Assert.IsTrue(bossOutcome.IsVictory, "С 1000+ Маны и требуемой энергией 50 Босс должен быть побеждён.");
            Assert.AreEqual(1, depthTier.CurrentTier, "Победа над Боссом должна продвинуть Ярус Глубины забега.");
            Assert.AreEqual(1, depthRecord.BestTier, "Постоянный рекорд глубины должен обновиться вслед за Ярусом.");
            Assert.IsTrue(tracker.HasWonBefore);
            Assert.IsTrue(pool.IsUnlocked(ArtifactCatalog.Talismans[0].Id), "Первая победа над Боссом должна разблокировать Талисманы/Амулеты каталога в общем Пуле.");
            // v9: Перелив энергии Босса в Монеты удалён — Босс больше не трогает никакую валюту напрямую.

            // === Возврат и кэш-аут (PRD раздел 10) ===
            journey.BeginReturn();
            ReturnConversionResult? returnResult = null;
            journey.Returned += r => returnResult = r;
            // Путь назад — тем же безопасным маршрутом, в обход ловушки.
            Assert.IsTrue(trail.TryAdvanceTo(altar));
            Assert.IsTrue(trail.TryAdvanceTo(safeDetour));
            Assert.IsTrue(trail.TryAdvanceTo(keySource));
            Assert.IsTrue(trail.TryAdvanceTo(manaSource));
            Assert.IsTrue(trail.TryAdvanceTo(start));

            Assert.IsNotNull(returnResult, "Достижение стартового ряда во время возврата должно было сконвертировать Ману/Ключи в Монеты.");
            Assert.AreEqual(returnResult.Value.TotalCoins, coinsWallet.Balance);
            Assert.Greater(coinsWallet.Balance, 0, "Конвертация Маны/Ключей должна была дать хотя бы 1 Монету при таких запасах.");

            // === Лагерь (PRD раздел 11) ===
            // Полное имя обязательно: простое "Camp" в этом контексте
            // резолвится компилятором в СОСЕДНЕЕ пространство имён
            // Burmalda.Camp (сиблинг Burmalda.IntegrationTests под общим
            // Burmalda), а не в импортированный `using Burmalda.Camp;`
            // класс — вылезло только на реальной компиляции (CS0118).
            var camp = new Burmalda.Camp.Camp(coinsWallet, pool);
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
            var pool = new ArtifactPool();
            var tracker = new FirstBossVictoryTracker();
            var depthTier = new RunDepthTier();

            string defeatReason = null;
            using var bossEncounter = new BossEncounterSystem(grid, trail, runMana, pool, tracker, depthTier,
                reason => defeatReason = reason, _ => 50);

            trail.TryAdvanceTo(boss);

            Assert.IsNotNull(defeatReason, "Поражение от Босса должно было сообщиться через колбэк.");
            Assert.AreEqual(0, depthTier.CurrentTier, "Поражение не должно продвигать Ярус Глубины.");
            Assert.IsFalse(tracker.HasWonBefore);
        }
    }
}
