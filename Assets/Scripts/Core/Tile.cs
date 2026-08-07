namespace Burmalda.Core
{
    /// <summary>
    /// Плита сетки тоннеля (PRD 4.1). Хранит координату и состояние распада
    /// (PRD 4.1/16) — само тиканье распада и его тайминг реализует
    /// Burmalda.Decay.TrailDecaySystem, Tile только хранит и применяет результат.
    /// Также хранит статичное препятствие (PRD 4.2, issue #9 — заблокированная
    /// плита) — постоянное, не тикает и не связано с распадом. Остальной тип
    /// плиты (яма/лава/валюта/артефакт) добавится сюда же по мере Traps.
    /// </summary>
    public sealed class Tile
    {
        public Tile(GridCoordinate coordinate)
        {
            Coordinate = coordinate;
        }

        public GridCoordinate Coordinate { get; }

        /// <summary>Плита разрушена распадом — по ней больше нельзя пройти.</summary>
        public bool IsDestroyed { get; private set; }

        /// <summary>
        /// Плита — статичное препятствие (PRD 4.2: заблокированная плита,
        /// видна заранее, обходится). Постоянное состояние, не связано с
        /// распадом — заблокированная плита никогда не была пройдена, значит
        /// и не может начать распадаться.
        /// </summary>
        public bool IsBlocked { get; private set; }

        /// <summary>Помечает плиту как непроходимое статичное препятствие. Повторные вызовы — не-op.</summary>
        public void MarkBlocked()
        {
            IsBlocked = true;
        }

        /// <summary>
        /// Плита — смертельная статичная ловушка (яма/лава, PRD 4.2). Null —
        /// плита не является такой ловушкой.
        /// </summary>
        public LethalTrapType? LethalTrap { get; private set; }

        /// <summary>Помечает плиту как смертельную ловушку заданного типа. Повторные вызовы сохраняют первый тип.</summary>
        public void MarkLethalTrap(LethalTrapType trapType)
        {
            if (LethalTrap.HasValue) return;
            LethalTrap = trapType;
        }

        /// <summary>Накопленное время распада плиты в секундах.</summary>
        public float DecayElapsedSeconds { get; private set; }

        /// <summary>
        /// Порог распада в секундах для этой плиты. Задаётся один раз, когда
        /// плита начинает распадаться (см. <see cref="BeginDecay"/>); null —
        /// плита распаду не подвержена (например, стартовая плита трейла).
        /// </summary>
        public float? DecayThresholdSeconds { get; private set; }

        /// <summary>
        /// Доля пройденного времени распада, 0..1 (для визуальной обратной
        /// связи — напр. цвет плиты, как в legacy/burmolda_demo.html, draw()).
        /// 0, пока распад не начат.
        /// </summary>
        public float DecayProgress01
        {
            get
            {
                if (!DecayThresholdSeconds.HasValue || DecayThresholdSeconds.Value <= 0f) return 0f;
                var ratio = DecayElapsedSeconds / DecayThresholdSeconds.Value;
                if (ratio < 0f) return 0f;
                return ratio > 1f ? 1f : ratio;
            }
        }

        /// <summary>Запускает распад плиты с заданным порогом. Повторные вызовы игнорируются.</summary>
        public void BeginDecay(float thresholdSeconds)
        {
            if (DecayThresholdSeconds.HasValue) return;
            DecayThresholdSeconds = thresholdSeconds;
        }

        /// <summary>
        /// Продвигает накопленное время распада на <paramref name="deltaSeconds"/>;
        /// разрушает плиту при достижении порога. Не действует, пока распад не
        /// начат (<see cref="BeginDecay"/>) и после того, как плита уже разрушена.
        /// </summary>
        public void AdvanceDecay(float deltaSeconds)
        {
            if (IsDestroyed || !DecayThresholdSeconds.HasValue || deltaSeconds <= 0f) return;

            DecayElapsedSeconds += deltaSeconds;
            if (DecayElapsedSeconds >= DecayThresholdSeconds.Value)
            {
                IsDestroyed = true;
            }
        }
    }
}
