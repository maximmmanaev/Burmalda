namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Задача «разрушение плиты» (владелец, 2026-09-01): параметры анимации
    /// обрушения и пульсации-предупреждения — mutable static, тот же
    /// принцип, что <see cref="TrapRevealFeedback"/> (точный баланс подбирает
    /// владелец на устройстве через <see cref="TrapDensityDebugPanel"/>, не
    /// агент вслепую).
    /// </summary>
    public static class DecayCollapseFeedback
    {
        /// <summary>
        /// Длительность анимации обрушения (проседание + крен + партиклы
        /// осыпи), секунды. Задача прямо просит "стартовое значение
        /// 0.25–0.4 с" — середина диапазона.
        /// </summary>
        public static float CollapseDurationSeconds = 0.32f;

        /// <summary>
        /// Сила вибро-пульсации в последней трети распада (см.
        /// <see cref="DecayPulseController"/>), 0..1. 0 — вибрация
        /// выключена совсем, тот же рычаг, что <see cref="TrapRevealFeedback.VibrationStrength"/>.
        /// </summary>
        public static float PulseVibrationStrength = 0.5f;
    }
}
