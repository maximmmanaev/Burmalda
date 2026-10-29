using System.Reflection;
using Burmalda.Altar;
using Burmalda.Artifacts;
using Burmalda.Bootstrap;
using Burmalda.Camp;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Decay;
using Burmalda.Movement;
using Burmalda.Sanctuary;
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
    /// проверяется здесь как Святилище (<c>Sanctuary.SanctuaryRunSystem</c>).
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
    /// Currency/Altar/Sanctuary/Camp/Lever), и дальше взаимодействует ТОЛЬКО через
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
    /// реальные балансные константы (`Sanctuary.Sanctuary.DefaultCollapseRowsPerSecond`
    /// и т.п., черновые/предмет Спринта 10) — тест проверяет ПРОВОДКУ систем
    /// друг к другу, а не баланс, поэтому использует заведомо большой запас
    /// Маны вместо точного попадания в порог.
    ///
    /// <b>Переработан задачей по экономике "Мана как доход забега, Монеты
    /// только в Лагере" (PRD v9)</b>: в забеге больше нет отдельного
    /// накопителя Монет (обычные плиты тоже начисляют Ману через
    /// <see cref="TrailManaIncomeSystem"/>) и нет постоянного кошелька
    /// Кристаллов — точные числа Маны ПОСЛЕ обхода ловушки намеренно
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
            Burmalda.Core.RunEffects.Current = Burmalda.Core.RunEffects.Neutral; // Алтарь подставляет эффекты билда — не оставляем их другим тестам
        }

        /// <summary>
        /// Собирает ровно ту топологию, что в реальной сцене: GridTraceInputController
        /// + TrailDecayController/RunController (уже размещены на сцене — см.
        /// doc-комментарий RunBootstrap) на одном GameObject, RunBootstrap —
        /// на отдельном (как самобутстрап в реальной игре), донабирающий
        /// Currency/Altar/Sanctuary/Camp/Lever на GameObject GridTraceInputController.
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
            // issue #225: без этого переопределения бросок d20 при попытке
            // шагнуть на смертельную ловушку ниже (LethalTrapType.ArrowWave,
            // issue #258 — не Lava, та с этой задачи физически проходима) идёт
            // на процесс-wide UnityEngine.Random.Range — исход (Fortune/
            // Knockback/Death) зависит от того, сколько раз этот RNG успели
            // вызвать ДРУГИЕ тесты, отработавшие раньше в том же прогоне
            // (порядок NUnit не гарантирован). Knockback телепортирует трейл
            // на "последний пройденный Алтарь" (Алтарей ещё не было — откат
            // на СТАРТ, RunState.ResolveHazard), что и давало наблюдаемый
            // флаки-фейл: CurrentPosition оказывалась (0,2) вместо keySource.
            // Фиксируем исход на Fortune (ничего не меняется, ровно то, что
            // тест и так предполагал) — тот же приём внедрённого источника
            // случайности, что уже применяется для TunnelObstacleGenerator/
            // AltarTriggerSystem, не блиндовый глобальный сид (см. issue).
            runController.RollDieOverride = () => 6; // 6+6 — «на месте»
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
            InvokePrivate(_bootstrap.Segments, "Awake");
            InvokePrivate(_bootstrap.Currency, "Awake");
            InvokePrivate(_bootstrap.Altar, "Awake");
            InvokePrivate(_bootstrap.Sanctuary, "Awake");
            InvokePrivate(_bootstrap.Camp, "Awake");
            InvokePrivate(_bootstrap.Lever, "Awake");
            InvokePrivate(_bootstrap, "Update");

            // Материализуем run-системы каждого контроллера — в реальной
            // игре это происходит на первом кадре после RunStarted, здесь
            // вызываем Update() каждого явно, в том же порядке зависимостей.
            InvokePrivate(_bootstrap.Currency, "Update");
            InvokePrivate(_bootstrap.Altar, "Update");
            InvokePrivate(_bootstrap.Sanctuary, "Update");
            InvokePrivate(_bootstrap.Camp, "Update");
        }

        [Test]
        public void FullCoreLoop_TunnelTrapsMultiplierAltarSanctuaryEvacuationCamp_AllSystemsIntegrate()
        {
            SetUp();
            // --- Разметка плит: источник Маны, ловушка, Алтарь, Босс ---
            var manaSource = new GridCoordinate(1, 2);
            var vault = new GridCoordinate(2, 2);
            // issue #258: статичная Лава физически проходима теперь (шаг не
            // отклоняется) — для сценария "ловушка блокирует прямой путь,
            // трейл должен её обойти" нужен один из четырёх рантайм-типов,
            // которые по-прежнему остаются жёсткой стеной; ArrowWave — тот
            // же представитель, что уже используется в GridTraceTrailTests.
            var trap = new GridCoordinate(3, 2); // ловушка на прямом пути — трейл должен её обойти
            var safeDetour = new GridCoordinate(3, 1);
            var altar = new GridCoordinate(4, 2);
            var sanctuaryEntry = new GridCoordinate(5, 2);
            var veinInSanctuary = new GridCoordinate(6, 2);
            var sarcophagusTile = new GridCoordinate(7, 2);
            _input.Grid.GetOrCreateTile(manaSource).MarkManaSource();
            _input.Grid.GetOrCreateTile(vault).MarkGateVault();
            _input.Grid.GetOrCreateTile(trap).TransitionToLethalTrap(LethalTrapType.ArrowWave);
            _input.Grid.GetOrCreateTile(altar).MarkAltar();
            _input.Grid.GetOrCreateTile(sanctuaryEntry).MarkSanctuaryEntry();
            _input.Grid.GetOrCreateTile(veinInSanctuary).MarkSanctuaryTile(SanctuaryTileKind.Vein);
            _input.Grid.GetOrCreateTile(sarcophagusTile).MarkSarcophagus();

            var altarReached = false;
            _bootstrap.Altar.AltarReached += _ => altarReached = true;

            var trail = _input.Trail;

            // === Тоннель → Ловушки (PRD 4.1/4.2) ===
            // Собираем валюты по пути к ловушке — сама trap=(3,2) на 3 ряда
            // впереди старта (0,2), не соседняя ему напрямую (IsAdjacentTo —
            // 8-направлено, максимум 1 клетка); шаг на неё нужно пробовать
            // с соседней клетки (тайник=(2,2)), иначе TryAdvanceTo даже
            // не доходит до проверки LethalTrap.
            Assert.IsTrue(trail.TryAdvanceTo(manaSource));
            Assert.IsTrue(trail.TryAdvanceTo(vault));

            // Прямой путь на ловушку — ход НЕ засчитывается, событие поднимается, но не убивает тест.
            LethalTrapType? triggeredTrap = null;
            trail.LethalTrapTriggered += (coord, type) => triggeredTrap = type;
            var steppedOnTrap = trail.TryAdvanceTo(trap);
            Assert.IsFalse(steppedOnTrap, "Шаг на смертельную ловушку не должен засчитываться.");
            Assert.AreEqual(LethalTrapType.ArrowWave, triggeredTrap);
            Assert.AreEqual(vault, trail.CurrentPosition, "Позиция не должна была измениться после отказа шагнуть на ловушку.");
            _inputObject.GetComponent<RunController>().RunState.RollFate(); // Бросок Судьбы 6+6 — продолжаем с той же плиты

            // Обходим ловушку по диагонали.
            Assert.IsTrue(trail.TryAdvanceTo(safeDetour));

            // === Множитель (PRD 4.3) ===
            // v9: обычные плиты тоже дают Ману (TrailManaIncomeSystem внутри
            // CurrencyController) — точное число и сам множитель теперь
            // инкапсулированы внутри CurrencyController, не читаются
            // напрямую (не предмет этого теста).
            var runMana = _bootstrap.Currency.RunManaCrystals;
            Assert.Greater(runMana.Total, 0, "Мана должна была накопиться — и с обычных плит, и с плитки-источника.");

            // === Алтарь (PRD v10 §13) ===
            Assert.IsTrue(trail.TryAdvanceTo(altar));
            Assert.IsTrue(altarReached, "Достижение Алтаря должно было поднять AltarReached — без каких-либо валют на руках.");

            // Тайник за Воротами подобран по пути (плита vault) — на этом Алтаре выбор 1 из 3, не 1 из 2 (PRD v10 §10, §13.2).
            var session = _bootstrap.Altar.ActiveSession;
            Assert.IsNotNull(session, "Алтарь должен был открыть выбор Реликвии.");
            Assert.AreEqual(3, session.Options.Count, "Тайник даёт третий вариант.");
            Assert.AreEqual(0, _bootstrap.Altar.PendingVaultOptions, "Бонус тайника потрачен на этом Алтаре.");
            Assert.IsTrue(session.Pick(session.Options[0].Id));
            Assert.AreEqual(1, _bootstrap.Altar.Build.Items.Count, "Взята ровно одна Реликвия.");
            Assert.IsNull(_bootstrap.Altar.ActiveSession);

            // === Святилище (PRD v10 §14): вход, Жила, выход к Алтарю в конце ===
            var manaBeforeSanctuary = runMana.Total;
            Assert.IsTrue(trail.TryAdvanceTo(sanctuaryEntry));
            Assert.IsNotNull(_bootstrap.Sanctuary.ActiveSanctuary, "Вход на плиту Святилища должен был запустить Святилище.");
            Assert.IsTrue(trail.TryAdvanceTo(veinInSanctuary));
            var exitRow = _bootstrap.Sanctuary.ActiveSanctuary.ExitRow;
            var exitAltar = new GridCoordinate(exitRow, 2);
            _input.Grid.GetOrCreateTile(exitAltar).MarkAltar();
            Assert.IsTrue(trail.TryAdvanceTo(sarcophagusTile));
            Assert.AreEqual(1, _bootstrap.Camp.Carried.Count, "Взятый Саркофаг должен стать несомым (PRD v10 §15.1).");
            for (var row = sarcophagusTile.Row + 1; row <= exitRow; row++) Assert.IsTrue(trail.TryAdvanceTo(new GridCoordinate(row, 2)));
            Assert.Greater(runMana.Total, manaBeforeSanctuary, "На выходе Мана Святилища должна была зачислиться.");
            Assert.AreEqual(1, _bootstrap.DepthTier.CurrentTier, "Прохождение Святилища должно продвинуть Ярус Глубины забега — тот самый, что читает RunBootstrap.");
            Assert.AreEqual(1, _bootstrap.Sanctuary.DepthRecord.BestTier, "Постоянный рекорд глубины должен обновиться вслед за Ярусом.");

            // === Эвакуация (PRD v10 §15.2): мгновенно, с Алтаря в конце Святилища ===
            var evacuation = _bootstrap.Camp.Evacuation;
            ReturnConversionResult? returnResult = null;
            evacuation.Returned += r => returnResult = r;
            Assert.IsTrue(evacuation.Evacuate());
            Assert.AreEqual(0, _bootstrap.Camp.Carried.Count, "Эвакуация уносит Саркофаги из забега…");
            Assert.AreEqual(1, _bootstrap.Camp.Camp.UnopenedSarcophagi.Count, "…в Лагерь, где они ждут открытия (§22.3).");

            Assert.IsNotNull(returnResult, "Эвакуация с Алтаря должна была сконвертировать Ману в Монеты.");
            Assert.AreEqual(returnResult.Value.TotalCoins, _bootstrap.Currency.Coins.Balance);
            Assert.Greater(_bootstrap.Currency.Coins.Balance, 0, "Конвертация Маны должна была дать хотя бы 1 Монету при таких запасах.");

            // Прокачка Идола/Тотема в Лагере — отдельные тесты (спринт v10-5).
        }

        [Test]
        public void CoreLoop_AltarIsPlainTile_StepIsCountedAndAltarReachedRaised()
        {
            SetUp();
            var altar = new GridCoordinate(1, 2);
            _input.Grid.GetOrCreateTile(altar).MarkAltar();

            var reached = false;
            _bootstrap.Altar.AltarReached += _ => reached = true;

            var advanced = _input.Trail.TryAdvanceTo(altar);

            Assert.IsTrue(advanced, "Плита-Алтарь сама по себе не препятствие — ход должен засчитаться.");
            Assert.IsTrue(reached);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} не найден рефлексией — сигнатура/имя изменились?");
            method.Invoke(target, args);
        }
    }
}
