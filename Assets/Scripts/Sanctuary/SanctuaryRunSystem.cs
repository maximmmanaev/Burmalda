using System;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Generation;
using Burmalda.Movement;

namespace Burmalda.Sanctuary
{
    /// <summary>
    /// Рантайм Святилища (PRD v10 §14): вход по плите <see cref="Tile.IsSanctuaryEntry"/>,
    /// сбор плит множителя и Саркофага, обрушение рядов в Лаву, зачисление Маны на
    /// выходе. Раскладка выложена в сетку заранее (<c>SegmentRowProvider</c>), эта система
    /// только исполняет её во времени. Обрушение под игроком — опасность, как любая
    /// другая (<paramref name="reportCollapseHazard"/> → Бросок Судьбы).
    /// </summary>
    public sealed class SanctuaryRunSystem : IDisposable
    {
        /// <summary>ЧЕРНОВОЕ значение [CONFIG]: базовая Мана Жилы до множителя.</summary>
        public static int VeinBaseAmount = 30;

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly RunCurrencyAccumulator _mana;
        private readonly Action _reportCollapseHazard;
        private int _collapsedThroughRow;
        private bool _disposed;

        public SanctuaryRunSystem(TunnelGrid grid, GridTraceTrail trail, RunCurrencyAccumulator mana, Action reportCollapseHazard)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _mana = mana ?? throw new ArgumentNullException(nameof(mana));
            _reportCollapseHazard = reportCollapseHazard ?? throw new ArgumentNullException(nameof(reportCollapseHazard));

            _trail.Advanced += OnAdvanced;
            _trail.PositionChanged += OnPositionChanged;
        }

        /// <summary>Святилище текущего захода — null до входа.</summary>
        public Sanctuary ActiveSanctuary { get; private set; }

        /// <summary>Святилище пройдено (игрок дошёл до Алтаря на выходе); поднимается один раз.</summary>
        public event Action<Sanctuary> Exited;

        /// <summary>Игрок поднял Саркофаг.</summary>
        public event Action SarcophagusTaken;

        public void Dispose()
        {
            if (_disposed) return;
            _trail.Advanced -= OnAdvanced;
            _trail.PositionChanged -= OnPositionChanged;
            _disposed = true;
        }

        public void Tick(float deltaSeconds)
        {
            var sanctuary = ActiveSanctuary;
            if (sanctuary == null || !sanctuary.IsActive) return;

            sanctuary.Tick(deltaSeconds);
            CollapseUpTo(sanctuary.LastCollapsedRow);

            if (_grid.TryGetTile(_trail.CurrentPosition, out var tile) && tile.SanctuaryTile == SanctuaryTileKind.Rift && tile.RiftSubtype.HasValue)
                sanctuary.TickRift(tile.RiftSubtype.Value, deltaSeconds);
        }

        private void CollapseUpTo(int lastRow)
        {
            for (var row = _collapsedThroughRow + 1; row <= lastRow; row++)
            {
                for (var column = 0; column < _grid.Width; column++)
                    _grid.GetOrCreateTile(new GridCoordinate(row, column)).TransitionToLethalTrap(LethalTrapType.Lava);

                _collapsedThroughRow = row;
                if (_trail.CurrentPosition.Row == row) _reportCollapseHazard();
            }
        }

        private void OnAdvanced(GridCoordinate coordinate)
        {
            if (!_grid.TryGetTile(coordinate, out var tile)) return;

            if (ActiveSanctuary == null)
            {
                if (tile.IsSanctuaryEntry) Enter(coordinate.Row);
                return;
            }

            if (!ActiveSanctuary.IsActive) return;
            Collect(tile);
        }

        private void OnPositionChanged(GridCoordinate coordinate)
        {
            var sanctuary = ActiveSanctuary;
            if (sanctuary == null || !sanctuary.IsActive) return;

            sanctuary.ReportPlayerRow(coordinate.Row);
            if (!sanctuary.HasExited) return;

            _mana.Add(sanctuary.AccumulatedIncome);
            Exited?.Invoke(sanctuary);
        }

        private void Enter(int entryRow)
        {
            var exitRow = entryRow + SegmentTemplateCatalog.SanctuaryLeshyTemplate.RowCount - 1;
            ActiveSanctuary = new Sanctuary(entryRow, exitRow, Sanctuary.DefaultCollapseRowsPerSecond);
            ActiveSanctuary.SarcophagusTaken += () => SarcophagusTaken?.Invoke();
            _collapsedThroughRow = entryRow;
        }

        private void Collect(Tile tile)
        {
            switch (tile.SanctuaryTile)
            {
                case SanctuaryTileKind.Vein:
                    ActiveSanctuary.CollectVein(VeinBaseAmount);
                    break;
                case SanctuaryTileKind.Resonance:
                    ActiveSanctuary.CollectResonance();
                    break;
                case SanctuaryTileKind.Echo:
                    ActiveSanctuary.CollectEcho();
                    break;
            }

            if (tile.TakeSarcophagus()) ActiveSanctuary.TakeSarcophagus();
        }
    }
}
