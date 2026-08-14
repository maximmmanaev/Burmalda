using System.Collections.Generic;

namespace Burmalda.Core
{
    /// <summary>
    /// Плита сетки тоннеля (PRD 4.1). Хранит координату и состояние распада
    /// (PRD 4.1/16) — само тиканье распада и его тайминг реализует
    /// Burmalda.Decay.TrailDecaySystem, Tile только хранит и применяет результат.
    /// Также хранит статичное препятствие (PRD 4.2, issue #9 — заблокированная
    /// плита, яма/лава) — постоянное, не тикает и не связано с распадом, и
    /// связку триггер→плита-взрыва динамической мгновенной ловушки (PRD 4.2,
    /// issue #10) — в отличие от статичных ловушек, опасность плиты
    /// появляется не сразу, а по факту прохода трейла через её триггер, и
    /// такую же связку для подвижной ловушки с таймингом (PRD v5 4.2, issue
    /// #45) — здесь опасность ещё и временная, включается/выключается по
    /// реальному времени, а не постоянна с момента активации.
    /// Также хранит признак плиты-источника Кристаллов Маны/Ключей (PRD
    /// раздел 5, issue #12) — постоянное состояние, аналогично препятствиям.
    /// Остальной тип плиты (артефакт) добавится сюда же по мере Traps.
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

        /// <summary>
        /// Плита — триггер динамической мгновенной ловушки (PRD 4.2, issue
        /// #10): координата связанной плиты-взрыва, которая становится
        /// смертельной (<see cref="LethalTrapType.Explosion"/>), когда трейл
        /// проходит через эту плиту-триггер. Null — плита не триггер. Сама
        /// эта плита-триггер безопасна для прохода — опасна именно связанная
        /// плита-цель.
        /// </summary>
        public GridCoordinate? ExplosiveTrapTarget { get; private set; }

        /// <summary>Помечает плиту как триггер, связанный с плитой-целью. Повторные вызовы сохраняют первую цель.</summary>
        public void MarkExplosiveTrapTrigger(GridCoordinate targetCoordinate)
        {
            if (ExplosiveTrapTarget.HasValue) return;
            ExplosiveTrapTarget = targetCoordinate;
        }

        /// <summary>
        /// Плита — триггер подвижной ловушки с таймингом (PRD v5 4.2, issue
        /// #45): координата связанной плиты-цели. В отличие от взрыва (#10),
        /// опасность цели не постоянна — включается/выключается по времени,
        /// см. <see cref="IsTimedTrapActive"/> и <c>Burmalda.Movement.TimedTrapSystem</c>.
        /// Сама плита-триггер всегда безопасна для прохода.
        /// </summary>
        public GridCoordinate? TimedTrapTarget { get; private set; }

        /// <summary>
        /// Тип ловушки с таймингом — актуален и на плите-триггере (какую цель
        /// готовит <see cref="TimedTrapTarget"/>), и на плите-цели, пока
        /// <see cref="IsTimedTrapActive"/> (какой тип сейчас активен). Одна и
        /// та же плита никогда не занимает обе роли одновременно — резервация
        /// цели в генераторе исключает эту коллизию.
        /// </summary>
        public TimedTrapType? TimedTrapKind { get; private set; }

        /// <summary>Помечает плиту как триггер ловушки с таймингом заданного типа, связанный с плитой-целью. Повторные вызовы сохраняют первые значения.</summary>
        public void MarkTimedTrapTrigger(GridCoordinate targetCoordinate, TimedTrapType kind)
        {
            if (TimedTrapTarget.HasValue) return;
            TimedTrapTarget = targetCoordinate;
            TimedTrapKind = kind;
        }

        /// <summary>
        /// Плита-цель ловушки с таймингом сейчас физически опасна (снаряд/
        /// лезвие проходит через неё) — в отличие от прочих ловушек это
        /// временное состояние, включается и выключается
        /// <c>Burmalda.Movement.TimedTrapSystem</c> по реальному времени.
        /// </summary>
        public bool IsTimedTrapActive { get; private set; }

        /// <summary>Делает плиту-цель опасной прямо сейчас и запоминает тип (для сообщения о смерти/визуала).</summary>
        public void ArmTimedTrap(TimedTrapType kind)
        {
            TimedTrapKind = kind;
            IsTimedTrapActive = true;
        }

        /// <summary>Снимает опасность с плиты-цели — окончательно, снаряд/лезвие уже прошли (одноразовая ловушка).</summary>
        public void DisarmTimedTrap()
        {
            IsTimedTrapActive = false;
        }

        /// <summary>
        /// Плита — рычаг (PRD 4.2, раздел 21, issue #51): не ловушка, не
        /// наносит вреда. Активация (проход трейла через эту плиту) должна
        /// открыть все плиты <see cref="LeverGateTargets"/> — см.
        /// <c>Burmalda.Generation.LeverActivationSystem</c>.
        /// </summary>
        public bool IsLever { get; private set; }

        /// <summary>Координаты плит короткого бокового прохода, которые открывает этот рычаг. Null, пока не помечена рычагом.</summary>
        public IReadOnlyList<GridCoordinate> LeverGateTargets { get; private set; }

        /// <summary>Помечает плиту как рычаг с заданным набором целей-ворот. Повторные вызовы сохраняют первый набор.</summary>
        public void MarkLever(IReadOnlyList<GridCoordinate> gateTargets)
        {
            if (IsLever) return;
            IsLever = true;
            LeverGateTargets = gateTargets;
        }

        /// <summary>
        /// Плита — часть бокового прохода, закрытого до активации связанного
        /// рычага (issue #51). В отличие от <see cref="IsBlocked"/>
        /// (постоянно непроходима), становится проходимой после
        /// <see cref="OpenLeverGate"/>.
        /// </summary>
        public bool IsGated { get; private set; }

        /// <summary>Плита-ворота открыта — рычаг уже активирован.</summary>
        public bool IsLeverGateOpen { get; private set; }

        /// <summary>Помечает плиту как закрытые ворота бокового прохода (закрыта по умолчанию).</summary>
        public void MarkGated()
        {
            IsGated = true;
        }

        /// <summary>Открывает ворота бокового прохода — вызывается системой активации рычага.</summary>
        public void OpenLeverGate()
        {
            IsLeverGateOpen = true;
        }

        /// <summary>
        /// Плита — источник Кристаллов Маны (PRD раздел 5, issue #12):
        /// "плитки-источники маны на пути". Сбор — см.
        /// <c>Burmalda.Currencies.TrailTileCurrencySystem</c>.
        /// </summary>
        public bool IsManaSource { get; private set; }

        /// <summary>Помечает плиту как источник Кристаллов Маны. Повторные вызовы — не-op.</summary>
        public void MarkManaSource()
        {
            IsManaSource = true;
        }

        /// <summary>
        /// Плита — источник Ключей (PRD раздел 5, issue #12): "отдельные
        /// плитки-источники ключей на пути".
        /// </summary>
        public bool IsKeySource { get; private set; }

        /// <summary>Помечает плиту как источник Ключей. Повторные вызовы — не-op.</summary>
        public void MarkKeySource()
        {
            IsKeySource = true;
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
