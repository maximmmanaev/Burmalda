using System;
using System.Reflection;
using Burmalda.Core;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    /// <summary>
    /// Issue #157 (перенос схемы A "удержание/отпускание" из спайка в
    /// продукт, PRD 4.1 переработка §4.1): полная связка
    /// нажатие→примеривание→ведение→отпускание/отмена, ход событий
    /// (RunConfirmed/PositionChanged) и правила хода (соседство, повторный
    /// шаг на пройденную плиту #61 — не менялись, здесь только регрессия).
    ///
    /// <see cref="GridTraceInputController.HandlePointerPressed"/>/
    /// <see cref="GridTraceInputController.HandlePointerReleased"/> —
    /// приватные (реальный Input System/Update() тестами не покрыт, как и
    /// раньше) — вызываются рефлексией, тот же паттерн, что и у прежней
    /// TryAdvanceAtScreenPosition в этом файле до переноса схемы A.
    /// </summary>
    public class GridTraceInputControllerTapTests
    {
        private const int Width = 5;
        private static readonly GridCoordinate StartCoordinate = new GridCoordinate(0, Width / 2);

        private GameObject _cameraObject;
        private GameObject _inputObject;
        private GridTraceInputController _controller;
        private GameObject _tileObjectA;
        private GameObject _tileObjectB;

        [TearDown]
        public void TearDown()
        {
            if (_tileObjectA != null) UnityEngine.Object.DestroyImmediate(_tileObjectA);
            if (_tileObjectB != null) UnityEngine.Object.DestroyImmediate(_tileObjectB);
            if (_inputObject != null) UnityEngine.Object.DestroyImmediate(_inputObject);
            if (_cameraObject != null) UnityEngine.Object.DestroyImmediate(_cameraObject);
        }

        // PRD 4.1 п.1: касание соседней плиты — примеривание, не шаг.
        [Test]
        public void PressOnAdjacentValidTile_SetsPreviewTarget_DoesNotAdvanceTrail()
        {
            var target = new GridCoordinate(1, Width / 2);
            SetUpController(out var positionChangedCount);
            _controller.Grid.GetOrCreateTile(target);
            _tileObjectA = CreateTileVisual(target);

            InvokePointerPressed(ScreenPositionOf(target));

            Assert.AreEqual(target, _controller.PreviewTarget, "касание соседней валидной плиты должно примеривать её");
            Assert.AreEqual(StartCoordinate, _controller.Trail.CurrentPosition, "примеривание не должно двигать трейл");
            Assert.AreEqual(0, positionChangedCount());
        }

        // PRD 4.1 п.2: ведение пальцем без отрыва переносит примеривание.
        [Test]
        public void DragToAnotherAdjacentTile_WithoutRelease_MovesPreviewTarget()
        {
            var targetA = new GridCoordinate(1, Width / 2);
            var targetB = new GridCoordinate(0, Width / 2 + 1);
            SetUpController(out _);
            _controller.Grid.GetOrCreateTile(targetA);
            _controller.Grid.GetOrCreateTile(targetB);
            _tileObjectA = CreateTileVisual(targetA);
            _tileObjectB = CreateTileVisual(targetB);

            InvokePointerPressed(ScreenPositionOf(targetA));
            Assert.AreEqual(targetA, _controller.PreviewTarget);

            InvokePointerPressed(ScreenPositionOf(targetB)); // не отпуская — тот же непрерывный жест

            Assert.AreEqual(targetB, _controller.PreviewTarget, "ведение без отрыва должно перенести примеривание на новую плиту");
            Assert.AreEqual(StartCoordinate, _controller.Trail.CurrentPosition, "трейл всё ещё не должен был продвинуться");
        }

        // PRD 4.1 п.3: отпускание совершает шаг.
        [Test]
        public void ReleaseWhilePreviewingValidTile_AdvancesTrail_AndLocksInput()
        {
            var target = new GridCoordinate(1, Width / 2);
            SetUpController(out var positionChangedCount);
            _controller.Grid.GetOrCreateTile(target);
            _tileObjectA = CreateTileVisual(target);

            InvokePointerPressed(ScreenPositionOf(target));
            InvokePointerReleased();

            Assert.AreEqual(target, _controller.Trail.CurrentPosition, "отпускание над валидной примериваемой плитой должно засчитаться шагом");
            Assert.AreEqual(1, positionChangedCount());
            Assert.IsNull(_controller.PreviewTarget, "примеривание должно сброситься после шага");
            Assert.IsTrue(_controller.IsInputLocked, "сразу после шага ввод должен быть заблокирован на StepDurationSeconds");
        }

        // PRD 4.1 п.4: отпускание вне валидной соседней плиты — отмена.
        [Test]
        public void ReleaseOutsideValidAdjacentTile_CancelsWithoutStep()
        {
            SetUpController(out var positionChangedCount);
            // Ничего не примеривали (палец никогда не был над валидной
            // плитой) — отпускание "в никуда".

            InvokePointerReleased();

            Assert.AreEqual(StartCoordinate, _controller.Trail.CurrentPosition, "отмена не должна сдвигать трейл");
            Assert.AreEqual(0, positionChangedCount());
            Assert.IsFalse(_controller.IsInputLocked, "отмена не анимирует шаг — блокировки ввода быть не должно");
        }

        // PRD 4.1 п.3: блокировка ввода настраиваема и лежит в заданном
        // задачей диапазоне 0.15-0.5с по умолчанию (сам гейт — в Update(),
        // читающем IsInputLocked; Update()/реальный Input System тестами не
        // покрыты по тем же причинам, что и раньше, см. doc-комментарий
        // класса — здесь фиксируем именно публичный контракт длительности).
        [Test]
        public void StepDurationSeconds_DefaultsWithinTaskRange()
        {
            SetUpController(out _);
            Assert.GreaterOrEqual(_controller.StepDurationSeconds, 0.15f);
            Assert.LessOrEqual(_controller.StepDurationSeconds, 0.5f);
        }

        // PRD 4.1: тап по своей же текущей позиции — кнопка (RunConfirmed), не примеривание.
        [Test]
        public void PressOnOwnCurrentTile_DoesNotPreview_ButRaisesRunConfirmed()
        {
            SetUpController(out var positionChangedCount);
            _tileObjectA = CreateTileVisual(StartCoordinate);
            var runConfirmedCount = 0;
            _controller.RunConfirmed += () => runConfirmedCount++;

            InvokePointerPressed(ScreenPositionOf(StartCoordinate));

            Assert.IsNull(_controller.PreviewTarget, "тап на собственную позицию не должен считаться примериванием");
            Assert.AreEqual(StartCoordinate, _controller.Trail.CurrentPosition);
            Assert.AreEqual(0, positionChangedCount());
            Assert.AreEqual(1, runConfirmedCount, "тап на свою же плитку — сигнал RunConfirmed (кнопка)");
        }

        // Issue #61 — регрессия: повторный шаг на пройденную, но не разрушенную плиту разрешён.
        [Test]
        public void ReleaseOnVisitedUndestroyedTile_StepAllowed()
        {
            var firstStep = new GridCoordinate(1, Width / 2);
            SetUpController(out _);
            _controller.Grid.GetOrCreateTile(firstStep);
            _tileObjectA = CreateTileVisual(firstStep);
            InvokePointerPressed(ScreenPositionOf(firstStep));
            InvokePointerReleased(); // первый шаг вперёд, плита firstStep теперь посещена

            // Второй шаг — назад, на стартовую плиту (уже посещена, не разрушена).
            _tileObjectB = CreateTileVisual(StartCoordinate);
            InvokePointerPressed(ScreenPositionOf(StartCoordinate));

            Assert.AreEqual(StartCoordinate, _controller.PreviewTarget, "повторный шаг на пройденную целую плиту должен быть доступен для примеривания (#61)");
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
            _tileObjectA = CreateTileVisual(target);
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
            // как и приватные обработчики ниже.
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
            // видит (проверено эмпирически: без этой строки тесты,
            // ожидающие реальное попадание в плитку, падали с "null"/"не
            // продвинулся", хотя геометрия луча была верной).
            Physics.SyncTransforms();

            return tile;
        }

        private void InvokePointerPressed(Vector2 screenPosition) =>
            InvokePrivate(_controller, "HandlePointerPressed", screenPosition);

        private void InvokePointerReleased() =>
            InvokePrivate(_controller, "HandlePointerReleased");

        /// <summary>Вызывает приватный метод рефлексией — единственный способ прогнать Awake()/обработчики ввода вне Play Mode без переписывания доступа production-кода ради тестируемости.</summary>
        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} не найден рефлексией — сигнатура/имя изменились?");
            method.Invoke(target, args);
        }
    }
}
