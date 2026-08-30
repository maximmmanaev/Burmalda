using System.Reflection;
using Burmalda.Altar;
using Burmalda.Artifacts;
using Burmalda.Bootstrap;
using Burmalda.Boss;
using Burmalda.Camp;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Decay;
using Burmalda.Movement;
using Burmalda.RunLifecycle;
using NUnit.Framework;
using UnityEngine;

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
    /// <b>Переработан задачей "композиционный корень RunBootstrap"</b>: до неё
    /// этот тест вручную конструировал каждую систему в порядке, скопированном
    /// из реальных Controller'ов (<c>CurrencyController.RebuildRunSystems</c>
    /// и т.д.) — и БЫЛ фактическим эталоном этого порядка (задача сама так
    /// его называет), но не проверял, что реальная игра вообще способна
    /// собрать эти системы через MonoBehaviour-компоненты — только то, что
    /// сами системы совместимы друг с другом. Теперь тест собирает GameObject
    /// ровно так, как это происходит в реальной сцене (<c>GridTraceInputController</c>
    /// + уже размещённые там <c>TrailDecayController</c>/<c>RunController</c>
    /// + <see cref="RunBootstrap"/>, который донабирает
    /// Currency/Altar/Boss/Camp/Lever), и дальше взаимодействует ТОЛЬКО через
    /// публичный API реальных Controller'ов — если сборка когда-нибудь
    /// сломается (неверный порядок AddComponent, отсутствующая зависимость),
    /// этот тест упадёт первым, а не только на реальном устройстве.
    ///
    /// Приватные <c>Awake</c>/<c>Update</c> вызываются рефлексией — вне Play
    /// Mode Unity не вызывает их сама даже после <c>AddComponent</c> (тот же
    /// принцип, что в <c>Movement.Tests.GridTraceInputControllerTapTests</c>/
    /// <c>Bootstrap.Tests.RunBootstrapTests</c>).
    ///
    /// Числа (требуемая энергия Босса, стоимость апгрейда Тотема) — НЕ
    /// реальные балансные константы (`Boss.BossController.BaseRequiredEnergy`
    /// и т.п., черновые/предмет Спринта 10) — тест проверяет ПРОВОДКУ систем
    /// друг к другу, а не баланс, поэтому использует заведомо большой запас
    /// Маны вместо точного попадания в порог.
    ///
    /// <b>Переработан задачей по экономике "Мана как доход забега, Монеты
    /// только в Лагере" (PRD v9)</b>: в забеге больше нет отдельного
    /// накопителя Монет (обычные плиты тоже начисляют Ману через
    /// <see cref="TrailManaIncomeSystem"/>) и нет постоянного кошелька
    /// Кристаллов — точные числа Маны/Ключей ПОСЛЕ обхода ловушки намеренно
    /// не проверяются равенством (зависят от множителя на каждом шаге,
    /// который сам по себе не предмет этого теста, к тому же теперь
    /// инкапсулирован внутри CurrencyController — не читается напрямую) —
    /// только тем, что они выросли.
    /// </summary>
    public class CoreLoopIntegrationTests
    {
        private const int Width = 5;

        private GameObject _inputObject;
        private GameObject _bootstrapObject;
        private GridTraceInputController _input;
        private RunBootstrap _bootstrap;

        [TearDown]
        public void TearDown()
        {
            if (_bootstrapObject != null) Object.DestroyImmediate(_bootstrapObject);
            if (_inputObject != null) Object.DestroyImmediate(_inputObject);
        }

        /// <summary>
        /// Собирает ровно ту топологию, что в реальной сцене: GridTraceInputController
        /// + TrailDecayController/RunController (уже размещены на сцене — см.
        /// doc-комментарий RunBootstrap) на одном GameObject, RunBootstrap —
        /// на отдельном (как самобутстрап в реальной игре), донабирающий
        /// Currency/Altar/Boss/Camp/Lever на GameObject GridTraceInputController.
        /// </summary>
        private void SetUp()
        {
            _inputObject = new GameObject("CoreLoop_Input");
            _input = _inputObject.AddComponent<GridTraceInputController>();
            InvokePrivate(_input, "Awake");

            var decay = _inputObject.AddComponent<TrailDecayController>();
            InvokePrivate(decay, "Awake");
            InvokePrivate(decay, "Update");

            var runController = _inputObject.AddComponent<RunController>();
            InvokePrivate(runController, "Awake");
            InvokePrivate(runController, "Update");

            _bootstrapObject = new GameObject("CoreLoop_Bootstrap");
            _bootstrap = _bootstrapObject.AddComponent<RunBootstrap>();
            InvokePrivate(_bootstrap, "Awake");

            // Как и в реальной игре: RunBootstrap.Update() добавляет
            // контроллеры через AddComponent, что вне Play Mode не вызывает
            // их Awake() само по себе — вызываем вручную (см. doc-комментарий
            // класса), затем ещё раз Update() бутстрапа, чтобы лоадаут
            // (EnsureLoadoutReady) увидел уже готовую Altar.Collection.
            InvokePrivate(_bootstrap, "Update");
            InvokePrivate(_bootstrap.Currency, "Awake");
            InvokePrivate(_bootstrap.Altar, "Awake");
            InvokePrivate(_bootstrap.Boss, "Awake");
            InvokePrivate(_bootstrap.Camp, "Awake");
            InvokePrivate(_bootstrap.Lever, "Awake");
            InvokePrivate(_bootstrap, "Update");

            // Материализуем run-системы каждого контроллера — в реальной
            // игре это происходит на первом кадре после RunStarted, здесь
            // вызываем Update() каждого явно, в том же порядке зависимостей.
            InvokePrivate(_bootstrap.Currency, "Update");
            InvokePrivate(_bootstrap.Altar, "Update");
            InvokePrivate(_bootstrap.Boss, "Update");
            InvokePrivate(_bootstrap.Camp, "Update");
        }

        [Test]
        public void FullCoreLoop_TunnelTrapsMultiplierAltarBossReturnCamp_AllSystemsIntegrate()
        {
            SetUp();
            // Симулирует "уже разблокировано ранее" — иначе PrimaryChest Ритуала пуст (issue #19).
            _bootstrap.Altar.Pool.Unlock(ArtifactCatalog.Amulets[0].Id);

            // --- Разметка плит: источники валют, ловушка, Алтарь, Босс ---
            var manaSource = new GridCoordinate(1, 2);
            var keySource = new GridCoordinate(2, 2);
            var trap = new GridCoordinate(3, 2); // ловушка на прямом пути — трейл должен её обойти
            var safeDetour = new GridCoordinate(3, 1);
            var altar = new GridCoordinate(4, 2);
            var boss = new GridCoordinate(5, 2);
            _input.Grid.GetOrCreateTile(manaSource).MarkManaSource();
            _input.Grid.GetOrCreateTile(keySource).MarkKeySource();
            _input.Grid.GetOrCreateTile(trap).MarkLethalTrap(LethalTrapType.Pit);
            _input.Grid.GetOrCreateTile(altar).MarkAltar();
            _input.Grid.GetOrCreateTile(boss).MarkBoss();

            Ritual ritualOpened = null;
            _bootstrap.Altar.RitualOpened += r => ritualOpened = r;
            BossEncounterOutcome bossOutcome = default;
            _bootstrap.Boss.EncounterResolved += (outcome, relic) => bossOutcome = outcome;

            var trail = _input.Trail;

            // === Тоннель → Ловушки (PRD 4.1/4.2) ===
            // Собираем валюты по пути к ловушке — сама trap=(3,2) на 3 ряда
            // впереди старта (0,2), не соседняя ему напрямую (IsAdjacentTo —
            // 8-направлено, максимум 1 клетка); шаг на неё нужно пробовать
            // с соседней клетки (keySource=(2,2)), иначе TryAdvanceTo даже
            // не доходит до проверки LethalTrap.
            Assert.IsTrue(trail.TryAdvanceTo(manaSource));
            Assert.IsTrue(trail.TryAdvanceTo(keySource));

            // Прямой путь на ловушку — ход НЕ засчитывается, событие поднимается, но не убивает тест.
            LethalTrapType? triggeredTrap = null;
            trail.LethalTrapTriggered += (coord, type) => triggeredTrap = type;
            var steppedOnTrap = trail.TryAdvanceTo(trap);
            Assert.IsFalse(steppedOnTrap, "Шаг на смертельную ловушку не должен засчитываться.");
            Assert.AreEqual(LethalTrapType.Pit, triggeredTrap);
            Assert.AreEqual(keySource, trail.CurrentPosition, "Позиция не должна была измениться после отказа шагнуть на ловушку.");

            // Обходим ловушку по диагонали.
            Assert.IsTrue(trail.TryAdvanceTo(safeDetour));

            // === Множитель (PRD 4.3) ===
            // v9: обычные плиты тоже дают Ману (TrailManaIncomeSystem внутри
            // CurrencyController) — точное число и сам множитель теперь
            // инкапсулированы внутри CurrencyController, не читаются
            // напрямую (не предмет этого теста).
            var runMana = _bootstrap.Currency.RunManaCrystals;
            var runKeys = _bootstrap.Currency.RunKeys;
            Assert.Greater(runMana.Total, 0, "Мана должна была накопиться — и с обычных плит, и с плитки-источника.");
            Assert.AreEqual(1, runKeys.Total, "Ключ должен был собраться с плитки-источника.");

            // Топ-ап валют — тест проверяет проводку систем, а не баланс.
            // Мана — с явным запасом СВЕРХ реального BossController.BaseRequiredEnergy
            // (5000, тир 0): в отличие от старой версии этого теста, здесь
            // Босс разрешается через настоящий BossController, а не через
            // BossEncounterSystem с подставным маленьким порогом — своя
            // балансная константа контроллера, не выдуманное число теста.
            runKeys.Add(1000);
            runMana.Add(BossController.BaseRequiredEnergy + 1000);

            // === Алтарь / Ритуал (PRD раздел 7) ===
            Assert.IsTrue(trail.TryAdvanceTo(altar));
            Assert.IsNotNull(ritualOpened, "Ритуал должен был открыться при достижении Алтаря с Ключами на руках.");
            var keysBeforePurchase = runKeys.Total;
            var purchased = ritualOpened.TryPurchasePrimary(runKeys, _bootstrap.Loadout);
            Assert.IsTrue(purchased);
            Assert.Less(runKeys.Total, keysBeforePurchase, "Покупка должна была потратить Ключи.");
            Assert.AreEqual(1, _bootstrap.Loadout.Acquired.Count);
            Assert.IsTrue(_bootstrap.Altar.Collection.Contains(_bootstrap.Loadout.Acquired[0].Id), "Покупка должна была зафиксировать артефакт в постоянной Коллекции (issue #18).");

            // === Босс ("Зал Реликвии" v4/v5 → Босс v6+, PRD раздел 8) ===
            Assert.IsTrue(trail.TryAdvanceTo(boss));
            Assert.IsTrue(bossOutcome.IsVictory, "С 1000+ Маны и требуемой энергией 50 Босс должен быть побеждён.");
            Assert.AreEqual(1, _bootstrap.DepthTier.CurrentTier, "Победа над Боссом должна продвинуть Ярус Глубины забега — тот самый, что читает RunBootstrap.");
            Assert.AreEqual(1, _bootstrap.Boss.DepthRecord.BestTier, "Постоянный рекорд глубины должен обновиться вслед за Ярусом.");
            Assert.IsTrue(_bootstrap.Boss.Tracker.HasWonBefore);
            Assert.IsTrue(_bootstrap.Altar.Pool.IsUnlocked(ArtifactCatalog.Talismans[0].Id), "Первая победа над Боссом должна разблокировать Талисманы/Амулеты каталога в общем Пуле.");

            // === Возврат и кэш-аут (PRD раздел 10) ===
            var journey = _bootstrap.Camp.Journey;
            journey.BeginReturn();
            ReturnConversionResult? returnResult = null;
            journey.Returned += r => returnResult = r;
            // Путь назад — тем же безопасным маршрутом, в обход ловушки.
            Assert.IsTrue(trail.TryAdvanceTo(altar));
            Assert.IsTrue(trail.TryAdvanceTo(safeDetour));
            Assert.IsTrue(trail.TryAdvanceTo(keySource));
            Assert.IsTrue(trail.TryAdvanceTo(manaSource));
            Assert.IsTrue(trail.TryAdvanceTo(new GridCoordinate(0, 2)));

            Assert.IsNotNull(returnResult, "Достижение стартового ряда во время возврата должно было сконвертировать Ману/Ключи в Монеты.");
            Assert.AreEqual(returnResult.Value.TotalCoins, _bootstrap.Currency.Coins.Balance);
            Assert.Greater(_bootstrap.Currency.Coins.Balance, 0, "Конвертация Маны/Ключей должна была дать хотя бы 1 Монету при таких запасах.");

            // === Лагерь (PRD раздел 11) ===
            var camp = _bootstrap.Camp.Camp;
            var totem = new Totem("totem-test", "Тестовый Тотем", TotemAbilityType.Breach);
            var coinsBeforeUpgrade = _bootstrap.Currency.Coins.Balance;
            var upgraded = camp.TryUpgradeTotem(totem, cost: 1);
            Assert.IsTrue(upgraded, "В Лагере должно было хватить Монет, зафиксированных при возврате, на минимальный апгрейд.");
            Assert.AreEqual(1, totem.Level);
            Assert.AreEqual(coinsBeforeUpgrade - 1, _bootstrap.Currency.Coins.Balance);
        }

        [Test]
        public void CoreLoop_AltarWithoutKeys_PassesThroughWithoutOpeningRitual()
        {
            // "Если у игрока нет Ключей — проходит мимо без потерь" (issue #19).
            SetUp();
            var altar = new GridCoordinate(1, 2);
            _input.Grid.GetOrCreateTile(altar).MarkAltar();

            var opened = false;
            _bootstrap.Altar.RitualOpened += _ => opened = true;

            var advanced = _input.Trail.TryAdvanceTo(altar);

            Assert.IsTrue(advanced, "Плита-Алтарь сама по себе не препятствие — ход должен засчитаться.");
            Assert.IsFalse(opened);
        }

        [Test]
        public void CoreLoop_BossEncounterWithInsufficientMana_ReportsDefeatAndDoesNotAdvanceDepth()
        {
            SetUp();
            var boss = new GridCoordinate(1, 2);
            _input.Grid.GetOrCreateTile(boss).MarkBoss(); // 0 Маны на руках — заведомо недостаточно

            string defeatReason = null;
            _bootstrap.Boss.EncounterResolved += (outcome, _) =>
            {
                if (!outcome.IsVictory) defeatReason = $"Не хватило энергии до Босса ({outcome.AccumulatedMana}/{outcome.RequiredEnergy})";
            };

            _input.Trail.TryAdvanceTo(boss);

            Assert.IsNotNull(defeatReason, "Поражение от Босса должно было сообщиться через EncounterResolved.");
            Assert.AreEqual(0, _bootstrap.DepthTier.CurrentTier, "Поражение не должно продвигать Ярус Глубины.");
            Assert.IsFalse(_bootstrap.Boss.Tracker.HasWonBefore);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} не найден рефлексией — сигнатура/имя изменились?");
            method.Invoke(target, args);
        }
    }
}
