using System;
using Burmalda.Core;
using Burmalda.Movement;

namespace Burmalda.Altar
{
    /// <summary>
    /// Тайник за Воротами (PRD v10 §10): подбор даёт <b>один дополнительный вариант на следующем Алтаре</b>
    /// (выбор 1 из 3). Несколько тайников до одного Алтаря не складываются — бонус не больше одного варианта [ГД].
    /// Реагирует на <see cref="GridTraceTrail.Advanced"/> — только по-настоящему новая плита.
    /// </summary>
    public sealed class VaultBonusTracker : IDisposable
    {
        /// <summary>Максимум накопленных дополнительных вариантов [ГД, черновик].</summary>
        public const int MaxPendingOptions = 1;

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private bool _disposed;

        public VaultBonusTracker(TunnelGrid grid, GridTraceTrail trail)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _trail.Advanced += OnAdvanced;
        }

        /// <summary>Дополнительных вариантов, ожидающих следующего Алтаря (маркер в HUD).</summary>
        public int PendingExtraOptions { get; private set; }

        /// <summary>Срабатывает при изменении <see cref="PendingExtraOptions"/>.</summary>
        public event Action<int> Changed;

        /// <summary>Забирает накопленные варианты для текущего Алтаря и обнуляет счётчик.</summary>
        public int Consume()
        {
            var granted = PendingExtraOptions;
            if (granted == 0) return 0;
            PendingExtraOptions = 0;
            Changed?.Invoke(0);
            return granted;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _trail.Advanced -= OnAdvanced;
            _disposed = true;
        }

        private void OnAdvanced(GridCoordinate coordinate)
        {
            if (!_grid.TryGetTile(coordinate, out var tile) || !tile.IsGateVault) return;
            if (PendingExtraOptions >= MaxPendingOptions) return;
            PendingExtraOptions++;
            Changed?.Invoke(PendingExtraOptions);
        }
    }
}
