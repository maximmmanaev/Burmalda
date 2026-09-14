using System.Reflection;
using Burmalda.Core;
using Burmalda.Decay;
using Burmalda.Movement;
using Burmalda.RunLifecycle;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.DebugVisuals.Tests
{
    /// <summary>
    /// Issue #266 [TEMP]: «во время смерти игрока однозначно понятно, что
    /// забег закончился» — минимальный тест на состояние/флаг (звук/UI как
    /// таковые тестом не проверить), не на визуал.
    ///
    /// Собирает ровно ту же топологию, что <c>IntegrationTests.CoreLoopIntegrationTests.SetUp</c>
    /// (GridTraceInputController + TrailDecayController + RunController на
    /// одном GameObject, приватные Awake/Update вызываются рефлексией — вне
    /// Play Mode Unity не вызывает их сама).
    /// </summary>
    public class DeathNotificationOverlayTests
    {
        private GameObject _inputObject;
        private GameObject _overlayObject;
        private GridTraceInputController _input;
        private RunController _runController;
        private DeathNotificationOverlay _overlay;

        [TearDown]
        public void TearDown()
        {
            if (_inputObject != null) Object.DestroyImmediate(_inputObject);
            if (_overlayObject != null) Object.DestroyImmediate(_overlayObject);
        }

        private void SetUp(int d20Roll = 5) // 5 — в диапазоне Death (1-9), тот же приём, что RunStateTests
        {
            _inputObject = new GameObject("DeathNotif_Input");
            _input = _inputObject.AddComponent<GridTraceInputController>();
            InvokePrivate(_input, "Awake");

            var decay = _inputObject.AddComponent<TrailDecayController>();
            InvokePrivate(decay, "Awake");
            InvokePrivate(decay, "Update");

            _runController = _inputObject.AddComponent<RunController>();
            _runController.RollD20Override = () => d20Roll; // issue #225 — не блиндовый глобальный RNG в тесте
            InvokePrivate(_runController, "Awake");
            InvokePrivate(_runController, "Update"); // строит RunState

            _overlayObject = new GameObject("DeathNotif_Overlay");
            _overlay = _overlayObject.AddComponent<DeathNotificationOverlay>();
            InvokePrivate(_overlay, "Update"); // подключает RunController/RunState, тот же приём, что PickupFeedback
        }

        private GridCoordinate KillPlayer()
        {
            var lethalTile = new GridCoordinate(1, _input.Grid.Width / 2);
            _input.Grid.GetOrCreateTile(lethalTile).TransitionToLethalTrap(LethalTrapType.ArrowWave);
            Assert.IsFalse(_input.Trail.TryAdvanceTo(lethalTile), "тест некорректен, если шаг на летальную ловушку не отклонён — исход d20 должен быть Death (roll=5)");
            return lethalTile;
        }

        [Test]
        public void Constructor_BeforeAnyDeath_NotShowingNotification()
        {
            SetUp();

            Assert.IsFalse(_overlay.IsShowingDeathNotification);
        }

        [Test]
        public void Died_ShowsDeathNotification()
        {
            SetUp();

            KillPlayer();

            Assert.IsTrue(_overlay.IsShowingDeathNotification, "issue #266: смерть обязана включать заметное уведомление");
        }

        [Test]
        public void Died_RunStateReportsNotAlive_ConsistentWithNotification()
        {
            SetUp();

            KillPlayer();

            Assert.IsFalse(_runController.RunState.IsAlive);
            Assert.IsTrue(_overlay.IsShowingDeathNotification);
        }

        [Test]
        public void Died_Fortune_DoesNotShowNotification()
        {
            // d20=20 — Fortune, игрок не умирает — уведомление не должно появляться.
            SetUp(d20Roll: 20);

            KillPlayer();

            Assert.IsTrue(_runController.RunState.IsAlive, "тест некорректен, если Fortune не защитила от смерти");
            Assert.IsFalse(_overlay.IsShowingDeathNotification);
        }

        [Test]
        public void Died_MessageContainsDeathReason()
        {
            SetUp();

            KillPlayer();

            var message = (string)GetPrivateField(_overlay, "_lastMessage");
            StringAssert.Contains(_runController.LastDeathReason, message,
                "уведомление обязано включать причину смерти, не только голый факт");
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
