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
        /// <summary>Сила вибрации, 0..1. 0 — вибрация выключена совсем (единственный прямой рычаг, доступный без платформенного кода).</summary>
        public static float VibrationStrength = 0.6f;

        /// <summary>Сколько секунд длится вибро-обратная связь на раскрытии (серия импульсов, см. TrapRevealController).</summary>
        public static float VibrationDurationSeconds = 0.15f;
    }
}
