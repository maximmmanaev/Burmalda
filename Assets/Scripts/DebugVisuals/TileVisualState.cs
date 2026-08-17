using Burmalda.Core;

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
        public TileVisualState(bool isStart, bool isCurrentPosition, bool isDestroyed, bool isBlocked, LethalTrapType? lethalTrap, float decayProgress01, bool isExplosiveTrapTrigger, bool isTimedTrapTrigger, TimedTrapType? activeTimedTrap, bool isBoss = false)
        {
            IsStart = isStart;
            IsCurrentPosition = isCurrentPosition;
            IsDestroyed = isDestroyed;
            IsBlocked = isBlocked;
            LethalTrap = lethalTrap;
            DecayProgress01 = decayProgress01;
            IsExplosiveTrapTrigger = isExplosiveTrapTrigger;
            IsTimedTrapTrigger = isTimedTrapTrigger;
            ActiveTimedTrap = activeTimedTrap;
            IsBoss = isBoss;
        }

        /// <summary>Стартовая плита трейла (индекс 0) — распаду не подвержена.</summary>
        public bool IsStart { get; }

        /// <summary>Плита, на которой сейчас стоит игрок (последняя в трейле).</summary>
        public bool IsCurrentPosition { get; }

        /// <summary>Плита разрушена распадом (см. <c>Core.Tile.IsDestroyed</c>).</summary>
        public bool IsDestroyed { get; }

        /// <summary>Плита — статичное препятствие, PRD 4.2 (см. <c>Core.Tile.IsBlocked</c>).</summary>
        public bool IsBlocked { get; }

        /// <summary>Плита — смертельная ловушка (яма/лава), PRD 4.2 (см. <c>Core.Tile.LethalTrap</c>).</summary>
        public LethalTrapType? LethalTrap { get; }

        /// <summary>Доля пройденного времени распада, 0..1 (см. <c>Core.Tile.DecayProgress01</c>).</summary>
        public float DecayProgress01 { get; }

        /// <summary>
        /// Плита — триггер динамической мгновенной ловушки, PRD 4.2, issue
        /// #10 (см. <c>Core.Tile.ExplosiveTrapTarget</c>). Сама плита-триггер
        /// не опасна — цветом отмечена только для ручного тестирования
        /// (в финальном арте, возможно, останется невидимой для игрока).
        /// </summary>
        public bool IsExplosiveTrapTrigger { get; }

        /// <summary>
        /// Плита — триггер ловушки с таймингом, PRD v5 4.2, issue #45 (см.
        /// <c>Core.Tile.TimedTrapTarget</c>). Сама плита-триггер не опасна —
        /// цветом отмечена только для ручного тестирования.
        /// </summary>
        public bool IsTimedTrapTrigger { get; }

        /// <summary>
        /// Плита — цель ловушки с таймингом, физически опасна прямо сейчас
        /// (см. <c>Core.Tile.IsTimedTrapActive</c>/<c>Core.Tile.TimedTrapKind</c>).
        /// Null — не активна (ещё не сработала или снаряд/лезвие уже прошли).
        /// </summary>
        public TimedTrapType? ActiveTimedTrap { get; }

        /// <summary>
        /// Плита — вход в Комнату Босса (PRD v8 §8.1, см. <c>Core.Tile.IsBoss</c>).
        /// Единственная строка debug-визуала, которую v8 не отменяет: сама
        /// встреча с Боссом заменяется Комнатой (Спринт 11), но вход в неё —
        /// та же плита, ту же плитку нужно уметь найти на устройстве уже
        /// сейчас, иначе на неё не встать.
        /// </summary>
        public bool IsBoss { get; }
    }
}
