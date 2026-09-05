using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Ловушка «Стрела» (docs/wiki/traps.md, issue #213) — раньше сосуществовала
    /// с более старой одноимённой механикой реального времени (<c>Core.TimedTrapType.Arrow</c>/
    /// <c>Movement.TimedTrapSystem</c>: одна плита-цель на реальном времени,
    /// не волна по нескольким плитам ряда ходами) — та старая механика
    /// удалена целиком владельцем 2026-09-05 («оставить только пять новых
    /// ловушек»). C#-идентификатор этой волны — <see cref="LethalTrapType.ArrowWave"/>.
    ///
    /// Построена на <see cref="TurnBasedThreatScheduler"/> (issue #212, свой
    /// экземпляр на систему — см. его doc-комментарий): проход трейла через
    /// плиту-триггер (<see cref="Tile.ArrowWaveTargetRow"/>/
    /// <see cref="Tile.ArrowWaveDirection"/>, заданы на генерации, ЕЩЁ НЕ
    /// подключено ни к одному генератору сегментов — отдельная задача
    /// авторинга шаблонов, здесь только механика) запускает волну: через
    /// <see cref="DelayTicks"/> ходов первый по направлению столбец
    /// заявленного ряда становится смертельным (<see cref="Tile.TransitionToLethalTrap"/>)
    /// ровно на 1 ход, затем безопасен снова (<see cref="Tile.ClearLethalTrap"/>)
    /// и опасным становится следующий столбец — пока волна не дойдёт до
    /// противоположного края ряда.
    ///
    /// Внешний <see cref="Tick"/> нужно вызывать явно, один раз на ход
    /// игрока (тот же принцип, что и у самого планировщика — эта система не
    /// подписывается на <see cref="TurnBasedThreatScheduler.TileDue"/>-
    /// эквивалент трейла сама, чтобы не завязываться на то, чем именно "ход"
    /// является для вызывающей стороны).
    ///
    /// Одноразовая ловушка на триггер — повторный проход не запускает вторую
    /// параллельную волну. Несколько одновременно активных волн (разные
    /// триггеры) поддерживаются независимо друг от друга.
    /// </summary>
    public sealed class ArrowWaveTrapSystem : IDisposable
    {
        // "Через 1 ход" — прямое требование владельца (docs/wiki/traps.md).
        // Балансное число, mutable static, не const — дебаг-панель (issue
        // #213, критерий приёмки), как TunnelObstacleGenerator.*Share.
        public static int DelayTicks = 1;

        private sealed class ActiveWave
        {
            public int Row;
            public RowWaveDirection Direction;
            public int NextColumnIndex;
            public GridCoordinate? PreviouslyArmedColumn;
        }

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly TurnBasedThreatScheduler _scheduler;
        private readonly HashSet<GridCoordinate> _firedTriggers = new HashSet<GridCoordinate>();

        // Координата, на "будильник" которой сейчас ждёт волна — на каждом
        // шаге волны перевешивается на следующую ожидаемую координату (см.
        // ScheduleNextStep). Ключ используется только для сопоставления
        // TurnBasedThreatScheduler.TileDue со "своей" волной — не хранит
        // игровой смысл сам по себе.
        private readonly Dictionary<GridCoordinate, ActiveWave> _waitingWaves = new Dictionary<GridCoordinate, ActiveWave>();

        private bool _disposed;

        public ArrowWaveTrapSystem(TunnelGrid grid, GridTraceTrail trail, TurnBasedThreatScheduler scheduler)
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
            if (!tile.ArrowWaveTargetRow.HasValue) return;
            if (!_firedTriggers.Add(coordinate)) return; // одноразовый триггер

            var direction = tile.ArrowWaveDirection.Value;
            var wave = new ActiveWave
            {
                Row = tile.ArrowWaveTargetRow.Value,
                Direction = direction,
                NextColumnIndex = FirstColumnIndex(direction, _grid.Width)
            };
            ScheduleNextStep(wave, DelayTicks);
        }

        private void OnTileDue(GridCoordinate coordinate)
        {
            if (!_waitingWaves.TryGetValue(coordinate, out var wave)) return;
            _waitingWaves.Remove(coordinate);

            // Снимаем опасность со столбца, опасного на ПРЕДЫДУЩЕМ шаге —
            // "опасна короткий момент, затем снова безопасна" (docs/wiki/traps.md).
            if (wave.PreviouslyArmedColumn.HasValue &&
                _grid.TryGetTile(wave.PreviouslyArmedColumn.Value, out var previousTile))
            {
                previousTile.ClearLethalTrap();
            }

            if (!IsColumnInRange(wave.NextColumnIndex))
                return; // волна уже прошла последний столбец — этот тик был нужен только чтобы снять опасность с него (см. выше)

            var current = new GridCoordinate(wave.Row, wave.NextColumnIndex);
            _grid.GetOrCreateTile(current).TransitionToLethalTrap(LethalTrapType.ArrowWave);
            wave.PreviouslyArmedColumn = current;
            wave.NextColumnIndex = StepColumnIndex(wave.NextColumnIndex, wave.Direction);

            ScheduleNextStep(wave, 1); // столбец опасен ровно 1 ход, затем — снятие (см. начало метода при следующем срабатывании)
        }

        private void ScheduleNextStep(ActiveWave wave, int ticksFromNow)
        {
            // Координата "будильника" для этого шага: следующий столбец,
            // если волна ещё не дошла до края, иначе — тот же столбец, что
            // уже отработал последним (нужен ровно один финальный тик,
            // чтобы снять с него опасность). TurnBasedThreatScheduler
            // поддерживает повторную независимую регистрацию одной и той же
            // координаты (см. его тесты) — коллизии с уже обработанным
            // срабатыванием нет, запись в _waitingWaves на эту секунду уже
            // удалена в OnTileDue до вызова этого метода.
            var alarmColumn = IsColumnInRange(wave.NextColumnIndex)
                ? wave.NextColumnIndex
                : wave.PreviouslyArmedColumn.Value.Column;
            var alarmCoordinate = new GridCoordinate(wave.Row, alarmColumn);
            _waitingWaves[alarmCoordinate] = wave;
            _scheduler.ScheduleActivation(alarmCoordinate, ticksFromNow);
        }

        private bool IsColumnInRange(int column) => column >= 0 && column < _grid.Width;

        private static int FirstColumnIndex(RowWaveDirection direction, int width) =>
            direction == RowWaveDirection.LeftToRight ? 0 : width - 1;

        private static int StepColumnIndex(int column, RowWaveDirection direction) =>
            direction == RowWaveDirection.LeftToRight ? column + 1 : column - 1;
    }
}
