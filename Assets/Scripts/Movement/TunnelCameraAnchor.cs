using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Issue #153, тумблер 2: якорь камеры по вьюпорту вместо трейлинга в
    /// мировых рядах (<see cref="TunnelCameraFollow.TrailingRowsBehindPlayer"/>).
    /// Мировой офсет не учитывает геометрию проекции — доля экрана, на
    /// которой оказывается плитка игрока, плывёт от Pitch/HeightOffset.
    /// Здесь — обратная задача: по желаемой доле высоты кадра решается, на
    /// каком расстоянии от камеры плитка игрока попадёт ровно в эту долю.
    /// Чистая математика без зависимости от Camera/сцены — как и
    /// <see cref="TunnelCameraFraming"/> — нужна на старте забега, когда
    /// сцены ещё нет, и в EditMode-тестах.
    ///
    /// Не участвует в калибровке ширины
    /// (<see cref="TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees"/>) —
    /// та по-прежнему считается от фиксированного
    /// <see cref="TunnelCameraFollow.TrailingRowsBehindPlayer"/> (см.
    /// док-комментарий <see cref="TunnelCameraController"/>): иначе
    /// дистанция-от-якоря входила бы в вычисление FOV, а FOV — обратно в
    /// дистанцию-от-якоря (через vFOV), зацикливая калибровку без явной
    /// необходимости.
    /// </summary>
    public static class TunnelCameraAnchor
    {
        /// <summary>
        /// Дистанция по Z (мировые единицы, НЕ ряды) от камеры до плитки
        /// игрока, при которой <c>Camera.WorldToViewportPoint</c> даёт РОВНО
        /// <paramref name="anchorViewportY"/> для этой плитки.
        ///
        /// Вывод (камера без yaw/roll — <c>Quaternion.Euler(pitch,0,0)</c>,
        /// та же геометрия, что и в <see cref="TunnelCameraFraming"/>): для
        /// камеры на высоте <paramref name="cameraHeightY"/> над землёй и
        /// точки на дистанции D впереди по оси взгляда,
        /// forward=(0,-sinθ,cosθ), up=(0,cosθ,sinθ):
        /// <code>
        ///   depth(D) = H·sinθ + D·cosθ   (проекция вектора камера→точка на forward)
        ///   vert(D)  = -H·cosθ + D·sinθ  (проекция на up)
        /// </code>
        /// Перспективная проекция без yaw/roll: <c>viewportY = 0.5 + 0.5·(vert/depth)/tan(vFOV/2)</c>.
        /// Решая относительно D при заданном целевом viewportY:
        /// <code>
        ///   k = (anchorViewportY-0.5)·2·tan(vFOV/2)
        ///   D = H·(k·sinθ + cosθ) / (sinθ - k·cosθ)
        /// </code>
        /// </summary>
        public static float ComputeTrailingDistanceForAnchor(float cameraHeightY, float pitchDegrees, float verticalFovDegrees, float anchorViewportY)
        {
            var pitchRad = pitchDegrees * Mathf.Deg2Rad;
            var vFovHalfRad = verticalFovDegrees * 0.5f * Mathf.Deg2Rad;
            var sin = Mathf.Sin(pitchRad);
            var cos = Mathf.Cos(pitchRad);
            var k = (anchorViewportY - 0.5f) * 2f * Mathf.Tan(vFovHalfRad);

            var denominator = sin - k * cos;
            // Вырожденный случай (почти горизонтальный взгляд или anchor≈крайние
            // значения) — безопасный фолбэк вместо NaN/бесконечности, на
            // практике недостижим при реальных Pitch/anchor этого проекта.
            if (Mathf.Abs(denominator) < 1e-4f) return cameraHeightY;

            return cameraHeightY * (k * sin + cos) / denominator;
        }
    }
}
