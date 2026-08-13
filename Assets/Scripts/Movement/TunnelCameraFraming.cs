using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Хотфикс камеры: угол наклона (Rotation X, см. <see cref="TunnelCameraFollow"/>)
    /// без неба в кадре + устойчивая ширина обзора тоннеля в
    /// <see cref="DesiredVisibleTiles"/> плиток на любом аспекте экрана.
    /// Чистая математика, без зависимости от Camera/сцены — тестируется в
    /// EditMode без сцены. Применяется в <see cref="TunnelCameraController"/>.
    /// </summary>
    public static class TunnelCameraFraming
    {
        /// <summary>Желаемая ширина видимой части тоннеля, в плитках.</summary>
        public const float DesiredVisibleTiles = 4f;

        /// <summary>
        /// Горизонтальный FOV (градусы), при котором на дистанции
        /// R = sqrt(cameraHeight² + groundDistanceToRow²) в кадре по ширине
        /// помещается ровно <see cref="DesiredVisibleTiles"/> плиток.
        /// </summary>
        public static float ComputeDesiredHorizontalFovDegrees(float cameraHeight, float groundDistanceToRow, float tileSize)
        {
            var halfWidth = DesiredVisibleTiles * tileSize / 2f;
            var r = Mathf.Sqrt(cameraHeight * cameraHeight + groundDistanceToRow * groundDistanceToRow);
            return 2f * Mathf.Atan(halfWidth / r) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Вертикальный FOV (то, что принимает Camera.fieldOfView),
        /// пересчитанный из желаемого горизонтального FOV под текущий
        /// aspect (Screen.width/Screen.height) — держит горизонтальный FOV
        /// (а значит и ширину обзора тоннеля) стабильным при смене
        /// разрешения/ориентации экрана.
        /// </summary>
        public static float ComputeVerticalFovDegrees(float desiredHorizontalFovDegrees, float aspect)
        {
            var vFovRad = 2f * Mathf.Atan(
                Mathf.Tan(desiredHorizontalFovDegrees * 0.5f * Mathf.Deg2Rad) / aspect);
            return vFovRad * Mathf.Rad2Deg;
        }
    }
}
