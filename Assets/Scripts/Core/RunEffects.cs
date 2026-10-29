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

        /// <summary>Скорость времени ловушек и распада: 1 — обычная, 0.5 — Идол «Время», 0 — полная остановка (игрок ходит).</summary>
        float ThreatTimeScale { get; }

        /// <summary>Скорость обрушения рядов Святилища (Тотем «Амик», ур. 3: меньше 1 — медленнее).</summary>
        float CollapseSpeedMultiplier { get; }

        /// <summary>Множитель Маны плит-источников (Тотем «Ваагош», ур. 1).</summary>
        float SourceManaMultiplier { get; }

        /// <summary>Множитель Маны Жил Святилища (Тотем «Ваагош», ур. 2).</summary>
        float VeinManaMultiplier { get; }

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

    /// <summary>Нейтральные эффекты — нет ни одной Реликвии, Идола и Тотема.</summary>
    public sealed class NeutralRunEffects : IRunEffects
    {
        public const float BaseResonanceBonus = 0.5f;

        public float WallCloseSlowdown => 1f;

        public float ThreatTimeScale => 1f;

        public float CollapseSpeedMultiplier => 1f;

        public float SourceManaMultiplier => 1f;

        public float VeinManaMultiplier => 1f;

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
        public float ThreatTimeScale { get; set; } = 1f;
        public float CollapseSpeedMultiplier { get; set; } = 1f;
        public float SourceManaMultiplier { get; set; } = 1f;
        public float VeinManaMultiplier { get; set; } = 1f;
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

    /// <summary>
    /// Слои эффектов (Реликвии, Идол, Тотем): множители перемножаются, «шаги активируют ловушки» — все слои разрешают,
    /// остановка обрушения — любой слой, бонус Маны складывается, поглощение попадания — первый слой, который поглотил.
    /// </summary>
    public sealed class CompositeRunEffects : IRunEffects
    {
        private readonly System.Collections.Generic.IReadOnlyList<IRunEffects> _layers;

        public CompositeRunEffects(System.Collections.Generic.IReadOnlyList<IRunEffects> layers) => _layers = layers;

        private float Product(System.Func<IRunEffects, float> f)
        {
            var v = 1f;
            foreach (var l in _layers) v *= f(l);
            return v;
        }

        public float WallCloseSlowdown => Product(l => l.WallCloseSlowdown);
        public float ThreatTimeScale => Product(l => l.ThreatTimeScale);
        public float CollapseSpeedMultiplier => Product(l => l.CollapseSpeedMultiplier);
        public float SourceManaMultiplier => Product(l => l.SourceManaMultiplier);
        public float VeinManaMultiplier => Product(l => l.VeinManaMultiplier);
        public float ManaMultiplier => Product(l => l.ManaMultiplier);
        public float StreakBonusMultiplier => Product(l => l.StreakBonusMultiplier);
        public float DecayRateMultiplier => Product(l => l.DecayRateMultiplier);
        public float BearTrapEscapeMultiplier => Product(l => l.BearTrapEscapeMultiplier);

        public float ResonanceBonus
        {
            get
            {
                var best = NeutralRunEffects.BaseResonanceBonus;
                foreach (var l in _layers) best = System.Math.Max(best, l.ResonanceBonus);
                return best;
            }
        }

        public bool StepsActivateTraps
        {
            get
            {
                foreach (var l in _layers) if (!l.StepsActivateTraps) return false;
                return true;
            }
        }

        public bool CollapseHeld
        {
            get
            {
                foreach (var l in _layers) if (l.CollapseHeld) return true;
                return false;
            }
        }

        public int BonusManaForStep(int stepNumber)
        {
            var sum = 0;
            foreach (var l in _layers) sum += l.BonusManaForStep(stepNumber);
            return sum;
        }

        public bool TryAbsorbHit(ThreatType threat)
        {
            foreach (var l in _layers) if (l.TryAbsorbHit(threat)) return true;
            return false;
        }
    }

    /// <summary>
    /// Текущие эффекты забега. Слои задают Алтарь (Реликвии), Идол и Тотем через <see cref="SetLayer"/>; по умолчанию —
    /// нейтральные. Присвоение <see cref="Current"/> заменяет все слои одним (удобно тестам).
    /// </summary>
    public static class RunEffects
    {
        public static readonly IRunEffects Neutral = new NeutralRunEffects();

        private static readonly System.Collections.Generic.Dictionary<string, IRunEffects> Layers = new System.Collections.Generic.Dictionary<string, IRunEffects>();
        private static IRunEffects _current = Neutral;

        public static IRunEffects Current
        {
            get => _current;
            set
            {
                Layers.Clear();
                _current = value ?? Neutral;
            }
        }

        /// <summary>Подставляет слой эффектов (null — убирает). Порядок слоёв — порядок первой установки.</summary>
        public static void SetLayer(string key, IRunEffects layer)
        {
            if (layer == null) Layers.Remove(key);
            else Layers[key] = layer;
            Rebuild();
        }

        public static bool HasLayer(string key, IRunEffects layer) => Layers.TryGetValue(key, out var l) && ReferenceEquals(l, layer);

        private static void Rebuild() =>
            _current = Layers.Count == 0 ? Neutral : new CompositeRunEffects(new System.Collections.Generic.List<IRunEffects>(Layers.Values));

        /// <summary>Базовое время ухода с Медвежьего капкана, с [CONFIG: PRD §8.2 № 9 — 1.5 с].</summary>
        public const float BearTrapBaseEscapeSeconds = 1.5f;

        /// <summary>Время ухода с капкана с учётом Реликвий — для будущей ловушки.</summary>
        public static float BearTrapEscapeSeconds() => BearTrapBaseEscapeSeconds * Current.BearTrapEscapeMultiplier;
    }
}
