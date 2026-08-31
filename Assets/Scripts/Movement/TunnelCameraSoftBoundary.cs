using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Issue #158 (продолжение #155): мягкая граница жёсткого клампа
    /// (<see cref="TunnelCameraFollow.AdvanceContinuousAnchorFollow"/>,
    /// issue A.3). Диагноз владельца продукта: пока плитка игрока внутри
    /// полосы, камера расслаблена (сила коррекции 0), а на самой границе
    /// жёсткий <c>Mathf.Clamp</c> дёргает её мгновенно — отсюда толчки.
    ///
    /// Здесь — дополнительное СЛАГАЕМОЕ, применяемое ПЕРЕД хард-клампом:
    /// сила коррекции (тянет дистанцию камера-игрок обратно к
    /// <paramref name="targetDistance"><c>targetDistance</c></paramref>—
    /// точке покоя anchor) равна нулю, пока доля пройденного расстояния до
    /// ближайшего края не превысила <paramref name="startFraction"/> (по
    /// умолчанию 0.6 — см. <see cref="DefaultStartFraction"/>), дальше
    /// нарастает по SmoothStep до максимума ровно на границе. Хард-кламп в
    /// <see cref="TunnelCameraFollow"/> НЕ убран и не ослаблен — эта
    /// коррекция только гасит скорость подхода к границе, оставляя сам
    /// хард-кламп страховкой на случай, если мягкая коррекция не успела
    /// (резкий скачок скорости за один кадр) — инвариант C (issue A.3,
    /// переформулирован задачей 1 как двусторонняя полоса) остаётся
    /// абсолютным, не тюнинговым.
    ///
    /// <b>Задача 1 (владелец продукта подтвердил на устройстве): полоса
    /// стала двусторонней</b> — <c>[anchor-backTolerance, anchor+tolerance]</c>
    /// вместо прежней <c>[anchor, anchor+tolerance]</c>. Раньше нижний край
    /// (anchor) совпадал с точкой покоя, и симметричная коррекция была
    /// прямо отклонена (см. историю) — коррекция на нижнем крае была бы
    /// ненулевой в состоянии покоя, ломая инвариант B. Теперь точка покоя
    /// (<paramref name="targetDistance"/>) — ОТДЕЛЬНЫЙ параметр СТРОГО
    /// ВНУТРИ полосы (не на её краю) — коррекция считается независимо для
    /// каждой половины (<c>[minDistance, targetDistance]</c> и
    /// <c>[targetDistance, maxDistance]</c>), с нулём ровно в
    /// <paramref name="targetDistance"/> — инвариант B по-прежнему
    /// достижим, только теперь обе стороны, а не одна, могут иметь
    /// ненулевую (но каждая — свою, от своей ширины) коррекцию.
    ///
    /// Чистая математика без зависимости от Camera/сцены — как и
    /// <see cref="TunnelCameraAnchor"/>/<see cref="TunnelCameraFraming"/> —
    /// тестируется напрямую (<see cref="Tests.TunnelCameraSoftBoundaryTests"/>).
    /// </summary>
    public static class TunnelCameraSoftBoundary
    {
        /// <summary>Точка начала нарастания, доля расстояния от target к каждому краю (своя для каждой стороны) — единственный параметр, настраиваемый из дебаг-панели (issue #158).</summary>
        public const float DefaultStartFraction = 0.6f;

        // Скорость мягкой коррекции у самого края (1/с, умножается на
        // остаток от target) — внутренняя тюнинг-константа. НЕ выведена в
        // дебаг-панель: issue #158 явно просит настраиваемой только точку
        // начала нарастания, не саму кривую/её силу.
        private const float MaxCorrectionRatePerSecond = 15f;

        /// <summary>
        /// Дополнительная поправка к "живой" Z-позиции трейлинг-точки (та
        /// же величина и знак, что и у слагаемых 1/2 в
        /// <see cref="TunnelCameraFollow.AdvanceContinuousAnchorFollow"/> —
        /// прибавляется к <c>_continuousCameraZ</c> напрямую) — ноль, пока
        /// <paramref name="distance"/> не отошла от
        /// <paramref name="targetDistance"/> дальше, чем
        /// <paramref name="startFraction"/> доли расстояния до
        /// соответствующего края (<paramref name="maxDistance"/> сверху,
        /// <paramref name="minDistance"/> снизу); растущая по SmoothStep
        /// дальше, вплоть до максимума на самом крае. Знак — В СТОРОНУ
        /// target (положительный сверху, отрицательный снизу — см.
        /// <see cref="TunnelCameraFollow.AdvanceContinuousAnchorFollow"/>,
        /// где положительная прибавка к <c>_continuousCameraZ</c> УМЕНЬШАЕТ
        /// дистанцию камера-игрок). Не NaN/Inf на вырожденных входах (нулевая/
        /// отрицательная ширина стороны, distance далеко за любым краем,
        /// startFraction=1) — безопасна вызывать каждый кадр с любыми
        /// значениями из дебаг-панели.
        /// </summary>
        public static float ComputeCorrection(float distance, float targetDistance, float minDistance, float maxDistance, float deltaSeconds, float startFraction)
        {
            if (distance > targetDistance)
                return ComputeSideMagnitude(distance - targetDistance, maxDistance - targetDistance, deltaSeconds, startFraction);

            if (distance < targetDistance)
                return -ComputeSideMagnitude(targetDistance - distance, targetDistance - minDistance, deltaSeconds, startFraction);

            return 0f; // distance == targetDistance — инвариант B, состояние покоя
        }

        /// <summary>Общая формула одной стороны (верх ИЛИ низ) — расстояние от target и ширина ДО этой стороны, всегда неотрицательные величины.</summary>
        private static float ComputeSideMagnitude(float residualFromTarget, float sideWidth, float deltaSeconds, float startFraction)
        {
            if (sideWidth <= 0f) return 0f;

            var t = residualFromTarget / sideWidth;
            if (t <= startFraction) return 0f;

            var rampWidth = Mathf.Max(1f - startFraction, 0.0001f); // защита от деления на ноль при startFraction=1
            var rampT = Mathf.Clamp01((t - startFraction) / rampWidth);
            var strength = rampT * rampT * (3f - 2f * rampT); // SmoothStep 0..1 (issue #158: "начать со SmoothStep")

            return residualFromTarget * strength * MaxCorrectionRatePerSecond * deltaSeconds;
        }
    }
}
