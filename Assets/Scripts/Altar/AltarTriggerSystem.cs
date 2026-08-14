using System;
using Burmalda.Artifacts;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Movement;

namespace Burmalda.Altar
{
    /// <summary>
    /// Запуск Ритуала при достижении клетки-Алтаря (PRD раздел 7, issue
    /// #19): "Достижение Алтаря запускает Ритуал". Реагирует на
    /// <see cref="GridTraceTrail.Advanced"/> — только по-настоящему новая
    /// плита (#61), повторный проход по уже посещённому Алтарю не
    /// открывает Ритуал заново. "Если у игрока нет Ключей — проходит мимо
    /// без потерь" — реализовано как отсутствие события: если
    /// <see cref="RunCurrencyAccumulator.Total"/> Ключей равен 0, Ритуал не
    /// создаётся и не открывается.
    /// </summary>
    public sealed class AltarTriggerSystem : IDisposable
    {
        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly RunCurrencyAccumulator _keys;
        private readonly ArtifactPool _pool;
        private readonly Func<float> _random01;
        private bool _disposed;

        public AltarTriggerSystem(TunnelGrid grid, GridTraceTrail trail, RunCurrencyAccumulator keys, ArtifactPool pool, Func<float> random01)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _keys = keys ?? throw new ArgumentNullException(nameof(keys));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _random01 = random01 ?? throw new ArgumentNullException(nameof(random01));

            _trail.Advanced += OnAdvanced;
        }

        /// <summary>Срабатывает с новым <see cref="Ritual"/>, когда трейл достигает Алтаря и у игрока есть хотя бы 1 Ключ.</summary>
        public event Action<Ritual> RitualOpened;

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
            if (_keys.Total <= 0) return;

            RitualOpened?.Invoke(new Ritual(_pool, _random01));
        }
    }
}
