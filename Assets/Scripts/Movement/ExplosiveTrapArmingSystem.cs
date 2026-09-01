using System;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Активация динамических мгновенных ловушек (PRD 4.2, issue #10):
    /// «активируются при приближении к плите-триггеру». Приближение здесь —
    /// проход трейла через саму плиту-триггер (<see cref="GridTraceTrail.PositionChanged"/>,
    /// а не просто соседство — простое соседство потребовало бы проверки на
    /// каждый ход, включая наведение без подтверждённого шага, и PRD не
    /// уточняет радиус приближения детальнее). Когда трейл оказывается на
    /// плите с <see cref="Tile.ExplosiveTrapTarget"/>, связанная плита-цель
    /// помечается <see cref="LethalTrapType.Explosion"/> — с этого момента
    /// она ведёт себя как обычная смертельная ловушка (issue #9,
    /// <see cref="GridTraceTrail.CanAdvanceTo"/>/<see cref="GridTraceTrail.LethalTrapTriggered"/>),
    /// никакой отдельной логики блокировки хода здесь не нужно.
    ///
    /// Реагирует на <see cref="GridTraceTrail.PositionChanged"/> (любой
    /// успешный ход, включая повтор — #61), а не только <see cref="GridTraceTrail.Advanced"/>:
    /// повторное срабатывание уже активной ловушки безопасно проверять при
    /// каждом ходе — <see cref="Tile.TransitionToLethalTrap"/> переприсваивает
    /// тот же <see cref="LethalTrapType.Explosion"/> (эффективно не-op).
    ///
    /// <b>Владелец, задача «разрушение плиты» (продолжение, 2026-09-01):</b>
    /// цель — <see cref="Tile.TransitionToLethalTrap"/>, не
    /// <see cref="Tile.MarkLethalTrap"/> — рантайм-переход, не генерация
    /// (см. её doc-комментарий): цель триггера может уже нести
    /// <see cref="Tile.IsManaSource"/>/<see cref="Tile.IsKeySource"/>
    /// (шаблоны «выкуп»/«последний-рывок» — "триггер уничтожает собственную
    /// награду", намеренная механика, не гонка генераторов).
    /// </summary>
    public sealed class ExplosiveTrapArmingSystem : IDisposable
    {
        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private bool _disposed;

        public ExplosiveTrapArmingSystem(TunnelGrid grid, GridTraceTrail trail)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _trail.PositionChanged += OnPositionChanged;
        }

        /// <summary>Отписывается от трейла. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.PositionChanged -= OnPositionChanged;
            _disposed = true;
        }

        private void OnPositionChanged(GridCoordinate coordinate)
        {
            if (!_grid.TryGetTile(coordinate, out var tile)) return;
            if (!tile.ExplosiveTrapTarget.HasValue) return;

            // Tile.TransitionToLethalTrap, не MarkLethalTrap — это рантайм-
            // переход (владелец, 2026-09-01), не генерация: цель может уже
            // нести ManaSource/KeySource (шаблоны «выкуп»/«последний-рывок»,
            // Generation.SegmentTemplateCatalog — "триггер уничтожает
            // собственную награду"), и это ожидаемо, не гонка генераторов.
            _grid.GetOrCreateTile(tile.ExplosiveTrapTarget.Value).TransitionToLethalTrap(LethalTrapType.Explosion);
        }
    }
}
