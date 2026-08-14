using System;
using System.Reflection;
using Burmalda.Core;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    /// <summary>
    /// Регресс-тесты на баг (2026-08-14, не гипотеза): тап по пустому месту
    /// экрана (где нет физически отрендеренной плитки) двигал камеру/трейл —
    /// причина была в <see cref="GridTraceInputController.TryAdvanceAtScreenPosition"/>,
    /// кастовавшем луч на абстрактную математическую плоскость пола
    /// (<c>Plane(Vector3.up, Vector3.zero)</c>), а не на реальные объекты
    /// плиток. ЛЮБАЯ точка тапа давала какую-то мировую точку → какую-то
    /// GridCoordinate, и если она проходила <c>grid.Contains</c>/
    /// <c>IsAdjacentTo</c> (а не факт материализации/рендера), ход
    /// засчитывался.
    ///
    /// Фикс — <see cref="GridTraceInputController.ResolveTappedTile"/>:
    /// Physics.Raycast против реальных коллайдеров
    /// (<see cref="TileVisualMarker"/>), координата читается прямо с
    /// попавшего коллайдера. <see cref="GridTraceTrail.CanAdvanceTo"/>/
    /// <see cref="GridTraceTrail.TryAdvanceTo"/> не менялись — здесь
    /// проверяется только то, как координата попадает туда со стороны
    /// инпута.
    ///
    /// <see cref="GridTraceInputController.TryAdvanceAtScreenPosition"/> —
    /// приватный (Update()/реальный Input System по-прежнему не покрыты
    /// тестами, см. <see cref="GridTraceInputControllerTests"/>), поэтому
    /// вызывается через рефлексию — единственный способ проверить полную
    /// связку "тап -> трейл/PositionChanged/RunConfirmed" без переписывания
    /// доступа production-кода ради тестируемости.
    /// </summary>
    public class GridTraceInputControllerTapTests
    {
        private const int Width = 5;
        private static readonly GridCoordinate StartCoordinate = new GridCoordinate(0, Width / 2);

        private GameObject _cameraObject;
        private GameObject _inputObject;
        private GridTraceInputController _controller;
        private GameObject _tileObject;

        [TearDown]
        public void TearDown()
        {
            if (_tileObject != null) UnityEngine.Object.DestroyImmediate(_tileObject);
            if (_inputObject != null) UnityEngine.Object.DestroyImmediate(_inputObject);
            if (_cameraObject != null) UnityEngine.Object.DestroyImmediate(_cameraObject);
        }

        [Test]
        public void TapOnEmptySpace_NoTileThere_DoesNotAdvanceTrailOrRaisePositionChanged()
        {
            // Соседняя (0,2)->(1,3) координата — валидна по adjacency, но
            // здесь намеренно НЕ создаём ни данные (GetOrCreateTile), ни
            // визуальный объект — ровно баговый сценарий: "пустое место".
            var emptyCoordinate = new GridCoordinate(1, Width / 2 + 1);
            SetUpController(out var positionChangedCount);
            var screenPosition = ScreenPositionOf(emptyCoordinate);

            InvokeTryAdvanceAtScreenPosition(screenPosition);

            Assert.AreEqual(StartCoordinate, _controller.Trail.CurrentPosition, "трейл не должен был продвинуться");
            Assert.AreEqual(0, positionChangedCount(), "PositionChanged не должен был подняться");
        }

        [Test]
        public void TapOnAdjacentMaterializedTile_AdvancesTrail()
        {
            var target = new GridCoordinate(1, Width / 2);
            SetUpController(out var positionChangedCount);
            _controller.Grid.GetOrCreateTile(target); // как TunnelGridReveal материализует данные заранее
            _tileObject = CreateTileVisual(target);
            var screenPosition = ScreenPositionOf(target);

            InvokeTryAdvanceAtScreenPosition(screenPosition);

            Assert.AreEqual(target, _controller.Trail.CurrentPosition, "тап на существующую соседнюю плитку должен засчитаться ходом");
            Assert.AreEqual(1, positionChangedCount());
        }

        [Test]
        public void TapOnOwnCurrentTile_DoesNotAdvance_ButRaisesRunConfirmed()
        {
            SetUpController(out var positionChangedCount);
            _tileObject = CreateTileVisual(StartCoordinate); // стартовая плита уже материализована конструктором трейла
            var runConfirmedCount = 0;
            _controller.RunConfirmed += () => runConfirmedCount++;
            var screenPosition = ScreenPositionOf(StartCoordinate);

            InvokeTryAdvanceAtScreenPosition(screenPosition);

            Assert.AreEqual(StartCoordinate, _controller.Trail.CurrentPosition, "тап на собственную позицию не должен считаться ходом");
            Assert.AreEqual(0, positionChangedCount(), "PositionChanged не должен подниматься для тапа на свою же плитку");
            Assert.AreEqual(1, runConfirmedCount, "тап на свою же плитку — сигнал RunConfirmed (кнопка)");
        }

        [Test]
        public void ResolveTappedTile_EmptySpace_ReturnsNull()
        {
            SetUpController(out _);
            var screenPosition = ScreenPositionOf(new GridCoordinate(1, Width / 2 + 1)); // ничего там не создано

            var resolved = GridTraceInputController.ResolveTappedTile(_cameraObject.GetComponent<Camera>(), screenPosition);

            Assert.IsNull(resolved);
        }

        [Test]
        public void ResolveTappedTile_HitsTileCollider_ReturnsItsCoordinate()
        {
            SetUpController(out _);
            var target = new GridCoordinate(1, Width / 2);
            _tileObject = CreateTileVisual(target);
            var screenPosition = ScreenPositionOf(target);

            var resolved = GridTraceInputController.ResolveTappedTile(_cameraObject.GetComponent<Camera>(), screenPosition);

            Assert.AreEqual(target, resolved);
        }

        private void SetUpController(out Func<int> positionChangedCount)
        {
            _cameraObject = new GameObject("TapTest_Camera") { tag = "MainCamera" };
            _cameraObject.AddComponent<Camera>();

            _inputObject = new GameObject("TapTest_Input");
            _controller = _inputObject.AddComponent<GridTraceInputController>();
            // Camera.main (Awake() ниже) — GameObject.FindWithTag под
            // капотом, ненадёжно в batchmode EditMode-тестах (эмпирически:
            // не резолвился до вызова Awake() рефлексией). Ставим приватное
            // поле _camera напрямую — детерминированно, без зависимости от
            // тега/поиска по сцене.
            typeof(GridTraceInputController)
                .GetField("_camera", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_controller, _cameraObject.GetComponent<Camera>());

            // Вне Play Mode Unity НЕ вызывает Awake() автоматически (нет
            // [ExecuteInEditMode]/[ExecuteAlways] — и не должно быть, это
            // обычный игровой MonoBehaviour) — вызываем вручную рефлексией,
            // как и приватный TryAdvanceAtScreenPosition ниже.
            InvokePrivate(_controller, "Awake");

            var count = 0;
            _controller.Trail.PositionChanged += _ => count++;
            positionChangedCount = () => count;
        }

        /// <summary>Экранная точка, глядя на которую камера сверху-вниз упирается ровно в мировую позицию координаты.</summary>
        private Vector2 ScreenPositionOf(GridCoordinate coordinate)
        {
            var worldPosition = _controller.Projection.ToWorldPosition(coordinate);
            var camera = _cameraObject.GetComponent<Camera>();
            camera.transform.SetPositionAndRotation(
                new Vector3(worldPosition.x, 10f, worldPosition.z),
                Quaternion.Euler(90f, 0f, 0f)); // строго вниз — без параллакса, экранная точка совпадает с центром плитки
            return camera.WorldToScreenPoint(worldPosition);
        }

        private GameObject CreateTileVisual(GridCoordinate coordinate)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube); // даёт BoxCollider бесплатно, как и TunnelDebugVisual
            tile.transform.position = _controller.Projection.ToWorldPosition(coordinate);
            tile.AddComponent<TileVisualMarker>().Initialize(coordinate);

            // Вне Play Mode нет FixedUpdate-тика физики, который обычно
            // синхронизирует Transform -> физическую сцену — без явного
            // вызова свежепереставленный коллайдер Physics.Raycast ниже не
            // видит (проверено эмпирически: без этой строки все три теста,
            // ожидающих реальное попадание в плитку, падали с "null"/"не
            // продвинулся", хотя геометрия луча была верной).
            Physics.SyncTransforms();

            return tile;
        }

        private void InvokeTryAdvanceAtScreenPosition(Vector2 screenPosition) =>
            InvokePrivate(_controller, "TryAdvanceAtScreenPosition", screenPosition);

        /// <summary>Вызывает приватный метод рефлексией — единственный способ прогнать Awake()/TryAdvanceAtScreenPosition вне Play Mode без переписывания доступа production-кода ради тестируемости.</summary>
        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} не найден рефлексией — сигнатура/имя изменились?");
            method.Invoke(target, args);
        }
    }
}
