namespace Burmalda.Core
{
    /// <summary>
    /// Эффекты Реликвий забега, которые читают системы игры (PRD v10 §20.2). Core не знает про Реликвии: системы
    /// спрашивают у <see cref="RunEffects.Current"/>, а состояние билда подставляет Алтарь. Нейтральные значения — «без Реликвий».
    /// Ни один эффект не даёт иммунитета к Лаве и не меняет Бросок Судьбы (инвариант §32).
    /// </summary>
    public interface IRunEffects
    {
        /// <summary>Во сколько раз Давилка и движущиеся Стены закрываются медленнее (Берестяной оберег); 1 — без эффекта.</summary>
        float WallCloseSlowdown { get; }

        /// <summary>Множитель всей Маны забега (Чёрное золото ×1.5).</summary>
        float ManaMultiplier { get; }

        /// <summary>Множитель бонуса Маны за серию чистого прохода (Лешачья тропа ×2).</summary>
        float StreakBonusMultiplier { get; }

        /// <summary>Во сколько раз быстрее распадается пол (Чёрное золото).</summary>
        float DecayRateMultiplier { get; }

        /// <summary>Бонус множителя Святилища за плиту Резонанса (Громовой знак +0.8 вместо +0.5).</summary>
        float ResonanceBonus { get; }

        /// <summary>Во сколько раз больше времени уйти с Медвежьего капкана (Тень Чернобога ×2). Капкана в игре пока нет — крючок.</summary>
        float BearTrapEscapeMultiplier { get; }

        /// <summary>Шаги активируют ловушки; false, пока действует Морок.</summary>
        bool StepsActivateTraps { get; }

        /// <summary>Обрушение Святилища остановлено (Ночь Чернобога).</summary>
        bool CollapseHeld { get; }

        /// <summary>Бонус Маны за шаг номер <paramref name="stepNumber"/> (Веретено: каждая 5-я плита).</summary>
        int BonusManaForStep(int stepNumber);

        /// <summary>
        /// Амулет раз за Ярус переживает попадание угрозы <paramref name="threat"/> (Плат Мокоши — любое, Перунов знак — Огненная
        /// струя). True — попадание поглощено, заряд на этом Ярусе потрачен. От Лавы защиты нет — Лава сюда не передаётся.
        /// </summary>
        bool TryAbsorbHit(ThreatType threat);
    }

    /// <summary>Нейтральные эффекты — нет ни одной Реликвии.</summary>
    public sealed class NeutralRunEffects : IRunEffects
    {
        public const float BaseResonanceBonus = 0.5f;

        public float WallCloseSlowdown => 1f;

        public float ManaMultiplier => 1f;

        public float StreakBonusMultiplier => 1f;

        public float DecayRateMultiplier => 1f;

        public float ResonanceBonus => BaseResonanceBonus;

        public float BearTrapEscapeMultiplier => 1f;

        public bool StepsActivateTraps => true;

        public bool CollapseHeld => false;

        public int BonusManaForStep(int stepNumber) => 0;

        public bool TryAbsorbHit(ThreatType threat) => false;
    }

    /// <summary>Настраиваемые эффекты — для тестов систем, которые читают <see cref="RunEffects.Current"/>.</summary>
    public sealed class ConfigurableRunEffects : IRunEffects
    {
        public float WallCloseSlowdown { get; set; } = 1f;
        public float ManaMultiplier { get; set; } = 1f;
        public float StreakBonusMultiplier { get; set; } = 1f;
        public float DecayRateMultiplier { get; set; } = 1f;
        public float ResonanceBonus { get; set; } = NeutralRunEffects.BaseResonanceBonus;
        public float BearTrapEscapeMultiplier { get; set; } = 1f;
        public bool StepsActivateTraps { get; set; } = true;
        public bool CollapseHeld { get; set; }
        public int SpindleEvery { get; set; }
        public int SpindleBonus { get; set; }
        public System.Func<ThreatType, bool> Absorb { get; set; } = _ => false;

        public int BonusManaForStep(int stepNumber) => SpindleEvery > 0 && stepNumber > 0 && stepNumber % SpindleEvery == 0 ? SpindleBonus : 0;

        public bool TryAbsorbHit(ThreatType threat) => Absorb(threat);
    }

    /// <summary>Текущие эффекты Реликвий забега: подставляет <c>AltarController</c>, по умолчанию — нейтральные.</summary>
    public static class RunEffects
    {
        public static readonly IRunEffects Neutral = new NeutralRunEffects();

        public static IRunEffects Current { get; set; } = Neutral;

        /// <summary>Базовое время ухода с Медвежьего капкана, с [CONFIG: PRD §8.2 № 9 — 1.5 с].</summary>
        public const float BearTrapBaseEscapeSeconds = 1.5f;

        /// <summary>Время ухода с капкана с учётом Реликвий — для будущей ловушки.</summary>
        public static float BearTrapEscapeSeconds() => BearTrapBaseEscapeSeconds * Current.BearTrapEscapeMultiplier;
    }
}
