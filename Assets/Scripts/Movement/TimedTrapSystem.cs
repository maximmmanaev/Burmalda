using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Активация и тайминг подвижных ловушек с таймингом (PRD v5 4.2, issue
    /// #45): «запускается с плиты-триггера и движется по определённой
    /// траектории/полосе в течение нескольких мгновений». Первая версия —
    /// одна плита-цель, окно опасности по реальному времени (не по шагам
    /// трейла, как остальные ловушки), а не движение снаряда по нескольким
    /// плитам подряд — решение владельца продукта, упрощение на будущее.
    ///
    /// «Приближение» из PRD, как и у мгновенных ловушек (#10), трактовано
    /// как проход трейла через саму плиту-триггер (<see cref="GridTraceTrail.PositionChanged"/>).
    /// После этого — задержка на подготовку (<see cref="WindUpSeconds"/>,
    /// телеграфирует игроку, что ловушка вот-вот сработает — саму задержку
    /// ещё предстоит связать с визуалом), затем плита-цель физически опасна
    /// в течение <see cref="ActiveSeconds"/> (<see cref="Tile.IsTimedTrapActive"/>),
    /// затем безопасна навсегда — одноразовая ловушка, как и взрыв (#10).
    ///
    /// Повторный проход через уже сработавший триггер не запускает вторую
    /// параллельную ловушку — иначе таймеры могли бы наложиться друг на
    /// друга и снять/выставить опасность не в том порядке.
    /// </summary>
    public sealed class TimedTrapSystem : IDisposable
    {
        // "Несколько мгновений" в PRD — точных секунд не задано. Черновые
        // значения, предмет плейтеста (Спринт 10, forbidden-actions.md):
        // задержка достаточна, чтобы игрок успел среагировать на срабатывание
        // триггера, активная фаза — короткое окно, которое нужно подгадать.
        public const float WindUpSeconds = 0.6f;
        public const float ActiveSeconds = 0.8f;

        private sealed class PendingTrap
        {
            public GridCoordinate Target;
            public TimedTrapType Kind;
            public float RemainingSeconds;
            public bool IsActivePhase;
        }

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly List<PendingTrap> _pending = new List<PendingTrap>();
        private readonly HashSet<GridCoordinate> _firedTriggers = new HashSet<GridCoordinate>();
        private bool _disposed;

        public TimedTrapSystem(TunnelGrid grid, GridTraceTrail trail)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _trail.PositionChanged += OnPositionChanged;
        }

        /// <summary>
        /// Продвигает тайминг всех ещё не разрешившихся ловушек на
        /// <paramref name="deltaSeconds"/> секунд реального времени —
        /// переключает фазы (подготовка → активна → безопасна навсегда) по
        /// достижении соответствующих порогов.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                var pending = _pending[i];
                pending.RemainingSeconds -= deltaSeconds;
                if (pending.RemainingSeconds > 0f) continue;

                if (!pending.IsActivePhase)
                {
                    _grid.GetOrCreateTile(pending.Target).ArmTimedTrap(pending.Kind);
                    pending.IsActivePhase = true;
                    pending.RemainingSeconds += ActiveSeconds; // перенос "перебора" времени в следующую фазу, без потери точности
                }
                else
                {
                    _grid.GetOrCreateTile(pending.Target).DisarmTimedTrap();
                    _pending.RemoveAt(i);
                }
            }
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
            if (!tile.TimedTrapTarget.HasValue) return;
            if (!_firedTriggers.Add(coordinate)) return; // уже сработал раньше — одноразовая ловушка

            _pending.Add(new PendingTrap
            {
                Target = tile.TimedTrapTarget.Value,
                Kind = tile.TimedTrapKind.Value,
                RemainingSeconds = WindUpSeconds,
                IsActivePhase = false
            });
        }
    }
}
