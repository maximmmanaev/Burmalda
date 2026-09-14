namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Задача «раскрытие опасности при примеривании»: "Параметры
    /// (длительность раскрытия, сила вибрации) — в дебаг-панель" — mutable
    /// static, тот же принцип, что <c>Core.TunnelObstacleGenerator</c>'s
    /// <c>*Share</c>-поля/<c>Generation.SegmentSelector.TierWindow*</c>:
    /// точный баланс подбирает владелец на устройстве
    /// (<see cref="TrapDensityDebugPanel"/>), не агент вслепую. Прочитано и
    /// применено в <see cref="TrapRevealController.PlayRevealVibration"/> —
    /// см. её doc-комментарий насчёт того, как именно эти два параметра
    /// используются поверх <see cref="UnityEngine.Handheld.Vibrate"/> (нет
    /// нативного контроля длительности/амплитуды).
    /// </summary>
    public static class TrapRevealFeedback
    {
        /// <summary>
        /// Сила вибрации, 0..1. 0 — вибрация выключена совсем (единственный
        /// прямой рычаг, доступный без платформенного кода).
        ///
        /// <b>Issue #264 (2026-09-14, «снизить силу и длительность
        /// оставшейся вибрации»):</b> было 0.6 — владелец прямо попросил
        /// сделать короче и слабее, НЕ убирать совсем (в отличие от
        /// вибрации распада, см. <see cref="DecayPulseController"/>, убрана
        /// этой же задачей целиком). Новое значение — предположение агента
        /// (не решение владельца про точный баланс), примерно вдвое от
        /// прежнего — тот же mutable static, дебаг-панель, владелец
        /// подбирает точнее на устройстве.
        /// </summary>
        public static float VibrationStrength = 0.3f;

        /// <summary>
        /// Сколько секунд длится вибро-обратная связь на раскрытии (серия
        /// импульсов, см. TrapRevealController). Issue #264: было 0.15с,
        /// снижено — та же логика, что у <see cref="VibrationStrength"/>.
        /// </summary>
        public static float VibrationDurationSeconds = 0.08f;
    }
}
