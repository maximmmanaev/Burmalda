using System.Reflection;
using Burmalda.Altar;
using Burmalda.Boss;
using Burmalda.Camp;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Decay;
using Burmalda.Generation;
using Burmalda.Movement;
using Burmalda.RunLifecycle;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Bootstrap.Tests
{
    /// <summary>
    /// Задача "композиционный корень RunBootstrap": до этой задачи
    /// CurrencyController/RunArtifactLoadout/RunDepthTier/BossController
    /// каждый раз обнаруживались НЕ созданными в реальной игре только когда
    /// кто-то пытался показать значение на экране. Тесты здесь проверяют
    /// именно то, из-за чего это происходило — порядок сборки, а не
    /// внутреннюю логику самих систем (она уже покрыта их собственными
    /// тестами и не менялась).
    ///
    /// Приватные методы (<c>Awake</c>/<c>Update</c>) вызываются рефлексией —
    /// тот же паттерн, что <c>Movement.Tests.GridTraceInputControllerTapTests</c>:
    /// вне Play Mode <c>GameObject.AddComponent&lt;T&gt;()</c> НЕ вызывает
    /// <c>Awake()</c> нового компонента автоматически (в отличие от реальной
    /// игры/сборки) — после каждого <see cref="RunBootstrap"/>.Update(),
    /// добавившего новые контроллеры, их <c>Awake()</c> нужно вызвать вручную,
    /// иначе их собственные приватные поля (`_input`/`_currency`/`_altar`)
    /// останутся null и все дальнейшие проверки будут ложно-отрицательными.
    /// </summary>
    public class RunBootstrapTests
    {
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

        /// <summary>Только GridTraceInputController — минимум для тестов, которым RunController/TrailDecayController не нужны.</summary>
        private void SetUpMinimal()
        {
            _inputObject = new GameObject("Bootstrap_Input");
            _input = _inputObject.AddComponent<GridTraceInputController>();
            InvokePrivate(_input, "Awake");

            _bootstrapObject = new GameObject("Bootstrap_Root");
            _bootstrap = _bootstrapObject.AddComponent<RunBootstrap>();
            InvokePrivate(_bootstrap, "Awake");
        }

        /// <summary>
        /// + TrailDecayController/RunController на том же объекте, что и в
        /// реальной сцене (см. doc-комментарий RunBootstrap — они уже там) —
        /// нужны тестам, проверяющим Ярус Глубины: BossController.IsReady()
        /// требует RunController, RunController требует TrailDecayController.
        /// </summary>
        private void SetUpWithRunController()
        {
            SetUpMinimal();

            var decay = _inputObject.AddComponent<TrailDecayController>();
            InvokePrivate(decay, "Awake");
            InvokePrivate(decay, "Update"); // материализует Decay

            var runController = _inputObject.AddComponent<RunController>();
            InvokePrivate(runController, "Awake");
            InvokePrivate(runController, "Update"); // материализует RunState
        }

        /// <summary>
        /// RunBootstrap.Update() добавляет контроллеры через AddComponent —
        /// вне Play Mode это не вызывает их Awake() само по себе (см.
        /// doc-комментарий класса), поэтому вызываем его здесь вручную для
        /// каждого — ровно то, что Unity сделала бы сама в реальной игре в
        /// момент AddComponent. Второй вызов Update() в конце — тем же
        /// путём: EnsureLoadoutReady() внутри первого Update() видит
        /// Altar.Collection ещё null (Altar.Awake() на тот момент не
        /// вызывался), лоадаут создаётся только на следующем тике —
        /// в реальной игре это следующий кадр, здесь — второй вызов Update().
        /// </summary>
        private void WireAndAwakeControllers()
        {
            InvokePrivate(_bootstrap, "Update");
            InvokePrivate(_bootstrap.Currency, "Awake");
            InvokePrivate(_bootstrap.Altar, "Awake");
            InvokePrivate(_bootstrap.Boss, "Awake");
            InvokePrivate(_bootstrap.Camp, "Awake");
            InvokePrivate(_bootstrap.Lever, "Awake");
            InvokePrivate(_bootstrap, "Update");
        }

        [Test]
        public void Update_WiresAllListedControllers_OntoInputGameObject()
        {
            SetUpMinimal();

            WireAndAwakeControllers();

            Assert.IsNotNull(_inputObject.GetComponent<CurrencyController>());
            Assert.IsNotNull(_inputObject.GetComponent<AltarController>());
            Assert.IsNotNull(_inputObject.GetComponent<BossController>());
            Assert.IsNotNull(_inputObject.GetComponent<CampController>());
            Assert.IsNotNull(_inputObject.GetComponent<LeverActivationController>());
        }

        [Test]
        public void Update_ExposesWiredControllers_ViaPublicProperties()
        {
            SetUpMinimal();

            WireAndAwakeControllers();

            Assert.AreSame(_inputObject.GetComponent<CurrencyController>(), _bootstrap.Currency);
            Assert.AreSame(_inputObject.GetComponent<AltarController>(), _bootstrap.Altar);
            Assert.AreSame(_inputObject.GetComponent<BossController>(), _bootstrap.Boss);
            Assert.AreSame(_inputObject.GetComponent<CampController>(), _bootstrap.Camp);
            Assert.AreSame(_inputObject.GetComponent<LeverActivationController>(), _bootstrap.Lever);
        }

        // Порядок AddComponent — не косметика (см. doc-комментарий класса):
        // AltarController читает CurrencyController через GetComponent РОВНО
        // ОДИН РАЗ в своём Awake(). Если бы порядок был обратным, это поле
        // осталось бы null навсегда.
        [Test]
        public void Update_AltarController_SeesCurrencyControllerAlreadyWired()
        {
            SetUpMinimal();

            WireAndAwakeControllers();

            var altarCurrencyField = GetPrivateField(_bootstrap.Altar, "_currency");
            Assert.AreSame(_bootstrap.Currency, altarCurrencyField, "AltarController должен был увидеть уже добавленный CurrencyController в своём Awake()");
        }

        [Test]
        public void Update_BossController_SeesCurrencyAndAltarAlreadyWired()
        {
            SetUpMinimal();

            WireAndAwakeControllers();

            Assert.AreSame(_bootstrap.Currency, GetPrivateField(_bootstrap.Boss, "_currency"));
            Assert.AreSame(_bootstrap.Altar, GetPrivateField(_bootstrap.Boss, "_altar"));
        }

        [Test]
        public void Update_CampController_SeesCurrencyAndAltarAlreadyWired()
        {
            SetUpMinimal();

            WireAndAwakeControllers();

            Assert.AreSame(_bootstrap.Currency, GetPrivateField(_bootstrap.Camp, "_currency"));
            Assert.AreSame(_bootstrap.Altar, GetPrivateField(_bootstrap.Camp, "_altar"));
        }

        // Update() вызывается каждый кадр в реальной игре — повторный вызов
        // не должен падать (DisallowMultipleComponent бросает исключение при
        // повторном AddComponent того же типа) и не должен плодить дубликаты.
        [Test]
        public void Update_CalledRepeatedly_DoesNotDuplicateComponentsOrThrow()
        {
            SetUpMinimal();

            Assert.DoesNotThrow(() =>
            {
                for (var i = 0; i < 5; i++) InvokePrivate(_bootstrap, "Update");
            });

            Assert.AreEqual(1, _inputObject.GetComponents<CurrencyController>().Length);
            Assert.AreEqual(1, _inputObject.GetComponents<AltarController>().Length);
            Assert.AreEqual(1, _inputObject.GetComponents<BossController>().Length);
            Assert.AreEqual(1, _inputObject.GetComponents<CampController>().Length);
            Assert.AreEqual(1, _inputObject.GetComponents<LeverActivationController>().Length);
        }

        [Test]
        public void Update_CreatesArtifactLoadout_FromAltarPersistentCollection()
        {
            SetUpMinimal();

            WireAndAwakeControllers();

            Assert.IsNotNull(_bootstrap.Loadout, "Лоадаут должен был создаться, как только у Алтаря готова постоянная Коллекция");
            Assert.AreEqual(0, _bootstrap.Loadout.Acquired.Count);
        }

        [Test]
        public void RestartRun_RebuildsFreshArtifactLoadout_NotReusingOldInstance()
        {
            SetUpMinimal();
            WireAndAwakeControllers();
            var firstLoadout = _bootstrap.Loadout;

            _input.Restart(); // поднимает RunStarted — RunBootstrap подписан на него в EnsureRunStartedSubscription

            Assert.AreNotSame(firstLoadout, _bootstrap.Loadout, "RunArtifactLoadout — новый экземпляр на каждый забег (см. её doc-комментарий), не переиспользуется");
        }

        [Test]
        public void DepthTier_BeforeBossHasRunSystems_IsNull()
        {
            SetUpMinimal();

            WireAndAwakeControllers();

            Assert.IsNull(_bootstrap.DepthTier, "Без RunController у Босса нет собственных run-систем — потребители должны увидеть null (прочерк), не выдуманный ноль");
        }

        // Полная сборка (RunController/TrailDecayController — как на реальной
        // сцене) — Ярус должен быть ЖИВЫМ: тем же экземпляром, что реально
        // продвигается победой над Боссом, а не отдельной подделкой внутри
        // какого-то потребителя (issue задачи: "Ярус живой, не фальшивый
        // экземпляр внутри HUD").
        [Test]
        public void DepthTier_WithFullWiring_ReflectsRealBossVictory()
        {
            SetUpWithRunController();
            WireAndAwakeControllers();
            InvokePrivate(_bootstrap.Currency, "Update"); // материализует RunManaCrystals/RunKeys
            InvokePrivate(_bootstrap.Altar, "Update"); // материализует Pool/Collection-зависимый AltarTriggerSystem
            InvokePrivate(_bootstrap.Boss, "Update"); // материализует BossEncounterSystem + RunDepthTier

            Assert.IsNotNull(_bootstrap.DepthTier, "После пересборки run-систем Босса Ярус должен существовать");
            Assert.AreSame(_bootstrap.Boss.DepthTier, _bootstrap.DepthTier, "RunBootstrap.DepthTier обязан быть тем же экземпляром, что у BossController — не копией");

            var bossCoordinate = new GridCoordinate(1, 2);
            _input.Grid.GetOrCreateTile(bossCoordinate).MarkBoss();
            _bootstrap.Currency.RunManaCrystals.Add(100000); // заведомо достаточно для победы черновым порогом

            _input.Trail.TryAdvanceTo(bossCoordinate);

            Assert.AreEqual(1, _bootstrap.DepthTier.CurrentTier, "Победа над Боссом должна была продвинуть тот самый Ярус, что читает RunBootstrap");
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
