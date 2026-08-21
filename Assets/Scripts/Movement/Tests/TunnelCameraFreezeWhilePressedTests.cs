using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    /// <summary>
    /// Issue #157 (перенос схемы A из спайка в продукт, PRD 4.1): "камера
    /// обновляется только между шагами" — пока
    /// <see cref="GridTraceInputController.IsPressed"/> истинно,
    /// <see cref="TunnelCameraController.Update"/> не должен трогать
    /// Transform вообще. <c>Update()</c> вызывается рефлексией — тот же
    /// принцип, что и у Awake() в <see cref="GridTraceInputControllerTapTests"/>
    /// (вне Play Mode Unity не гоняет per-frame коллбэки сама).
    /// </summary>
    public class TunnelCameraFreezeWhilePressedTests
    {
        private GameObject _host;
        private GridTraceInputController _input;
        private TunnelCameraController _cameraController;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        [Test]
        public void Update_WhilePressed_NeverMovesTransform_ResumesOnceReleased()
        {
            _host = new GameObject("CameraFreezeTest_Host");
            _host.AddComponent<Camera>();
            _input = _host.AddComponent<GridTraceInputController>();
            _cameraController = _host.AddComponent<TunnelCameraController>();

            InvokePrivate(_input, "Awake");
            InvokePrivate(_cameraController, "Awake");
            InvokePrivate(_cameraController, "OnEnable");

            SetIsPressed(true);
            var positionBeforeAnyUpdate = _host.transform.position;
            var rotationBeforeAnyUpdate = _host.transform.rotation;

            InvokePrivate(_cameraController, "Update"); // первый кадр, палец уже прижат
            Assert.AreEqual(positionBeforeAnyUpdate, _host.transform.position, "камера не должна двигаться, пока палец на экране (кадр 1)");
            Assert.AreEqual(rotationBeforeAnyUpdate, _host.transform.rotation);

            InvokePrivate(_cameraController, "Update"); // второй кадр подряд, всё ещё прижат
            Assert.AreEqual(positionBeforeAnyUpdate, _host.transform.position, "камера не должна двигаться ни на одном кадре, пока палец на экране (кадр 2)");
            Assert.AreEqual(rotationBeforeAnyUpdate, _host.transform.rotation);

            SetIsPressed(false);
            InvokePrivate(_cameraController, "Update");
            Assert.AreNotEqual(positionBeforeAnyUpdate, _host.transform.position, "как только палец отпущен, камера должна возобновить обновление — иначе тест не отличил бы \"заморожена\" от \"вообще сломана\"");
        }

        private void SetIsPressed(bool value)
        {
            // IsPressed — авто-свойство с private set (обновляется реальным
            // Update() контроллера, здесь его симулировать не нужно —
            // проверяется реакция TunnelCameraController, не сам ввод).
            var property = typeof(GridTraceInputController).GetProperty(nameof(GridTraceInputController.IsPressed));
            var backingField = typeof(GridTraceInputController).GetField($"<{property.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(backingField, "backing field авто-свойства IsPressed не найден рефлексией — имя/тип изменились?");
            backingField.SetValue(_input, value);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} не найден рефлексией — сигнатура/имя изменились?");
            method.Invoke(target, args);
        }
    }
}
