using System;
using System.Collections.Generic;

namespace Burmalda.Core
{
    /// <summary>
    /// Процедурная расстановка препятствий по сетке (PRD 4.2, issues #9/#10):
    /// заблокированные плиты, статичные смертельные ловушки (яма/лава) и
    /// триггеры динамических мгновенных ловушек (взрыв). Реагирует на
    /// <see cref="TunnelGrid.TileMaterialized"/> — как только плита впервые
    /// появляется в сетке, для неё один раз бросается случайное решение, по
    /// аналогии с genTileType() из legacy/burmolda_demo.html. Материализация
    /// плит заранее (до того как игрок успевает до них дойти) — не забота
    /// этого класса, см. <c>Burmalda.Movement.TunnelGridReveal</c>.
    ///
    /// Стартовый ряд (Row == 0) всегда безопасен — как и в прототипе
    /// (genTileType: "if(r===0)return 'safe'").
    ///
    /// Пороговые значения (5% заблокировано, 3% суммарно яма/лава, 3%
    /// триггер взрыва) взяты буквально из прототипа (genTileType: rnd&lt;0.05
    /// → 'block', rnd&lt;0.08 → 'pit', rnd&lt;0.11 → 'spike'). Деление ямы/лавы
    /// поровну (1.5%/1.5%) — черновое решение агента: в прототипе лавы не
    /// было (появилась только в PRD v5, аналога частоты взять неоткуда);
    /// частота триггера взрыва позаимствована у прототипного 'spike' (сам
    /// spike в прототипе был статичной мгновенной смертью без отдельного
    /// триггера — ближайший доступный аналог по частоте, не по механике, см.
    /// PRD v5 4.2). Все эти числа — предмет плейтеста баланса (Спринт 10, см.
    /// docs/rules/forbidden-actions.md) — не менять молча.
    /// </summary>
    public sealed class TunnelObstacleGenerator : IDisposable
    {
        public const float BlockedThreshold = 0.05f;
        public const float PitThreshold = 0.065f;
        public const float LavaThreshold = 0.08f;
        public const float ExplosiveTriggerThreshold = 0.11f;

        private readonly TunnelGrid _grid;
        private readonly Func<float> _random01;
        // Координаты плит, зарезервированных как цель уже сгенерированного
        // триггера (issue #10) — им нельзя роллить собственный тип, иначе
        // цель взрыва могла бы оказаться заранее видимым препятствием
        // (block/pit/lava), что противоречит идее "не видна заранее, пока
        // не активирована". Одноразовая резервация — снимается при первой
        // материализации этой координаты (Remove ниже).
        private readonly HashSet<GridCoordinate> _reservedExplosionTargets = new HashSet<GridCoordinate>();
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

            // Плита зарезервирована как цель взрыва другого триггера —
            // остаётся обычной (безопасной на вид) до срабатывания триггера,
            // собственного броска не получает.
            if (_reservedExplosionTargets.Remove(tile.Coordinate)) return;

            var roll = _random01();
            if (roll < BlockedThreshold) tile.MarkBlocked();
            else if (roll < PitThreshold) tile.MarkLethalTrap(LethalTrapType.Pit);
            else if (roll < LavaThreshold) tile.MarkLethalTrap(LethalTrapType.Lava);
            else if (roll < ExplosiveTriggerThreshold)
            {
                // Цель — плита сразу впереди по глубине тоннеля, тот же
                // столбец (issue #10: "запускается с плиты-триггера" —
                // ближайшая по ходу движения плита, PRD не уточняет радиус/
                // направление детальнее). Координата известна сразу — сама
                // плита-цель материализуется позже (TunnelGridReveal идёт по
                // рядам последовательно), к этому моменту резервация уже
                // ждёт её.
                var target = new GridCoordinate(tile.Coordinate.Row + 1, tile.Coordinate.Column);
                tile.MarkExplosiveTrapTrigger(target);
                _reservedExplosionTargets.Add(target);
            }
        }
    }
}
