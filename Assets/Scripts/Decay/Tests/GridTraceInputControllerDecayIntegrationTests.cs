using System.Reflection;
using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Decay.Tests
{
    /// <summary>
    /// Issue #157 (перенос схемы A из спайка): "время не останавливается
    /// никогда" — распад тикает и во время примеривания, и во время
    /// анимации шага (блокировки ввода). Регресс-тест на ОТСУТСТВИЕ связи
    /// между <see cref="GridTraceInputController"/> и <see cref="TrailDecaySystem"/> —
    /// они тикают независимо, ни то ни другое не гейтит другое (см.
    /// doc-комментарий класса контроллера).
    /// </summary>
    public class GridTraceInputControllerDecayIntegrationTests
    {
        private const int Width = 5;

        private GameObject _cameraObject;
        private GameObject _inputObject;
        private GameObject _tileObject;
        private GridTraceInputController _controller;

        [TearDown]
        public void TearDown()
        {
            if (_tileObject != null) Object.DestroyImmediate(_tileObject);
            if (_inputObject != null) Object.DestroyImmediate(_inputObject);
            if (_cameraObject != null) Object.DestroyImmediate(_cameraObject);
        }

        [Test]
        public void Tick_WhilePreviewing_StillAdvancesDecayOnSteppedTile()
        {
            SetUpController();
            var decay = new TrailDecaySystem(_controller.Grid, _controller.Trail);
            var stepped = new GridCoordinate(1, Width / 2);
            _controller.Trail.TryAdvanceTo(stepped); // порог распада начат

            // Игрок держит палец, примеривая СОСЕДНЮЮ плиту (не двигая
            // трейл) — decay не должен знать/заботиться об этом состоянии.
            var preview = new GridCoordinate(1, Width / 2 + 1);
            _controller.Grid.GetOrCreateTile(preview);
            _tileObject = CreateTileVisual(preview);
            InvokePrivate(_controller, "HandlePointerPressed", ScreenPositionOf(preview));
            Assert.AreEqual(preview, _controller.PreviewTarget, "проверка сетапа: примеривание действительно активно");

            var before = _controller.Grid.GetOrCreateTile(stepped).DecayElapsedSeconds;
            decay.Tick(1f);
            var after = _controller.Grid.GetOrCreateTile(stepped).DecayElapsedSeconds;

            Assert.Greater(after, before, "распад должен тикать, пока игрок удерживает примеривание — раздумье стоит времени");
        }

        [Test]
        public void Tick_WhileInputLockedAfterStep_StillAdvancesDecay()
        {
            SetUpController();
            var decay = new TrailDecaySystem(_controller.Grid, _controller.Trail);
            var target = new GridCoordinate(1, Width / 2);
            _controller.Grid.GetOrCreateTile(target);
            _tileObject = CreateTileVisual(target);

            InvokePrivate(_controller, "HandlePointerPressed", ScreenPositionOf(target));
            InvokePrivate(_controller, "HandlePointerReleased"); // шаг совершён, ввод заблокирован на StepDurationSeconds
            Assert.IsTrue(_controller.IsInputLocked, "проверка сетапа: блокировка ввода действительно активна");

            var before = _controller.Grid.GetOrCreateTile(target).DecayElapsedSeconds;
            decay.Tick(1f);
            var after = _controller.Grid.GetOrCreateTile(target).DecayElapsedSeconds;

            Assert.Greater(after, before, "распад должен тикать во время анимации шага/блокировки ввода — пауз на раздумье нет");
        }

        private void SetUpController()
        {
            _cameraObject = new GameObject("DecayIntegration_Camera");
            var camera = _cameraObject.AddComponent<Camera>();

            _inputObject = new GameObject("DecayIntegration_Input");
            _controller = _inputObject.AddComponent<GridTraceInputController>();
            typeof(GridTraceInputController)
                .GetField("_camera", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_controller, camera);

            InvokePrivate(_controller, "Awake");
        }

        private Vector2 ScreenPositionOf(GridCoordinate coordinate)
        {
            var worldPosition = _controller.Projection.ToWorldPosition(coordinate);
            var camera = _cameraObject.GetComponent<Camera>();
            camera.transform.SetPositionAndRotation(
                new Vector3(worldPosition.x, 10f, worldPosition.z),
                Quaternion.Euler(90f, 0f, 0f));
            return camera.WorldToScreenPoint(worldPosition);
        }

        private GameObject CreateTileVisual(GridCoordinate coordinate)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.transform.position = _controller.Projection.ToWorldPosition(coordinate);
            tile.AddComponent<TileVisualMarker>().Initialize(coordinate);
            Physics.SyncTransforms(); // см. GridTraceInputControllerTapTests — без этого свежий Raycast коллайдер не видит
            return tile;
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} не найден рефлексией — сигнатура/имя изменились?");
            method.Invoke(target, args);
        }
    }
}
