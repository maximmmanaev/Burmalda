using System;
using Burmalda.Core;
using Burmalda.Movement;

namespace Burmalda.Altar
{
    /// <summary>
    /// Достижение клетки-Алтаря (PRD v10 §13): на каждом Алтаре игрок выбирает Реликвию («1 из 2»),
    /// видит Афишу и решает «Дальше» / «Вернуться в Лагерь». Реагирует на
    /// <see cref="GridTraceTrail.Advanced"/> — только по-настоящему новая плита (#61), повторный проход по
    /// уже посещённому Алтарю событие не повторяет. Ключей и условий входа нет (v10-3).
    /// </summary>
    public sealed class AltarTriggerSystem : IDisposable
    {
        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private bool _disposed;

        public AltarTriggerSystem(TunnelGrid grid, GridTraceTrail trail)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _trail.Advanced += OnAdvanced;
        }

        /// <summary>Срабатывает, когда трейл впервые достигает клетки-Алтаря.</summary>
        public event Action<GridCoordinate> AltarReached;

        /// <summary>Отписывается от трейла. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.Advanced -= OnAdvanced;
            _disposed = true;
        }

        private void OnAdvanced(GridCoordinate coordinate)
        {
            if (!_grid.TryGetTile(coordinate, out var tile)) return;
            if (!tile.IsAltar) return;
            AltarReached?.Invoke(coordinate);
        }
    }
}
