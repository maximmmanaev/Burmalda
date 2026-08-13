using System;

namespace Burmalda.Core
{
    /// <summary>
    /// Процедурная расстановка статичных препятствий (PRD 4.2, issue #9):
    /// заблокированные плиты и смертельные ловушки (яма/лава). Реагирует на
    /// <see cref="TunnelGrid.TileMaterialized"/> — как только плита впервые
    /// появляется в сетке, для неё один раз бросается случайное решение, по
    /// аналогии с genTileType() из legacy/burmolda_demo.html. Материализация
    /// плит заранее (до того как игрок успевает до них дойти) — не забота
    /// этого класса, см. <c>Burmalda.Movement.TunnelGridReveal</c>.
    ///
    /// Стартовый ряд (Row == 0) всегда безопасен — как и в прототипе
    /// (genTileType: "if(r===0)return 'safe'").
    ///
    /// Пороговые значения (5% заблокировано, 3% суммарно яма/лава) взяты
    /// буквально из прототипа (genTileType: rnd&lt;0.05 → 'block', rnd&lt;0.08 →
    /// 'pit'). Деление ямы/лавы поровну (1.5%/1.5%) — черновое решение
    /// агента: в прототипе лавы не было (появилась только в PRD v5, аналога
    /// частоты взять неоткуда). Оба числа — предмет плейтеста баланса
    /// (Спринт 10, см. docs/rules/forbidden-actions.md) — не менять молча.
    /// </summary>
    public sealed class TunnelObstacleGenerator : IDisposable
    {
        public const float BlockedThreshold = 0.05f;
        public const float PitThreshold = 0.065f;
        public const float LavaThreshold = 0.08f;

        private readonly TunnelGrid _grid;
        private readonly Func<float> _random01;
        private bool _disposed;

        /// <param name="random01">
        /// Источник случайности — должен возвращать значение в [0, 1) при
        /// каждом вызове (как <c>UnityEngine.Random.value</c>/<c>System.Random.NextDouble()</c>).
        /// Внедряется явно вместо прямой зависимости от конкретного RNG —
        /// тесты подставляют детерминированную последовательность.
        /// </param>
        public TunnelObstacleGenerator(TunnelGrid grid, Func<float> random01)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _random01 = random01 ?? throw new ArgumentNullException(nameof(random01));
            _grid.TileMaterialized += OnTileMaterialized;
        }

        /// <summary>Отписывается от сетки. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _grid.TileMaterialized -= OnTileMaterialized;
            _disposed = true;
        }

        private void OnTileMaterialized(Tile tile)
        {
            if (tile.Coordinate.Row == 0) return; // стартовый ряд всегда безопасен

            var roll = _random01();
            if (roll < BlockedThreshold) tile.MarkBlocked();
            else if (roll < PitThreshold) tile.MarkLethalTrap(LethalTrapType.Pit);
            else if (roll < LavaThreshold) tile.MarkLethalTrap(LethalTrapType.Lava);
        }
    }
}
