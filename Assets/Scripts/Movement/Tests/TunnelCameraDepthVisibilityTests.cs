using Burmalda.Core;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    /// <summary>
    /// Issue #109, пункт B.1: <c>TunnelGridReveal.RowsAheadOfPlayer</c>/
    /// <c>SegmentRowProvider.RowsAheadOfPlayer</c> = 8 сами по себе значат
    /// только "плита материализована" — не "плита видна в кадре". Этот тест
    /// замеряет реальный <see cref="Camera.WorldToViewportPoint"/> на
    /// актуальной устоявшейся геометрии (<see cref="TunnelCameraFollow.DefaultPitchDegrees"/>=50°,
    /// <c>HeightOffset</c>=(0,6,0) со сцены, <see cref="TunnelCameraFollow.TrailingRowsBehindPlayer"/>=3)
    /// для каждого ряда от текущей позиции игрока до +8 рядов вперёд —
    /// ровно диапазон материализации <c>RowsAheadOfPlayer</c>. Портретный
    /// аспект (0.5625) — единственная поддерживаемая ориентация (см.
    /// <see cref="TunnelCameraViewportFramingTests"/>).
    ///
    /// Регрессия ловится автоматически: если кто-то поменяет Pitch/HeightOffset/
    /// TrailingRowsBehindPlayer так, что дальние из 8 материализованных рядов
    /// уйдут за верхний край кадра, этот тест покраснеет — константа
    /// RowsAheadOfPlayer сама по себе (без этого теста) такую регрессию не ловит.
    /// </summary>
    public class TunnelCameraDepthVisibilityTests
    {
        private const float TileSize = 1f;
        private const int Width = 5;
        private const int RowsAheadOfPlayer = 8; // TunnelGridReveal.RowsAheadOfPlayer / SegmentRowProvider.RowsAheadOfPlayer
        private const float PortraitAspect = 0.5625f; // 9:16 — единственная поддерживаемая ориентация

        private static (GridTraceTrail trail, WorldGridProjection projection, Vector3 heightOffset) CreateSteadyStateSetup(int playerRow)
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, Width / 2));
            var projection = new WorldGridProjection(TileSize, Width);
            var heightOffset = new Vector3(0f, 6f, 0f); // HeightOffset со сцены (TunnelCameraController._heightOffset)

            for (var row = 1; row <= playerRow; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, Width / 2));

            return (trail, projection, heightOffset);
        }

        private static TunnelCameraFollow CreateSteadyStateFollow(GridTraceTrail trail, WorldGridProjection projection, Vector3 heightOffset)
        {
            var follow = new TunnelCameraFollow(trail, projection, heightOffset);
            follow.ConfirmRun();
            follow.AdvanceIntroTween(TunnelCameraFollow.TweenDurationSeconds);
            return follow;
        }

        // Та же чистая функция, что TunnelCameraController.Update() использует каждый кадр.
        private static float ComputeDesiredHorizontalFovDeg(WorldGridProjection projection, Vector3 heightOffset, float pitchDeg)
        {
            var groundDistanceToRow = TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow(
                heightOffset.z, projection.TileSize, TunnelCameraFollow.TrailingRowsBehindPlayer);
            return TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(
                heightOffset.y, groundDistanceToRow, pitchDeg, projection.TileSize);
        }

        [TestCase(0)]
        [TestCase(15)] // произвольный удалённый от старта ряд — дистанция камера-игрок не зависит от Row (см. TunnelCameraViewportFramingTests)
        public void MaterializedRowsAheadOfPlayer_AllLandInFrame(int playerRow)
        {
            var (trail, projection, heightOffset) = CreateSteadyStateSetup(playerRow);
            var follow = CreateSteadyStateFollow(trail, projection, heightOffset);
            var pitch = follow.TargetRotation.eulerAngles.x;
            var desiredHorizontalFovDeg = ComputeDesiredHorizontalFovDeg(projection, heightOffset, pitch);

            GameObject cameraObject = null;
            try
            {
                cameraObject = new GameObject("TestCamera_TunnelCameraDepthVisibility");
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(follow.CurrentPosition, follow.CurrentRotation);
                camera.aspect = PortraitAspect;
                camera.fieldOfView = TunnelCameraFraming.ComputeVerticalFovDegrees(desiredHorizontalFovDeg, PortraitAspect);

                for (var offset = 0; offset <= RowsAheadOfPlayer; offset++)
                {
                    var row = trail.CurrentPosition.Row + offset;
                    var worldPosition = projection.ToWorldPosition(new GridCoordinate(row, Width / 2));
                    var viewport = camera.WorldToViewportPoint(worldPosition);

                    Assert.Greater(viewport.z, 0f, $"ряд +{offset} (row={row}) должен быть перед камерой, не позади");
                    Assert.GreaterOrEqual(viewport.y, 0f, $"ряд +{offset} (row={row}) не должен быть за нижним краем кадра");
                    Assert.LessOrEqual(viewport.y, 1f, $"ряд +{offset} (row={row}) не должен быть за верхним краем кадра — материализован, но не виден игроку");
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
