using Burmalda.Core;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    /// <summary>
    /// Хотфикс камеры: угол наклона без неба в кадре (Rotation X = 35°) +
    /// устойчивая ширина обзора тоннеля в <see cref="TunnelCameraFraming.DesiredVisibleTiles"/>
    /// плитки на любом аспекте экрана (см. <see cref="TunnelCameraFraming"/>).
    /// Проверяет через реальный <see cref="Camera"/> и
    /// <see cref="Camera.WorldToViewportPoint"/> — world-позиции берутся из
    /// фактически заспавненной сетки (<see cref="WorldGridProjection"/>), не
    /// выдуманы. Batchmode-совместимо (EditMode, без сцены/Play Mode).
    /// </summary>
    public class TunnelCameraViewportFramingTests
    {
        private const float TileSize = 1f;
        private const int Width = 5;

        [Test]
        public void SteadyStateReferenceRow_LeftAndRightEdges_LandNearFrameEdgesOnAnyAspect()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, Width / 2));
            var projection = new WorldGridProjection(TileSize, Width);
            var heightOffset = new Vector3(0f, 4f, -1f); // Height Offset со сцены — не трогается

            // Шаг 1: дистанция от стартовой позиции камеры до первой
            // заспавненной плиты (ряд 0) — вход для TunnelCameraFraming.
            var atStart = new TunnelCameraFollow(trail, projection, heightOffset);
            var cameraStartPosition = atStart.TargetPosition;
            var firstRowWorldZ = projection.ToWorldPosition(new GridCoordinate(0, Width / 2)).z;
            var groundDistanceToRow = Mathf.Abs(firstRowWorldZ - cameraStartPosition.z);
            atStart.Dispose();

            // Шаг 4: желаемый горизонтальный FOV из реальной геометрии.
            var desiredHorizontalFovDeg = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(
                heightOffset.y, groundDistanceToRow, projection.TileSize);

            // Продвигаем трейл в установившийся режим (P >= TrailingRowsBehindPlayer).
            for (var row = 1; row <= 10; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, Width / 2));
            var follow = new TunnelCameraFollow(trail, projection, heightOffset);

            // Шаг 2: pitch >= 30° — условие отсутствия неба (vFOV=60° -> vFOV/2=30°).
            var pitch = follow.TargetRotation.eulerAngles.x;
            Assert.GreaterOrEqual(pitch, 30f);

            // Опорный ряд — 2 плитки позади текущей позиции игрока (реально
            // заспавнен трейлом). Реальные world-позиции крайних плиток.
            var referenceRow = trail.CurrentPosition.Row - 2;
            var leftEdgeX = projection.ToWorldPosition(new GridCoordinate(referenceRow, 0)).x - TileSize / 2f;
            var rightEdgeX = projection.ToWorldPosition(new GridCoordinate(referenceRow, Width - 1)).x + TileSize / 2f;
            var rowWorldZ = projection.ToWorldPosition(new GridCoordinate(referenceRow, 0)).z;

            GameObject cameraObject = null;
            try
            {
                cameraObject = new GameObject("TestCamera_TunnelCameraViewportFraming");
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(follow.CurrentPosition, follow.CurrentRotation);

                // Шаг 3: устойчивость на разных аспектах — проверяем портрет,
                // ландшафт и квадрат одним и тем же desiredHorizontalFovDeg.
                foreach (var aspect in new[] { 0.5625f, 1.7778f, 1f })
                {
                    camera.aspect = aspect;
                    camera.fieldOfView = TunnelCameraFraming.ComputeVerticalFovDegrees(desiredHorizontalFovDeg, aspect);

                    var leftViewport = camera.WorldToViewportPoint(new Vector3(leftEdgeX, 0f, rowWorldZ));
                    var rightViewport = camera.WorldToViewportPoint(new Vector3(rightEdgeX, 0f, rowWorldZ));

                    Assert.AreEqual(0.05f, leftViewport.x, 0.03f, $"левый край, aspect={aspect}");
                    Assert.AreEqual(0.95f, rightViewport.x, 0.03f, $"правый край, aspect={aspect}");
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
