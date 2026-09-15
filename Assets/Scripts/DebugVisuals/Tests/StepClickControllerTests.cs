using System.Reflection;
using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.DebugVisuals.Tests
{
    /// <summary>
    /// Issue #264 («звук щелчка на каждый шаг по обычной плитке»). Звук как
    /// таковой один в один не проверить тестом — но то, что нужный
    /// audio-clip реально строится/проигрывается в нужном месте (а не в
    /// исключённых — опасная/скрытая), проверяемо через ссылку на приватное
    /// поле построенного клипа, рефлексия, тот же паттерн, что
    /// <c>Movement.Tests.TrapSystemsControllerTests</c>.
    /// </summary>
    public class StepClickControllerTests
    {
        private GameObject _hostObject;
        private GridTraceInputController _input;
        private StepClickController _controller;

        [TearDown]
        public void TearDown()
        {
            if (_hostObject != null) Object.DestroyImmediate(_hostObject);
        }

        private void SetUp()
        {
            _hostObject = new GameObject("StepClick_Host");
            _input = _hostObject.AddComponent<GridTraceInputController>();
            InvokePrivate(_input, "Awake");

            _controller = _hostObject.AddComponent<StepClickController>();
            InvokePrivate(_controller, "Update"); // подключает трейл, тот же приём, что PickupFeedback
        }

        [Test]
        public void HandlePositionChanged_OrdinaryTile_BuildsAndPlaysClickClip()
        {
            SetUp();
            var target = new GridCoordinate(1, _input.Grid.Width / 2);
            _input.Grid.GetOrCreateTile(target); // материализует плиту — TryGetTile иначе не найдёт её (тот же принцип, что и везде в проекте)

            InvokePrivate(_controller, "HandlePositionChanged", target);

            var clip = (AudioClip)GetPrivateField(_controller, "_clickClip");
            Assert.IsNotNull(clip, "обычная плита обязана проиграть щелчок");
            Assert.Greater(clip.length, 0f);
        }

        [Test]
        public void HandlePositionChanged_LethalTrapTile_DoesNotPlayClick()
        {
            SetUp();
            var target = new GridCoordinate(1, _input.Grid.Width / 2);
            _input.Grid.GetOrCreateTile(target).TransitionToLethalTrap(LethalTrapType.ArrowWave);

            InvokePrivate(_controller, "HandlePositionChanged", target);

            Assert.IsNull(GetPrivateField(_controller, "_clickClip"), "опасная плита не должна давать обычный щелчок");
        }

        [Test]
        public void HandlePositionChanged_HiddenTrapTriggerTile_DoesNotPlayClick_EvenBeforeReveal()
        {
            SetUp();
            var target = new GridCoordinate(1, _input.Grid.Width / 2);
            _input.Grid.GetOrCreateTile(target).MarkBombTrigger(); // триггер, но IsDangerSignatureRevealed ещё ложь

            InvokePrivate(_controller, "HandlePositionChanged", target);

            Assert.IsNull(GetPrivateField(_controller, "_clickClip"), "скрытый триггер ловушки не должен давать обычный щелчок, даже нераскрытый");
        }

        [Test]
        public void HandlePositionChanged_CurrencySourceTile_StillPlaysClick_NotExcluded()
        {
            // Источник валюты/Алтарь/рычаг и т.п. — не "скрытая опасность" и
            // не "опасность", остаются "обычной" плитой для этой задачи.
            SetUp();
            var target = new GridCoordinate(1, _input.Grid.Width / 2);
            _input.Grid.GetOrCreateTile(target).MarkManaSource();

            InvokePrivate(_controller, "HandlePositionChanged", target);

            Assert.IsNotNull(GetPrivateField(_controller, "_clickClip"), "источник валюты не скрытая опасность и не летален — щелчок обязан проиграть");
        }

        [Test]
        public void RealPositionChangedEvent_OrdinaryStep_TriggersClick()
        {
            // Сквозной тест: контроллер реально подписан на
            // GridTraceTrail.PositionChanged, не только приватный метод
            // корректен в изоляции.
            SetUp();
            var target = new GridCoordinate(1, _input.Grid.Width / 2);

            Assert.IsTrue(_input.Trail.TryAdvanceTo(target));

            Assert.IsNotNull(GetPrivateField(_controller, "_clickClip"));
        }

        [Test]
        public void RealPositionChangedEvent_RevisitingAlreadyVisitedTile_StillTriggersClick()
        {
            // #61: "на каждый шаг", не только на первое посещение — трейл
            // подписка идёт на PositionChanged, не Advanced.
            SetUp();
            var target = new GridCoordinate(1, _input.Grid.Width / 2);
            var start = _input.Trail.CurrentPosition;
            Assert.IsTrue(_input.Trail.TryAdvanceTo(target));
            // Сбрасываем зафиксированный клип, чтобы проверить именно повторный шаг.
            typeof(StepClickController).GetField("_clickClip", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_controller, null);

            Assert.IsTrue(_input.Trail.TryAdvanceTo(start)); // назад, на уже посещённую плитку

            Assert.IsNotNull(GetPrivateField(_controller, "_clickClip"), "повторный шаг на уже пройденную плитку тоже должен звучать");
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
