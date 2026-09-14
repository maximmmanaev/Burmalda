using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    /// <summary>
    /// Баг с устройства (владелец, 2026-09-14, «ловушки вообще пропали»,
    /// issue #256, портирован сюда вместе с переходом всех пяти систем на
    /// реальное время — issue #254): <see cref="TrapSystemsController"/>
    /// подписывается на <see cref="GridTraceInputController.RunStarted"/> в
    /// своём <c>OnEnable()</c>, но в реальной игре
    /// <c>Bootstrap.RunBootstrap.EnsureControllersWired</c> добавляет этот
    /// Controller на сцену ПОСЛЕ того, как первый (и на практике —
    /// единственный на весь забег до ручного рестарта) <c>RunStarted</c> уже
    /// поднялся синхронно внутри <c>GridTraceInputController.Awake()</c> —
    /// см. доккомментарий <c>RunBootstrap.EnsureLoadoutReady</c>, тот же
    /// класс гонки, что уже был найден и решён для лоадаута артефактов, но
    /// не был решён здесь. Controller безвозвратно пропускает единственное
    /// событие, которое должно было его построить, и ни разу не строит ни
    /// одну из пяти систем ловушек за весь забег — в отличие от ВСЕХ
    /// остальных контроллеров забега (<c>Boss.BossController</c>/
    /// <c>Generation.SegmentGenerationController</c> и т.д.), у него не было
    /// ленивой самопроверки в <c>Update()</c>, подстраховывающей именно этот
    /// случай.
    ///
    /// Приватные методы вызываются рефлексией — тот же паттерн, что
    /// <c>Bootstrap.Tests.RunBootstrapTests</c>: вне Play Mode
    /// <c>GameObject.AddComponent&lt;T&gt;()</c> не вызывает Awake() сама.
    /// </summary>
    public class TrapSystemsControllerTests
    {
        private GameObject _hostObject;
        private GridTraceInputController _input;
        private TrapSystemsController _controller;

        [TearDown]
        public void TearDown()
        {
            if (_hostObject != null) Object.DestroyImmediate(_hostObject);
        }

        // Воспроизводит реальный порядок RunBootstrap.EnsureControllersWired:
        // GridTraceInputController.Awake() (поднимает самый первый RunStarted
        // синхронно, ещё БЕЗ единого подписчика) строго ПЕРЕД тем, как
        // TrapSystemsController вообще появляется на сцене.
        private void SetUpReproducingRealBootstrapRace()
        {
            _hostObject = new GameObject("TrapController_Host");
            _input = _hostObject.AddComponent<GridTraceInputController>();
            InvokePrivate(_input, "Awake"); // здесь и поднимается тот самый пропущенный RunStarted

            _controller = _hostObject.AddComponent<TrapSystemsController>();
            InvokePrivate(_controller, "Awake");
            InvokePrivate(_controller, "OnEnable"); // подписка происходит СТРОГО ПОСЛЕ уже пропущенного события
        }

        [Test]
        public void Update_CalledAfterMissedInitialRunStarted_SelfHealsAndBuildsAllFiveSystems()
        {
            SetUpReproducingRealBootstrapRace();

            Assert.IsNull(GetPrivateField(_controller, "_arrowWave"), "до самолечения система ещё не должна быть построена — так воспроизводится баг");

            InvokePrivate(_controller, "Update"); // тот самый следующий кадр реальной игры

            Assert.IsNotNull(GetPrivateField(_controller, "_arrowWave"), "Update() обязан был заметить пропущенный RunStarted и построить систему сам, как у BossController/SegmentGenerationController");
            Assert.IsNotNull(GetPrivateField(_controller, "_bomb"));
            Assert.IsNotNull(GetPrivateField(_controller, "_bladeTact"));
            Assert.IsNotNull(GetPrivateField(_controller, "_fallingRock"));
            Assert.IsNotNull(GetPrivateField(_controller, "_lavaWave"));
        }

        [Test]
        public void Update_AfterSelfHeal_TicksAllFiveSystemsWithRealDeltaTime_DoesNotThrow()
        {
            // Сквозной тест: недостаточно, чтобы поля просто были не-null —
            // Update() на следующем кадре обязан уже начать тикать их
            // реальным временем, не падая на несуществующих системах.
            SetUpReproducingRealBootstrapRace();
            InvokePrivate(_controller, "Update"); // самолечение — строит системы

            Assert.DoesNotThrow(() => InvokePrivate(_controller, "Update"), "второй кадр обязан тикать уже построенные системы, не падать");
        }

        [Test]
        public void Update_TriggerActuallyFiresOnRealGridAfterSelfHeal()
        {
            // Сквозной тест: недостаточно, чтобы поля просто были не-null —
            // нужно, чтобы система, построенная самолечением, реально видела
            // триггер на настоящей сетке забега и реально его отрабатывала,
            // тем же путём, что и в игре. Time.deltaTime не надёжен в
            // EditMode — тикаем построенную систему напрямую известным
            // значением, не через Update() Controller'а.
            SetUpReproducingRealBootstrapRace();
            InvokePrivate(_controller, "Update"); // самолечение — строит системы

            var trigger = new Core.GridCoordinate(1, _input.Grid.Width / 2);
            _input.Grid.GetOrCreateTile(trigger).MarkBombTrigger();
            Assert.IsTrue(_input.Trail.TryAdvanceTo(trigger), "шаг на триггер должен был пройти");

            var bomb = (BombTrapSystem)GetPrivateField(_controller, "_bomb");
            Assert.IsFalse(_input.Grid.GetOrCreateTile(trigger).LethalTrap.HasValue, "взрыв ещё не должен был произойти — задержка не истекла");

            bomb.Tick(BombTrapSystem.ComputeDelaySeconds(0)); // Controller не задаёт CurrentTierProvider в этом тесте — Ярус 0

            Assert.AreEqual(Core.LethalTrapType.BombBlast, _input.Grid.GetOrCreateTile(trigger).LethalTrap,
                "система, построенная самолечением Update(), обязана реально сработать на настоящей сетке забега");
        }

        // Issue #260: композиционный корень (RunBootstrap) присваивает
        // CurrentTierProvider ДО того, как Rebuild() успевает построить
        // BombTrapSystem (см. RunBootstrap.EnsureControllersWired) — этот
        // тест подтверждает, что значение реально доходит до
        // BombTrapSystem.ComputeDelaySeconds, а не молча игнорируется.
        [Test]
        public void CurrentTierProvider_SetBeforeRebuild_ShortensBombDelay()
        {
            SetUpReproducingRealBootstrapRace();
            _controller.CurrentTierProvider = () => 10;
            InvokePrivate(_controller, "Update"); // самолечение — строит системы с уже установленным провайдером

            var trigger = new Core.GridCoordinate(1, _input.Grid.Width / 2);
            _input.Grid.GetOrCreateTile(trigger).MarkBombTrigger();
            Assert.IsTrue(_input.Trail.TryAdvanceTo(trigger));

            var bomb = (BombTrapSystem)GetPrivateField(_controller, "_bomb");
            bomb.Tick(BombTrapSystem.ComputeDelaySeconds(10));

            Assert.IsTrue(_input.Grid.GetOrCreateTile(trigger).IsBombCollapsed,
                "на Ярусе 10 задержка обязана была истечь за ComputeDelaySeconds(10) секунд — если бы CurrentTierProvider не дошёл до BombTrapSystem, реальная задержка осталась бы ComputeDelaySeconds(0) (дольше), и взрыв бы ещё не произошёл");
        }

        [Test]
        public void Update_CalledRepeatedly_DoesNotRebuildOnceAlreadyBuilt()
        {
            SetUpReproducingRealBootstrapRace();
            InvokePrivate(_controller, "Update");
            var firstArrowWave = GetPrivateField(_controller, "_arrowWave");

            InvokePrivate(_controller, "Update");
            InvokePrivate(_controller, "Update");

            Assert.AreSame(firstArrowWave, GetPrivateField(_controller, "_arrowWave"),
                "повторные кадры не должны пересобирать уже построенные системы заново");
        }

        // Обратная сторона: если RunStarted пойман штатно (подписка успела
        // ДО события — обычный рестарт на уже полностью собранной сцене),
        // самолечение в Update() не должно создавать вторую, лишнюю копию.
        [Test]
        public void Update_AfterOrdinaryRunStarted_DoesNotDuplicateSystems()
        {
            _hostObject = new GameObject("TrapController_Host");
            _input = _hostObject.AddComponent<GridTraceInputController>();
            InvokePrivate(_input, "Awake");

            _controller = _hostObject.AddComponent<TrapSystemsController>();
            InvokePrivate(_controller, "Awake");
            InvokePrivate(_controller, "OnEnable");

            _input.Restart(); // штатный путь — подписка уже есть, ловит RunStarted вовремя
            var afterRestart = GetPrivateField(_controller, "_arrowWave");
            Assert.IsNotNull(afterRestart, "обычный рестарт после готовой подписки уже должен был построить систему без всякого самолечения");

            InvokePrivate(_controller, "Update");

            Assert.AreSame(afterRestart, GetPrivateField(_controller, "_arrowWave"), "Update() не должен пересобирать уже штатно построенные системы");
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} не найден рефлексией — сигнатура/имя изменились?");
            method.Invoke(target, args);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} не найдено рефлексией — имя поля изменилось?");
            return field.GetValue(target);
        }
    }
}
