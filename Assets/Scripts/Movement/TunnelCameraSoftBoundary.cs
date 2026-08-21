using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Issue #158 (продолжение #155): мягкая граница жёсткого клампа
    /// (<see cref="TunnelCameraFollow.AdvanceContinuousAnchorFollow"/>,
    /// issue A.3). Диагноз владельца продукта: пока плитка игрока внутри
    /// полосы <c>[anchor, anchor+tolerance]</c>, камера расслаблена (сила
    /// коррекции 0), а на самой границе жёсткий <c>Mathf.Clamp</c> дёргает
    /// её мгновенно — отсюда толчки.
    ///
    /// Здесь — дополнительное СЛАГАЕМОЕ, применяемое ПЕРЕД хард-клампом:
    /// сила коррекции (тянет дистанцию камера-игрок обратно к anchor) равна
    /// нулю, пока доля пройденной полосы <c>t=(distance-min)/(max-min)</c>
    /// не превысила <paramref name="startFraction"/> (по умолчанию 0.6 —
    /// см. <see cref="DefaultStartFraction"/>), дальше нарастает по
    /// SmoothStep до максимума ровно на границе (t=1). Хард-кламп в
    /// <see cref="TunnelCameraFollow"/> НЕ убран и не ослаблен — эта
    /// коррекция только гасит скорость подхода к границе, оставляя сам
    /// хард-кламп страховкой на случай, если мягкая коррекция не успела
    /// (резкий скачок скорости за один кадр) — инвариант C (issue A.3)
    /// остаётся абсолютным, не тюнинговым.
    ///
    /// Асимметрично намеренно: применяется только к ВЕРХНЕМУ краю (подход к
    /// anchor+tolerance при быстром движении вперёд — жалоба владельца).
    /// Нижний край (anchor) — это одновременно устоявшаяся точка покоя
    /// (см. <see cref="TunnelCameraFollow.AdvanceContinuousAnchorFollow"/>,
    /// слагаемое 2, feedback тянет РОВНО к <c>minTrailingDistance</c>) —
    /// сила коррекции здесь обязана быть точно нулём при distance==min,
    /// иначе инвариант B (камера не двигается без хода, см.
    /// <see cref="Tests.TunnelCameraContinuousFollowTests.Rest_NoNewStepAfterSettling_CameraStaysAbsolutelyStill"/>)
    /// был бы недостижим. Симметричный (двусторонний от геометрического
    /// центра полосы) вариант ломает именно этот инвариант — рассмотрен и
    /// отклонён.
    ///
    /// Чистая математика без зависимости от Camera/сцены — как и
    /// <see cref="TunnelCameraAnchor"/>/<see cref="TunnelCameraFraming"/> —
    /// тестируется напрямую (<see cref="Tests.TunnelCameraSoftBoundaryTests"/>).
    /// </summary>
    public static class TunnelCameraSoftBoundary
    {
        /// <summary>Точка начала нарастания, доля полосы от anchor (0) к anchor+tolerance (1) — единственный параметр, настраиваемый из дебаг-панели (issue #158).</summary>
        public const float DefaultStartFraction = 0.6f;

        // Скорость мягкой коррекции у самого края (1/с, умножается на
        // остаток над anchor) — внутренняя тюнинг-константа. НЕ выведена в
        // дебаг-панель: issue #158 явно просит настраиваемой только точку
        // начала нарастания, не саму кривую/её силу.
        private const float MaxCorrectionRatePerSecond = 15f;

        /// <summary>
        /// Дополнительная поправка к "живой" Z-позиции трейлинг-точки
        /// (та же величина и знак, что и у слагаемых 1/2 в
        /// <see cref="TunnelCameraFollow.AdvanceContinuousAnchorFollow"/> —
        /// прибавляется к <c>_continuousCameraZ</c> напрямую) — ноль, пока
        /// <paramref name="distance"/> не превысила <paramref name="startFraction"/>
        /// доли полосы <c>[minDistance, maxDistance]</c>; положительная и
        /// растущая по SmoothStep дальше, вплоть до максимума на
        /// <paramref name="maxDistance"/>. Не NaN/Inf на вырожденных входах
        /// (полоса нулевой/отрицательной ширины, distance ниже anchor,
        /// distance за maxDistance, startFraction=1) — безопасна вызывать
        /// каждый кадр с любыми значениями из дебаг-панели.
        /// </summary>
        public static float ComputeCorrection(float distance, float minDistance, float maxDistance, float deltaSeconds, float startFraction)
        {
            var bandWidth = maxDistance - minDistance;
            if (bandWidth <= 0f) return 0f;

            var t = (distance - minDistance) / bandWidth;
            if (t <= startFraction) return 0f;

            var rampWidth = Mathf.Max(1f - startFraction, 0.0001f); // защита от деления на ноль при startFraction=1
            var rampT = Mathf.Clamp01((t - startFraction) / rampWidth);
            var strength = rampT * rampT * (3f - 2f * rampT); // SmoothStep 0..1 (issue #158: "начать со SmoothStep")

            var residualAboveAnchor = distance - minDistance; // > 0 здесь, т.к. t > startFraction >= 0
            return residualAboveAnchor * strength * MaxCorrectionRatePerSecond * deltaSeconds;
        }
    }
}
