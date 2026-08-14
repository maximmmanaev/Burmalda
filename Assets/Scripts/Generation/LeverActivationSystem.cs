using System;
using System.Collections.Generic;
using Burmalda.Core;
using Burmalda.Movement;

namespace Burmalda.Generation
{
    /// <summary>
    /// Активация рычагов (PRD 4.2/21, issue #51): "Рычаг на карте — не
    /// ловушка... Активация открывает короткий боковой проход к артефакту".
    /// «Приближение»/активация — по аналогии с мгновенными и тайминг-
    /// ловушками (#10/#45) — трактуется как проход трейла через саму плиту-
    /// рычаг (<see cref="GridTraceTrail.PositionChanged"/>). Открытие ворот
    /// (<see cref="Tile.OpenLeverGate"/>) необратимо и одноразово — повторный
    /// проход через уже активированный рычаг ничего не меняет.
    /// </summary>
    public sealed class LeverActivationSystem : IDisposable
    {
        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly HashSet<GridCoordinate> _activatedLevers = new HashSet<GridCoordinate>();
        private bool _disposed;

        public LeverActivationSystem(TunnelGrid grid, GridTraceTrail trail)
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
            if (!tile.IsLever) return;
            if (tile.LeverGateTargets == null) return;
            if (!_activatedLevers.Add(coordinate)) return; // уже активирован — одноразово

            foreach (var target in tile.LeverGateTargets)
                _grid.GetOrCreateTile(target).OpenLeverGate();
        }
    }
}
