namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Снимок состояния плиты, нужный только для выбора debug-цвета
    /// (<see cref="TileDebugColor"/>) — намеренно не весь <c>Core.Tile</c>,
    /// чтобы маппинг состояние→цвет оставался чистой функцией, тестируемой
    /// без сцены и без Unity API рендеринга.
    /// </summary>
    public readonly struct TileVisualState
    {
        public TileVisualState(bool isStart, bool isCurrentPosition, bool isDestroyed, bool isBlocked, float decayProgress01)
        {
            IsStart = isStart;
            IsCurrentPosition = isCurrentPosition;
            IsDestroyed = isDestroyed;
            IsBlocked = isBlocked;
            DecayProgress01 = decayProgress01;
        }

        /// <summary>Стартовая плита трейла (индекс 0) — распаду не подвержена.</summary>
        public bool IsStart { get; }

        /// <summary>Плита, на которой сейчас стоит игрок (последняя в трейле).</summary>
        public bool IsCurrentPosition { get; }

        /// <summary>Плита разрушена распадом (см. <c>Core.Tile.IsDestroyed</c>).</summary>
        public bool IsDestroyed { get; }

        /// <summary>Плита — статичное препятствие, PRD 4.2 (см. <c>Core.Tile.IsBlocked</c>).</summary>
        public bool IsBlocked { get; }

        /// <summary>Доля пройденного времени распада, 0..1 (см. <c>Core.Tile.DecayProgress01</c>).</summary>
        public float DecayProgress01 { get; }
    }
}
