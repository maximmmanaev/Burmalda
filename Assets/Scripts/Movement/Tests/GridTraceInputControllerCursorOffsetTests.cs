using System.Reflection;
using Burmalda.Core;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    /// <summary>
    /// Issue #153, тумблер 3: палец в нижней части экрана тянется к целевой
    /// плитке и закрывает её собой — фикс сдвигает точку рейкаста на
    /// <see cref="GridTraceInputController.CursorOffsetPixelsY"/> пикселей
    /// ВВЕРХ перед <see cref="GridTraceInputController.ResolveTappedTile"/>,
    /// не переписывая сам рейкаст (см. её doc-комментарий и
    /// <see cref="GridTraceInputControllerTapTests"/> — тот же фикс на
    /// реальные коллайдеры, cbf99ca). Явный тумблер (<see cref="GridTraceInputController.UseCursorOffset"/>),
    /// деф. ВЫКЛЮЧЕН — <see cref="GridTraceInputControllerTapTests"/> не
    /// тронуты ни на строчку. Relative-swipe ввод явно НЕ вводится (запрещено
    /// задачей) — здесь только сдвиг абсолютной экранной точки.
    /// </summary>
    public class GridTraceInputControllerCursorOffsetTests
    {
        private const int Width = 5;

        private GameObject _cameraObject;
        private GameObject _inputObject;
        private GridTraceInputController _controller;
        private GameObject _fingerTileObject;
        private GameObject _aboveTileObject;

        [TearDown]
        public void TearDown()
        {
            if (_aboveTileObject != null) Object.DestroyImmediate(_aboveTileObject);
            if (_fingerTileObject != null) Object.DestroyImmediate(_fingerTileObject);
            if (_inputObject != null) Object.DestroyImmediate(_inputObject);
            if (_cameraObject != null) Object.DestroyImmediate(_cameraObject);
        }

        [Test]
        public void ToggleOff_RaycastsExactlyAtFingerPosition_EvenWithNonZeroOffsetConfigured()
        {
            // Тумблер выключен — сконфигурированное смещение (пусть даже
            // ненулевое) не должно применяться вообще.
            var fingerTile = new GridCoordinate(1, Width / 2);
            SetUpController(useCursorOffset: false, cursorOffsetPixelsY: 120f);
            _fingerTileObject = CreateTileVisual(fingerTile);
            AimCameraStraightDownAt(fingerTile);
            var fingerScreenPosition = _cameraObject.GetComponent<Camera>().WorldToScreenPoint(_controller.Projection.ToWorldPosition(fingerTile));

            InvokeTryAdvanceAtScreenPosition(fingerScreenPosition);

            Assert.AreEqual(fingerTile, _controller.Trail.CurrentPosition, "при выключенном тумблере рейкаст должен попасть ровно в плитку под пальцем");
        }

        [Test]
        public void ToggleOn_RaycastsAboveFingerPosition_AdvancesToTileThereInstead()
        {
            // Игрок держит палец над плитой fingerTile (соседней стартовой),
            // но реальная цель ("голова" трейла) — aboveTile, на один ряд
            // дальше — именно она должна засчитаться при включённом тумблере.
            var fingerTile = new GridCoordinate(1, Width / 2);
            var aboveTile = new GridCoordinate(2, Width / 2); // на ряд дальше — соседняя fingerTile, не старту напрямую
            SetUpController(useCursorOffset: true, cursorOffsetPixelsY: 0f); // офсет выставим ниже, точно под геометрию теста

            _fingerTileObject = CreateTileVisual(fingerTile);
            _aboveTileObject = CreateTileVisual(aboveTile);

            var midWorldZ = (_controller.Projection.ToWorldPosition(fingerTile).z + _controller.Projection.ToWorldPosition(aboveTile).z) / 2f;
            var camera = _cameraObject.GetComponent<Camera>();
            camera.transform.SetPositionAndRotation(
                new Vector3(_controller.Projection.ToWorldPosition(fingerTile).x, 10f, midWorldZ),
                Quaternion.Euler(90f, 0f, 0f)); // строго вниз, без параллакса, камера не двигается между замерами ниже

            var fingerScreenPosition = camera.WorldToScreenPoint(_controller.Projection.ToWorldPosition(fingerTile));
            var aboveScreenPosition = camera.WorldToScreenPoint(_controller.Projection.ToWorldPosition(aboveTile));
            var offsetPixels = aboveScreenPosition.y - fingerScreenPosition.y;
            Assert.Greater(offsetPixels, 0f, "тестовая геометрия должна давать положительный (вверх по экрану) офсет — иначе тест ничего не проверяет");

            _controller.CursorOffsetPixelsY = offsetPixels;
            _controller.Trail.TryAdvanceTo(fingerTile); // продвигаем трейл обычным API — следующий тап целится в aboveTile (соседнюю ЕЙ)

            InvokeTryAdvanceAtScreenPosition(fingerScreenPosition); // палец на fingerTile, но офсет целит в aboveTile

            Assert.AreEqual(aboveTile, _controller.Trail.CurrentPosition, "рейкаст должен был попасть в плитку ВЫШЕ пальца (aboveTile), не под ним");
        }

        [Test]
        public void ToggleOn_DoesNotAffectCurrentScreenPositionExposedToOtherSystems()
        {
            var fingerTile = new GridCoordinate(1, Width / 2);
            SetUpController(useCursorOffset: true, cursorOffsetPixelsY: 200f);
            _fingerTileObject = CreateTileVisual(fingerTile);
            AimCameraStraightDownAt(fingerTile);
            var fingerScreenPosition = _cameraObject.GetComponent<Camera>().WorldToScreenPoint(_controller.Projection.ToWorldPosition(fingerTile));

            InvokeTryAdvanceAtScreenPosition(fingerScreenPosition);

            var currentScreenPosition = (Vector2)typeof(GridTraceInputController)
                .GetProperty(nameof(GridTraceInputController.CurrentScreenPosition))
                .GetValue(_controller);
            Assert.AreEqual(default(Vector2), currentScreenPosition, "TryAdvanceAtScreenPosition не должен сам записывать CurrentScreenPosition — это делает Update()");
        }

        private void SetUpController(bool useCursorOffset, float cursorOffsetPixelsY)
        {
            _cameraObject = new GameObject("CursorOffsetTest_Camera") { tag = "MainCamera" };
            _cameraObject.AddComponent<Camera>();

            _inputObject = new GameObject("CursorOffsetTest_Input");
            _controller = _inputObject.AddComponent<GridTraceInputController>();
            typeof(GridTraceInputController)
                .GetField("_camera", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_controller, _cameraObject.GetComponent<Camera>());

            InvokePrivate(_controller, "Awake");

            _controller.UseCursorOffset = useCursorOffset;
            _controller.CursorOffsetPixelsY = cursorOffsetPixelsY;
        }

        /// <summary>Камера строго сверху-вниз над координатой — без параллакса, как в GridTraceInputControllerTapTests.</summary>
        private void AimCameraStraightDownAt(GridCoordinate coordinate)
        {
            var worldPosition = _controller.Projection.ToWorldPosition(coordinate);
            _cameraObject.GetComponent<Camera>().transform.SetPositionAndRotation(
                new Vector3(worldPosition.x, 10f, worldPosition.z),
                Quaternion.Euler(90f, 0f, 0f));
        }

        private GameObject CreateTileVisual(GridCoordinate coordinate)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.transform.position = _controller.Projection.ToWorldPosition(coordinate);
            tile.AddComponent<TileVisualMarker>().Initialize(coordinate);
            Physics.SyncTransforms();
            return tile;
        }

        private void InvokeTryAdvanceAtScreenPosition(Vector2 screenPosition) =>
            InvokePrivate(_controller, "TryAdvanceAtScreenPosition", screenPosition);

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} не найден рефлексией — сигнатура/имя изменились?");
            method.Invoke(target, args);
        }
    }
}
