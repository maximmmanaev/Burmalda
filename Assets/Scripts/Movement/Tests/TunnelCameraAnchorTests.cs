using Burmalda.Core;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    /// <summary>
    /// Issue #153, тумблер 2: якорь камеры по вьюпорту — проверенный через
    /// реальный <see cref="Camera.WorldToViewportPoint"/>, тот же паттерн,
    /// что и <see cref="TunnelCameraViewportFramingTests"/>/<see cref="TunnelCameraDepthVisibilityTests"/>.
    /// Инвариант A (доля экрана не зависит от темпа) проверяется СОВМЕСТНО с
    /// тумблером 1 (<see cref="TunnelCameraFollow.AdvanceStepTween"/>) —
    /// именно доезд фиксированной длительности убирает зависимость от темпа,
    /// сам якорь только задаёт, НА КАКОЙ доле держится игрок.
    /// </summary>
    public class TunnelCameraAnchorTests
    {
        private const float TileSize = 1f;
        private const int Width = 5;
        private const float PortraitAspect = 0.5625f; // 9:16 — единственная поддерживаемая ориентация
        private const float AnchorViewportY = 0.32f; // дефолт debug-панели, issue #153

        private static readonly Vector3 HeightOffset = new Vector3(0f, 6f, 0f);

        // Тот же расчёт, что и TunnelCameraController.Update() — калибровка
        // ШИРИНЫ намеренно остаётся на фиксированном TrailingRowsBehindPlayer
        // (см. doc-комментарий TunnelCameraAnchor/TunnelCameraController).
        private static float ComputeSteadyVerticalFov(WorldGridProjection projection, float pitchDeg)
        {
            var groundDistanceToRow = TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow(
                HeightOffset.z, projection.TileSize, TunnelCameraFollow.TrailingRowsBehindPlayer);
            var desiredHorizontalFovDeg = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(
                HeightOffset.y, groundDistanceToRow, pitchDeg, projection.TileSize);
            return TunnelCameraFraming.ComputeVerticalFovDegrees(desiredHorizontalFovDeg, PortraitAspect);
        }

        private static (GridTraceTrail trail, WorldGridProjection projection, TunnelCameraFollow follow, float vFov) CreateAnchoredSteadyState()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, Width / 2));
            var projection = new WorldGridProjection(TileSize, Width);
            var follow = new TunnelCameraFollow(trail, projection, HeightOffset);
            follow.ConfirmRun();
            follow.AdvanceIntroTween(TunnelCameraFollow.TweenDurationSeconds);

            var vFov = ComputeSteadyVerticalFov(projection, TunnelCameraFollow.DefaultPitchDegrees);
            var anchorTrailingDistance = TunnelCameraAnchor.ComputeTrailingDistanceForAnchor(
                HeightOffset.y, TunnelCameraFollow.DefaultPitchDegrees, vFov, AnchorViewportY);
            follow.TrailingDistance = anchorTrailingDistance;

            return (trail, projection, follow, vFov);
        }

        private static GameObject CreateCamera(string name, TunnelCameraFollow follow, float vFov)
        {
            var cameraObject = new GameObject(name);
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(follow.CurrentPosition, follow.CurrentRotation);
            camera.aspect = PortraitAspect;
            camera.fieldOfView = vFov;
            return cameraObject;
        }

        [Test]
        public void ComputeTrailingDistanceForAnchor_CurrentProjectGeometry_MatchesHistoricalMeasurementBallpark()
        {
            // Регресс-тест на саму формулу, независимо от Follow: при
            // устоявшейся геометрии проекта (H=6, Pitch=50°, портретный
            // vFOV, HeightOffset.Z=0) якорь 0.32 должен давать дистанцию
            // близкую к СТАРОЙ фиксированной (TrailingRowsBehindPlayer=3) —
            // не совпадение, а перепроверка того же порядка величины, что
            // уже измерялась на устройстве (см. комментарии
            // TunnelCameraViewportFramingTests, viewport.y≈0.324 при D=3).
            var projection = new WorldGridProjection(TileSize, Width);
            var vFov = ComputeSteadyVerticalFov(projection, TunnelCameraFollow.DefaultPitchDegrees);

            var distance = TunnelCameraAnchor.ComputeTrailingDistanceForAnchor(
                HeightOffset.y, TunnelCameraFollow.DefaultPitchDegrees, vFov, AnchorViewportY);

            Assert.AreEqual(TunnelCameraFollow.TrailingRowsBehindPlayer * TileSize, distance, 0.1f);
        }

        // Issue #153, инвариант A: доля высоты кадра, на которой оказывается
        // игрок в устоявшемся движении, отличается не более чем на ±3%
        // между медленным/средним/максимально быстрым темпом. Темп — в
        // Tick()-вызовах на ход (тикает раз за Update(), ~60/с).
        // ДЕФОЛТНАЯ длительность твина шага (100мс = 6 тиков) короче
        // интервала между ходами даже на самом быстром реалистичном темпе
        // (FastPace = 13 тиков ≈ 217мс) — твин успевает полностью доехать
        // ДО следующего хода на всех трёх темпах, поэтому здесь ожидается
        // ПОЧТИ НУЛЕВОЙ разброс, не просто "в пределах допуска" — если тест
        // однажды перестанет проходить с большим запасом, это сигнал, что
        // сама архитектура твина сломана, не только тюнинг чисел.
        [TestCase(60, TestName = "SlowPace")] // ~1 ход/с
        [TestCase(25, TestName = "MediumPace")] // ~2.4 хода/с
        [TestCase(13, TestName = "FastPace")] // ~4.6 хода/с — максимально достижимый темп
        public void SteadyMovement_ThreePaces_WithStepTween_PlayerAnchorSpreadWithinThreePercent(int ticksPerMove)
        {
            var (trail, projection, follow, vFov) = CreateAnchoredSteadyState();
            follow.StepTweenDurationSeconds = TunnelCameraFollow.DefaultStepTweenDurationSeconds;

            for (var cycle = 0; cycle < 10; cycle++)
            {
                trail.TryAdvanceTo(new GridCoordinate(trail.CurrentPosition.Row + 1, Width / 2));
                for (var t = 0; t < ticksPerMove; t++) follow.AdvanceStepTween(1f / 60f);
            }

            GameObject cameraObject = null;
            try
            {
                var camera = (cameraObject = CreateCamera("TestCamera_Pace", follow, vFov)).GetComponent<Camera>();
                var playerWorld = projection.ToWorldPosition(trail.CurrentPosition);
                var viewport = camera.WorldToViewportPoint(playerWorld);

                Assert.AreEqual(AnchorViewportY, viewport.y, 0.03f, $"ticksPerMove={ticksPerMove}: доля кадра под игроком должна быть в пределах ±3% от якоря на ЛЮБОМ темпе");
            }
            finally
            {
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                follow.Dispose();
            }
        }

        [Test]
        public void StepBackOntoVisitedTile_AnchorRecomputesInsteadOfSticking()
        {
            // Якорь должен держаться и назад, при возврате на уже пройденную
            // плиту (#61) — существующая подписка на PositionChanged (не
            // Advanced) не должна была сломаться правками ComputeTargetPosition.
            var (trail, projection, follow, vFov) = CreateAnchoredSteadyState();
            follow.StepTweenDurationSeconds = TunnelCameraFollow.DefaultStepTweenDurationSeconds;

            for (var row = 1; row <= 10; row++)
            {
                trail.TryAdvanceTo(new GridCoordinate(row, Width / 2));
                follow.AdvanceStepTween(TunnelCameraFollow.DefaultStepTweenDurationSeconds);
            }

            trail.TryAdvanceTo(new GridCoordinate(9, Width / 2)); // шаг НАЗАД на уже посещённую плиту
            follow.AdvanceStepTween(TunnelCameraFollow.DefaultStepTweenDurationSeconds);

            GameObject cameraObject = null;
            try
            {
                var camera = (cameraObject = CreateCamera("TestCamera_StepBack", follow, vFov)).GetComponent<Camera>();
                var playerWorld = projection.ToWorldPosition(trail.CurrentPosition); // row=9 теперь
                var viewport = camera.WorldToViewportPoint(playerWorld);

                Assert.AreEqual(AnchorViewportY, viewport.y, 0.03f, "якорь должен отработать и для шага назад, не только вперёд");
            }
            finally
            {
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                follow.Dispose();
            }
        }

        [Test]
        public void SteadyState_AtDefaultAnchor_AtLeastTwoRowsBehindPlayerRemainVisible()
        {
            // Issue #153 требовал минимум 4 ряда позади. Замер (см. PR): при
            // НЕИЗМЕННЫХ Pitch=50°/HeightOffset.y=6/DesiredVisibleTiles=4
            // (защищены задачей от изменения без явного обоснования)
            // достижимый максимум — 2 целых ряда, дальше плитки уходят ЗА
            // нижний край кадра — геометрическое ограничение самой проекции
            // (то же ограничение существовало и ДО якоря, при жёстко зашитой
            // TrailingRowsBehindPlayer=3), не недоработка этой задачи.
            // Альтернативы (обе меняют видимую геометрию за пределами
            // точечного скоупа этой задачи, см. PR): H=9 + DesiredVisibleTiles=6
            // даёт 4 ряда, либо Pitch≈25-30° даёт 4 ряда ценой неба в кадре.
            // Тест — регрессионный барьер на уже достигнутых 2.
            var (trail, projection, follow, vFov) = CreateAnchoredSteadyState();

            for (var row = 1; row <= 10; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, Width / 2));
            follow.SnapToTarget();

            GameObject cameraObject = null;
            try
            {
                var camera = (cameraObject = CreateCamera("TestCamera_RowsBehind", follow, vFov)).GetComponent<Camera>();

                var visibleRowsBehind = 0;
                for (var offset = 1; offset <= 8; offset++)
                {
                    var row = trail.CurrentPosition.Row - offset;
                    var worldPosition = projection.ToWorldPosition(new GridCoordinate(row, Width / 2));
                    var viewport = camera.WorldToViewportPoint(worldPosition);
                    if (viewport.z > 0f && viewport.y >= 0f && viewport.y <= 1f) visibleRowsBehind = offset;
                    else break;
                }

                Assert.GreaterOrEqual(visibleRowsBehind, 2, "минимум 2 ряда позади должны оставаться в кадре — см. doc-комментарий теста насчёт геометрического предела в 4");
            }
            finally
            {
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                follow.Dispose();
            }
        }

        [Test]
        public void MaterializedRowsAheadOfPlayer_StillAllLandInFrame_WithAnchorEnabled()
        {
            // playerRow..playerRow+8 (RowsAheadOfPlayer=8, TunnelGridReveal/
            // SegmentRowProvider) — видимость вперёд не должна деградировать
            // от включения якоря (issue #153, acceptance).
            var (trail, projection, follow, vFov) = CreateAnchoredSteadyState();

            for (var row = 1; row <= 10; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, Width / 2));
            follow.SnapToTarget();

            GameObject cameraObject = null;
            try
            {
                var camera = (cameraObject = CreateCamera("TestCamera_ForwardVisibility", follow, vFov)).GetComponent<Camera>();

                for (var offset = 0; offset <= 8; offset++)
                {
                    var row = trail.CurrentPosition.Row + offset;
                    var worldPosition = projection.ToWorldPosition(new GridCoordinate(row, Width / 2));
                    var viewport = camera.WorldToViewportPoint(worldPosition);

                    Assert.Greater(viewport.z, 0f, $"ряд +{offset} должен быть перед камерой");
                    Assert.GreaterOrEqual(viewport.y, 0f, $"ряд +{offset} не должен быть за нижним краем");
                    Assert.LessOrEqual(viewport.y, 1f, $"ряд +{offset} не должен быть за верхним краем");
                }
            }
            finally
            {
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                follow.Dispose();
            }
        }

        [Test]
        public void IntroThenFirstSixMoves_PlayerTileStaysInFrameThroughout()
        {
            // Интро-твин (#140/#144) не должен сломаться этой задачей —
            // прогоняем первые 5-6 ходов от самого старта забега (интро ещё
            // играет часть этого времени) и проверяем WorldToViewportPoint
            // на каждом шаге, не только на устоявшемся хвосте.
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, Width / 2));
            var projection = new WorldGridProjection(TileSize, Width);
            var follow = new TunnelCameraFollow(trail, projection, HeightOffset);
            follow.ConfirmRun();

            GameObject cameraObject = null;
            try
            {
                cameraObject = new GameObject("TestCamera_IntroThenMoves");
                var camera = cameraObject.AddComponent<Camera>();
                camera.aspect = PortraitAspect;

                for (var row = 1; row <= 6; row++)
                {
                    follow.AdvanceIntroTween(0.05f);

                    var vFov = ComputeSteadyVerticalFov(projection, follow.CurrentPitchDegrees);
                    var anchorTrailingDistance = TunnelCameraAnchor.ComputeTrailingDistanceForAnchor(
                        HeightOffset.y, follow.CurrentPitchDegrees, vFov, AnchorViewportY);
                    follow.TrailingDistance = anchorTrailingDistance;

                    trail.TryAdvanceTo(new GridCoordinate(row, Width / 2));
                    follow.AdvanceStepTween(0.05f);

                    camera.transform.SetPositionAndRotation(follow.CurrentPosition, follow.CurrentRotation);
                    camera.fieldOfView = vFov > 0f ? vFov : 60f;

                    var playerWorld = projection.ToWorldPosition(trail.CurrentPosition);
                    var viewport = camera.WorldToViewportPoint(playerWorld);

                    Assert.Greater(viewport.z, 0f, $"ход {row}: плитка игрока должна быть перед камерой");
                    Assert.GreaterOrEqual(viewport.y, 0f, $"ход {row}: плитка игрока не должна быть за нижним краем");
                    Assert.LessOrEqual(viewport.y, 1f, $"ход {row}: плитка игрока не должна быть за верхним краем");
                }
            }
            finally
            {
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                follow.Dispose();
            }
        }
    }
}
