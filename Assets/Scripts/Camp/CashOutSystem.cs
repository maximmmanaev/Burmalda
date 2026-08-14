using System;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Movement;

namespace Burmalda.Camp
{
    /// <summary>
    /// Кэш-аут на Алтаре (PRD раздел 10, issue #25): "Кэш-аут на Алтаре
    /// фиксирует... прогресс до этой точки". Фиксирует чекпоинт (см.
    /// <see cref="RunCurrencyAccumulator.Checkpoint"/>) для Монет, Кристаллов
    /// Маны и Ключей текущего забега при достижении Алтаря — БЕЗУСЛОВНО, в
    /// отличие от <c>Altar.AltarTriggerSystem</c> (открытие Ритуала зависит
    /// от наличия Ключей): фиксация прогресса не требует Ключей, только
    /// сам факт достижения Алтаря. Откат к чекпоинту при смерти в пути
    /// назад — забота <see cref="ReturnJourneySystem"/>, не этого класса.
    /// </summary>
    public sealed class CashOutSystem : IDisposable
    {
        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly RunCurrencyAccumulator _coins;
        private readonly RunCurrencyAccumulator _mana;
        private readonly RunCurrencyAccumulator _keys;
        private bool _disposed;

        public CashOutSystem(TunnelGrid grid, GridTraceTrail trail, RunCurrencyAccumulator coins, RunCurrencyAccumulator mana, RunCurrencyAccumulator keys)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _coins = coins ?? throw new ArgumentNullException(nameof(coins));
            _mana = mana ?? throw new ArgumentNullException(nameof(mana));
            _keys = keys ?? throw new ArgumentNullException(nameof(keys));

            _trail.Advanced += OnAdvanced;
        }

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

            _coins.Checkpoint();
            _mana.Checkpoint();
            _keys.Checkpoint();
        }
    }
}
