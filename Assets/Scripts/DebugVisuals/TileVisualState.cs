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
        public TileVisualState(bool isStart, bool isCurrentPosition, bool isDestroyed, bool isBlocked, LethalTrapType? lethalTrap, float decayProgress01, bool isExplosiveTrapTrigger, bool isTimedTrapTrigger, TimedTrapType? activeTimedTrap, bool isBoss = false, bool isManaSource = false, bool isKeySource = false, bool isLever = false, bool isGated = false, bool isLeverGateOpen = false, bool isAltar = false, bool isDangerSignatureRevealed = false)
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
            IsManaSource = isManaSource;
            IsKeySource = isKeySource;
            IsLever = isLever;
            IsGated = isGated;
            IsLeverGateOpen = isLeverGateOpen;
            IsAltar = isAltar;
            IsDangerSignatureRevealed = isDangerSignatureRevealed;
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

        /// <summary>
        /// Плита — источник Кристаллов Маны (PRD раздел 5, issue #12, см.
        /// <c>Core.Tile.IsManaSource</c>). Маршрутная задача «идти за Маной
        /// или за Ключами» требует, чтобы источник был виден игроку ВСЕГДА,
        /// не только в момент подбора (playtest владельца, задача «сделать
        /// тоннель играбельным») — в отличие от сигнатуры опасности, это
        /// награда, не подлежит сокрытию.
        /// </summary>
        public bool IsManaSource { get; }

        /// <summary>Плита — источник Ключей (PRD раздел 5, issue #12, см. <c>Core.Tile.IsKeySource</c>). Тот же принцип видимости, что и <see cref="IsManaSource"/>.</summary>
        public bool IsKeySource { get; }

        /// <summary>
        /// Плита — рычаг (PRD 4.2/21, issue #51, см. <c>Core.Tile.IsLever</c>).
        /// Задача «рычаг и ворота невидимы» (плейтест владельца — «рычагов
        /// поблизости не обнаружил»): видим ВСЕГДА, это механизм-приглашение
        /// («на это можно нажать»), не скрытая опасность — не должен
        /// путаться с сигнатурой ловушки.
        /// </summary>
        public bool IsLever { get; }

        /// <summary>
        /// Плита — часть бокового прохода, закрытого рычагом (issue #51, см.
        /// <c>Core.Tile.IsGated</c>). В отличие от <see cref="IsBlocked"/>
        /// («никогда») — это «пока нет»: должна читаться как явная
        /// временная преграда, не как обычная стена.
        /// </summary>
        public bool IsGated { get; }

        /// <summary>
        /// Осмысленно только когда <see cref="IsGated"/> истинно (см.
        /// <c>Core.Tile.IsLeverGateOpen</c>) — момент открытия ворот должен
        /// быть визуально заметен, иначе игрок не свяжет нажатие рычага с
        /// результатом.
        /// </summary>
        public bool IsLeverGateOpen { get; }

        /// <summary>
        /// Плита — Алтарь (PRD, см. <c>Core.Tile.IsAltar</c>). Фиксированное
        /// структурное состояние той же группы, что <see cref="IsBoss"/> —
        /// не подвержено распаду, должно быть видно всегда, задача «тёплый
        /// набор плит».
        /// </summary>
        public bool IsAltar { get; }

        /// <summary>
        /// Задача «раскрытие опасности при примеривании»: осмысленно только
        /// для скрытых состояний (<see cref="LethalTrap"/> Pit/Explosion,
        /// <see cref="IsExplosiveTrapTrigger"/>/<see cref="IsTimedTrapTrigger"/>)
        /// — см. <c>Core.Tile.IsDangerSignatureRevealed</c>. Пока ложно, эти
        /// состояния рисуются НЕОТЛИЧИМО от обычного пола (не сигнатурой) —
        /// раньше сигнатура была видна всегда, PRD v9 §4.2 заменяется.
        /// </summary>
        public bool IsDangerSignatureRevealed { get; }
    }
}
