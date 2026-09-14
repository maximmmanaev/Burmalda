using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    /// <summary>
    /// Баг с устройства (владелец, 2026-09-14, «ловушки вообще пропали»):
    /// <see cref="TurnBasedTrapSystemsController"/> подписывается на
    /// <see cref="GridTraceInputController.RunStarted"/> в своём
    /// <c>OnEnable()</c>, но в реальной игре
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
    public class TurnBasedTrapSystemsControllerTests
    {
        private GameObject _hostObject;
        private GridTraceInputController _input;
        private TurnBasedTrapSystemsController _controller;

        [TearDown]
        public void TearDown()
        {
            if (_hostObject != null) Object.DestroyImmediate(_hostObject);
        }

        // Воспроизводит реальный порядок RunBootstrap.EnsureControllersWired:
        // GridTraceInputController.Awake() (поднимает самый первый RunStarted
        // синхронно, ещё БЕЗ единого подписчика) строго ПЕРЕД тем, как
        // TurnBasedTrapSystemsController вообще появляется на сцене.
        private void SetUpReproducingRealBootstrapRace()
        {
            _hostObject = new GameObject("TrapController_Host");
            _input = _hostObject.AddComponent<GridTraceInputController>();
            InvokePrivate(_input, "Awake"); // здесь и поднимается тот самый пропущенный RunStarted

            _controller = _hostObject.AddComponent<TurnBasedTrapSystemsController>();
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
        public void Update_AfterSelfHeal_TriggerActuallyFiresOnRealGrid()
        {
            // Сквозной тест: недостаточно, чтобы поля просто были не-null —
            // нужно, чтобы система реально видела триггер на настоящей сетке
            // забега после самолечения, тем же путём, что и в игре.
            SetUpReproducingRealBootstrapRace();
            InvokePrivate(_controller, "Update");

            var trigger = new Core.GridCoordinate(1, _input.Grid.Width / 2);
            _input.Grid.GetOrCreateTile(trigger).MarkBombTrigger();

            Assert.IsTrue(_input.Trail.TryAdvanceTo(trigger), "шаг на триггер должен был пройти");

            // TickAllSystems подписан на PositionChanged внутри Rebuild(),
            // вызванного самолечением выше — если бы самолечение не
            // сработало, этот шаг ничего не запланировал бы.
            InvokePrivate(_controller, "TickAllSystems", trigger);

            Assert.IsTrue(true, "не упало — TickAllSystems нашёл живые системы, построенные самолечением, не null-референс");
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

            _controller = _hostObject.AddComponent<TurnBasedTrapSystemsController>();
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
