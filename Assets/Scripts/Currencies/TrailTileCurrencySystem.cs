using System;
using Burmalda.Core;
using Burmalda.Movement;

namespace Burmalda.Currencies
{
    /// <summary>
    /// Сбор валюты с плиток-источников (PRD раздел 5, issue #12): "плитки-
    /// источники маны/ключей на пути". Общая реализация для Кристаллов Маны
    /// (<see cref="Tile.IsManaSource"/>) и Ключей (<see cref="Tile.IsKeySource"/>)
    /// — структурно одинаковы, различаются только тем, какой предикат/сумму
    /// им передаёт вызывающая сторона (см. <c>CurrencyController</c>).
    /// Реагирует на <see cref="GridTraceTrail.Advanced"/> — только
    /// по-настоящему новые плитки (#61), как и <c>TrailMultiplierSystem</c>.
    /// </summary>
    public sealed class TrailTileCurrencySystem : IDisposable
    {
        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly Func<Tile, bool> _isSource;
        private readonly Func<Tile, int> _amountForTile;
        private readonly float _rewardMultiplier;
        private readonly RunCurrencyAccumulator _accumulator;
        private bool _disposed;

        /// <param name="rewardMultiplier">
        /// Множитель начисляемой суммы (PRD v7 §20, Знамения «Хрупкий Свод»:
        /// Кристаллы Маны ×1.5, «Ловчая Тропа»: Ключи ×2). По умолчанию 1
        /// (нейтрально) — Currencies.asmdef намеренно не получает
        /// зависимость от RunModifiers, значение читает и передаёт
        /// вызывающая сторона (см. Currencies.CurrencyController).
        /// </param>
        public TrailTileCurrencySystem(TunnelGrid grid, GridTraceTrail trail, Func<Tile, bool> isSource, int amountPerSource, RunCurrencyAccumulator accumulator, float rewardMultiplier = 1f)
            : this(grid, trail, isSource, _ => amountPerSource, accumulator, rewardMultiplier)
        {
        }

        /// <summary>
        /// Перегрузка с суммой, вычисляемой ПО ПЛИТЕ, не единой на все
        /// источники (задача «видимые рычаги, инвариант лавы, размер
        /// награды за Воротами», владелец, 2026-09-04): тайник за Воротами
        /// (<see cref="Tile.KeySourceAmount"/>) даёт свою сумму, отличную
        /// от обычных источников на пути — <paramref name="amountForTile"/>
        /// решает это на каждый сбор, а не единая <c>amountPerSource</c>
        /// выше.
        /// </summary>
        public TrailTileCurrencySystem(TunnelGrid grid, GridTraceTrail trail, Func<Tile, bool> isSource, Func<Tile, int> amountForTile, RunCurrencyAccumulator accumulator, float rewardMultiplier = 1f)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _isSource = isSource ?? throw new ArgumentNullException(nameof(isSource));
            _amountForTile = amountForTile ?? throw new ArgumentNullException(nameof(amountForTile));
            _rewardMultiplier = rewardMultiplier;
            _accumulator = accumulator ?? throw new ArgumentNullException(nameof(accumulator));

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
            if (!_isSource(tile)) return;

            _accumulator.Add((int)Math.Round(_amountForTile(tile) * _rewardMultiplier));
        }
    }
}
