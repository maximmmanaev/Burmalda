using System;
using System.Collections.Generic;

namespace Burmalda.Core
{
    /// <summary>
    /// Процедурная расстановка препятствий по сетке (PRD 4.2, issues #9/#10/
    /// #45): заблокированные плиты, статичные смертельные ловушки (яма/
    /// лава), триггеры динамических мгновенных ловушек (взрыв) и триггеры
    /// ловушек с таймингом (стрела/лезвие). Реагирует на
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
    /// триггер взрыва, 1.5%/1.5% триггер стрелы/лезвия) взяты буквально из
    /// прототипа там, где есть аналог (genTileType: rnd&lt;0.05 → 'block',
    /// rnd&lt;0.08 → 'pit', rnd&lt;0.11 → 'spike'), либо оценены агентом по
    /// аналогии там, где аналога нет (лава — новая в PRD v5; ловушки с
    /// таймингом — тоже новые в PRD v5, частота взята вдвое меньше взрыва,
    /// т.к. это более сложная в реализации будущая механика с окном
    /// тайминга, а не мгновенная смерть). Все эти числа — предмет плейтеста
    /// баланса (Спринт 10, см. docs/rules/forbidden-actions.md) — не менять
    /// молча.
    /// </summary>
    public sealed class TunnelObstacleGenerator : IDisposable
    {
        public const float BlockedThreshold = 0.05f;
        public const float PitThreshold = 0.065f;
        public const float LavaThreshold = 0.08f;
        public const float ExplosiveTriggerThreshold = 0.11f;
        public const float TimedTrapArrowThreshold = 0.125f;
        public const float TimedTrapBladeThreshold = 0.14f;

        private readonly TunnelGrid _grid;
        private readonly Func<float> _random01;
        // Координаты плит, зарезервированных как цель уже сгенерированного
        // триггера (взрыв #10 или тайминг #45) — им нельзя роллить
        // собственный тип, иначе цель могла бы оказаться заранее видимым
        // препятствием (block/pit/lava), что противоречит идее "не видна
        // заранее, пока не активирована". Одноразовая резервация — снимается
        // при первой материализации этой координаты (Remove ниже).
        private readonly HashSet<GridCoordinate> _reservedTrapTargets = new HashSet<GridCoordinate>();
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

            // Плита зарезервирована как цель другого триггера — остаётся
            // обычной (безопасной на вид) до срабатывания триггера,
            // собственного броска не получает.
            if (_reservedTrapTargets.Remove(tile.Coordinate)) return;

            var roll = _random01();
            if (roll < BlockedThreshold) tile.MarkBlocked();
            else if (roll < PitThreshold) tile.MarkLethalTrap(LethalTrapType.Pit);
            else if (roll < LavaThreshold) tile.MarkLethalTrap(LethalTrapType.Lava);
            else if (roll < ExplosiveTriggerThreshold)
            {
                var target = NextRowTarget(tile.Coordinate);
                tile.MarkExplosiveTrapTrigger(target);
                _reservedTrapTargets.Add(target);
            }
            else if (roll < TimedTrapArrowThreshold)
            {
                var target = NextRowTarget(tile.Coordinate);
                tile.MarkTimedTrapTrigger(target, TimedTrapType.Arrow);
                _reservedTrapTargets.Add(target);
            }
            else if (roll < TimedTrapBladeThreshold)
            {
                var target = NextRowTarget(tile.Coordinate);
                tile.MarkTimedTrapTrigger(target, TimedTrapType.Blade);
                _reservedTrapTargets.Add(target);
            }
        }

        // Цель — плита сразу впереди по глубине тоннеля, тот же столбец
        // (issues #10/#45: "запускается с плиты-триггера" — ближайшая по
        // ходу движения плита, PRD не уточняет радиус/направление
        // детальнее). Координата известна сразу — сама плита-цель
        // материализуется позже (TunnelGridReveal идёт по рядам
        // последовательно), к этому моменту резервация уже ждёт её.
        private static GridCoordinate NextRowTarget(GridCoordinate trigger) =>
            new GridCoordinate(trigger.Row + 1, trigger.Column);
    }
}
