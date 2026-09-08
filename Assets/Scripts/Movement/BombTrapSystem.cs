using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Ловушка «Бомба» (docs/wiki/traps.md, issue #214) — раньше сосуществовала
    /// с ближайшим по духу, но не совпадающим ни по одному из трёх
    /// параметров прецедентом (<c>Movement.ExplosiveTrapArmingSystem</c>/
    /// <c>Core.LethalTrapType.Explosion</c>: активация мгновенная, не через
    /// 2 хода; одна плита, не площадь 3×3; результат постоянный, не возврат
    /// в обычное состояние) — та старая механика удалена целиком владельцем
    /// 2026-09-05 («оставить только пять новых ловушек»). C#-идентификатор
    /// этой ловушки — <see cref="LethalTrapType.BombBlast"/>.
    ///
    /// Построена на <see cref="TurnBasedThreatScheduler"/> (issue #212, свой
    /// экземпляр на систему): проход трейла через плиту-триггер
    /// (<see cref="Tile.IsBombTrigger"/>) запускает отсчёт. Через
    /// <see cref="DelayTicks"/> ходов площадь <see cref="RadiusTiles"/>
    /// вокруг триггера (по умолчанию радиус 1 — квадрат 3×3, восемь соседей
    /// плюс сама плита-триггер, обрезанный по границе сетки) становится
    /// смертельной ОДНОМОМЕНТНО — одним циклом <see cref="Tile.TransitionToLethalTrap"/>
    /// внутри одного вызова <see cref="Tick"/>, не по очереди, как у Стрелы
    /// (<see cref="ArrowWaveTrapSystem"/>). Через <see cref="ExplosionDurationTicks"/>
    /// ходов ПОСЛЕ взрыва площадь возвращается в обычное состояние
    /// (<see cref="Tile.ClearLethalTrap"/>) — владелец прямым текстом:
    /// «плиты не разрушаются... дыры — отдельное решение, не реализовывать
    /// без явного запроса».
    ///
    /// <b>Владелец НЕ указал явно, сколько ходов площадь остаётся смертельной
    /// после взрыва до возврата в обычное состояние</b> (docs/wiki/traps.md
    /// говорит только "через 2 хода после активации... становятся
    /// смертельными одномоментно" и отдельно "после взрыва плиты
    /// возвращаются в обычное состояние", без числа между этими двумя
    /// моментами). <see cref="ExplosionDurationTicks"/> = 1 — минимальный
    /// осмысленный дефолт (меньше 1 хода означает "никогда фактически не
    /// опасна"), задокументирован здесь как предположение агента, а не
    /// решение владельца, mutable static — владелец меняет без правки кода.
    ///
    /// <b>Не решает</b>: что происходит, если игрок стоит НА триггере (или
    /// соседней плите) в момент, когда таймер истекает и площадь становится
    /// смертельной — в отличие от <see cref="Decay.TrailDecaySystem.TileDestroyed"/>
    /// (которое явно проверяет текущую позицию игрока), эта система (и
    /// <see cref="RunLifecycle.RunState"/>) сейчас не убивает игрока
    /// РЕТРОАКТИВНО за то, что он стоит на плите, ставшей смертельной, пока
    /// он там уже стоял, не совершая новый ход на неё — симметрично тому,
    /// как <see cref="ArrowWaveTrapSystem"/> тоже этого не делает. Открытый
    /// вопрос, не входит в критерии приёмки issue #214, решение — за
    /// отдельной задачей, если владелец сочтёт нужным.
    ///
    /// Одноразовая ловушка на триггер — повторный проход не запускает
    /// вторую параллельную бомбу (тот же приём, что <see cref="ArrowWaveTrapSystem"/>).
    /// Несколько одновременных бомб (разные триггеры) поддерживаются
    /// независимо друг от друга.
    /// </summary>
    public sealed class BombTrapSystem : IDisposable
    {
        // "Через 2 хода" — прямое требование владельца. Балансное число,
        // mutable static, не const — дебаг-панель (issue #214, критерий приёмки).
        public static int DelayTicks = 2;

        // "Радиус 1 (квадрат 3×3)" — прямое требование владельца.
        public static int RadiusTiles = 1;

        // Не задано владельцем явно — см. doc-комментарий класса.
        public static int ExplosionDurationTicks = 1;

        private sealed class PendingExplosion
        {
            public GridCoordinate Trigger;
            public bool IsArmed;
        }

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly TurnBasedThreatScheduler _scheduler;
        private readonly HashSet<GridCoordinate> _firedTriggers = new HashSet<GridCoordinate>();

        // Ключ — координата триггера: вся площадь взрыва арится/снимается
        // одним событием на один и тот же "будильник" (в отличие от
        // ArrowWaveTrapSystem, где у каждого столбца свой), поэтому одной
        // координаты-триггера достаточно, чтобы сопоставить TileDue со
        // "своим" взрывом.
        private readonly Dictionary<GridCoordinate, PendingExplosion> _waitingExplosions = new Dictionary<GridCoordinate, PendingExplosion>();

        private bool _disposed;

        public BombTrapSystem(TunnelGrid grid, GridTraceTrail trail, TurnBasedThreatScheduler scheduler)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _trail.PositionChanged += OnPositionChanged;
            _scheduler.TileDue += OnTileDue;
        }

        /// <summary>Продвигает планировщик на 1 ход — вызывать явно на каждый ход игрока (см. doc-комментарий класса).</summary>
        public void Tick() => _scheduler.Tick();

        /// <summary>Отписывается от трейла и планировщика. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.PositionChanged -= OnPositionChanged;
            _scheduler.TileDue -= OnTileDue;
            _disposed = true;
        }

        private void OnPositionChanged(GridCoordinate coordinate)
        {
            if (!_grid.TryGetTile(coordinate, out var tile)) return;
            if (!tile.IsBombTrigger) return;
            if (!_firedTriggers.Add(coordinate)) return; // одноразовый триггер

            var explosion = new PendingExplosion { Trigger = coordinate, IsArmed = false };
            _waitingExplosions[coordinate] = explosion;
            _scheduler.ScheduleActivation(coordinate, DelayTicks);
        }

        private void OnTileDue(GridCoordinate coordinate)
        {
            if (!_waitingExplosions.TryGetValue(coordinate, out var explosion)) return;
            _waitingExplosions.Remove(coordinate);

            if (!explosion.IsArmed)
            {
                // "Одномоментно, не по очереди" (критерий приёмки) — все
                // плиты площади получают LethalTrap в рамках ОДНОГО вызова
                // OnTileDue, ни одна не ждёт следующего Tick.
                foreach (var target in ComputeBlastArea(explosion.Trigger))
                    _grid.GetOrCreateTile(target).TransitionToLethalTrap(LethalTrapType.BombBlast);

                explosion.IsArmed = true;
                _waitingExplosions[coordinate] = explosion; // тот же ключ — повторная независимая регистрация, запись выше уже удалена
                _scheduler.ScheduleActivation(coordinate, ExplosionDurationTicks);
            }
            else
            {
                foreach (var target in ComputeBlastArea(explosion.Trigger))
                {
                    if (_grid.TryGetTile(target, out var tile)) tile.ClearLethalTrap();
                }
            }
        }

        /// <summary>
        /// Все координаты в пределах <see cref="RadiusTiles"/> вокруг
        /// <paramref name="center"/> (включая саму <paramref name="center"/>),
        /// обрезанные по границе сетки (<see cref="TunnelGrid.Contains"/>) —
        /// у края тоннеля площадь взрыва меньше 9 тайлов (критерий приёмки).
        /// </summary>
        private IEnumerable<GridCoordinate> ComputeBlastArea(GridCoordinate center)
        {
            for (var rowOffset = -RadiusTiles; rowOffset <= RadiusTiles; rowOffset++)
            {
                for (var columnOffset = -RadiusTiles; columnOffset <= RadiusTiles; columnOffset++)
                {
                    var candidate = new GridCoordinate(center.Row + rowOffset, center.Column + columnOffset);
                    if (_grid.Contains(candidate)) yield return candidate;
                }
            }
        }
    }
}
