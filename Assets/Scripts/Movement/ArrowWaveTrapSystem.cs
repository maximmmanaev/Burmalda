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
    /// <b>Реальное время, не ходы (владелец, 2026-09-14, issue #254 —
    /// «Волновые ловушки переходят на реальное время»).</b> Раньше система
    /// была построена на тактовом планировщике ходов (issue #212), тикаемом
    /// ровно на каждый шаг игрока — из-за этого волна физически не могла
    /// догнать игрока: она продвигалась ровно тогда же, когда шагал игрок,
    /// то есть была безопасна по построению, а не по игровому замыслу.
    /// Теперь построена на <see cref="RealTimeThreatScheduler"/> (см. её
    /// doc-комментарий) — вся последовательность после срабатывания триггера
    /// (первый столбец и каждый следующий) тикается реальными секундами,
    /// один параметр <see cref="StepSeconds"/> на весь путь волны.
    ///
    /// Проход трейла через плиту-триггер (<see cref="Tile.ArrowWaveTargetRow"/>/
    /// <see cref="Tile.ArrowWaveDirection"/>, заданы на генерации, ЕЩЁ НЕ
    /// подключено ни к одному генератору сегментов — отдельная задача
    /// авторинга шаблонов, здесь только механика) запускает волну: через
    /// <see cref="StepSeconds"/> секунд первый по направлению столбец
    /// заявленного ряда становится смертельным (<see cref="Tile.TransitionToLethalTrap"/>)
    /// ровно на <see cref="StepSeconds"/> секунд, затем безопасен снова
    /// (<see cref="Tile.ClearLethalTrap"/>) и опасным становится следующий
    /// столбец — пока волна не дойдёт до противоположного края ряда.
    ///
    /// Внешний <see cref="Tick"/> нужно вызывать явно, из <c>Update()</c>
    /// владеющего MonoBehaviour с <c>Time.deltaTime</c> — НЕ на каждый шаг
    /// игрока (это и был бы старый баг снова). Обнаружение триггера
    /// (<see cref="OnPositionChanged"/>) по-прежнему висит на
    /// <see cref="GridTraceTrail.PositionChanged"/> — только ПРОДВИЖЕНИЕ уже
    /// активной волны переехало на реальное время, не момент её запуска.
    ///
    /// <b>Issue #268 (2026-09-14, живой тест устройства, владелец: «смерть
    /// не появляется после того, как в меня выстреливает стрела»).</b> Игрок
    /// мог уже стоять на столбце, когда тот становится смертельным (не новый
    /// ход) — <see cref="GridTraceTrail.TryAdvanceTo"/> тут ни при чём.
    /// <see cref="OnTileDue"/> теперь вызывает
    /// <see cref="GridTraceTrail.CheckCurrentPositionForLethalTrap"/> сразу
    /// после армирования — тот же примитив, что уже использует
    /// <see cref="BombTrapSystem"/> (issue #260), не дублирует логику.
    ///
    /// Одноразовая ловушка на триггер — повторный проход не запускает вторую
    /// параллельную волну. Несколько одновременно активных волн (разные
    /// триггеры) поддерживаются независимо друг от друга.
    /// </summary>
    public sealed class ArrowWaveTrapSystem : IDisposable
    {
        // Единственный параметр скорости волны — и задержка до первого
        // столбца, и время между последующими столбцами (владелец: "Скорость
        // волны — параметр, настраиваемый в дебаг-панели"). Дефолт 0.3с —
        // тот же ориентир, что уже используется в docs/wiki/traps.md для
        // перевода ходов в секунды ("При шаге ~0.3 с... это примерно один
        // ход") — стартовое приближение агента, не решение владельца,
        // mutable static — дебаг-панель (issue #254, критерий приёмки).
        public static float StepSeconds = 0.3f;

        private sealed class ActiveWave
        {
            public int Row;
            public RowWaveDirection Direction;
            public int NextColumnIndex;
            public GridCoordinate? PreviouslyArmedColumn;
        }

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly RealTimeThreatScheduler _scheduler;
        private readonly HashSet<GridCoordinate> _firedTriggers = new HashSet<GridCoordinate>();

        // Координата, на "будильник" которой сейчас ждёт волна — на каждом
        // шаге волны перевешивается на следующую ожидаемую координату (см.
        // ScheduleNextStep). Ключ используется только для сопоставления
        // RealTimeThreatScheduler.TileDue со "своей" волной — не хранит
        // игровой смысл сам по себе.
        private readonly Dictionary<GridCoordinate, ActiveWave> _waitingWaves = new Dictionary<GridCoordinate, ActiveWave>();

        private bool _disposed;

        public ArrowWaveTrapSystem(TunnelGrid grid, GridTraceTrail trail, RealTimeThreatScheduler scheduler)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _trail.PositionChanged += OnPositionChanged;
            _scheduler.TileDue += OnTileDue;
        }

        /// <summary>Продвигает планировщик на <paramref name="deltaSeconds"/> реального времени — вызывать явно из Update() (см. doc-комментарий класса).</summary>
        public void Tick(float deltaSeconds) => _scheduler.Tick(deltaSeconds);

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
            ScheduleNextStep(wave, StepSeconds);
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
            // Issue #268: игрок мог уже стоять на этой плите, не совершая
            // новый ход — TryAdvanceTo тут ни при чём, см. doc-комментарий
            // GridTraceTrail.CheckCurrentPositionForLethalTrap (тот же
            // примитив, что уже использует BombTrapSystem, issue #260).
            _trail.CheckCurrentPositionForLethalTrap();
            wave.PreviouslyArmedColumn = current;
            wave.NextColumnIndex = StepColumnIndex(wave.NextColumnIndex, wave.Direction);

            ScheduleNextStep(wave, StepSeconds); // столбец опасен ровно StepSeconds, затем — снятие (см. начало метода при следующем срабатывании)
        }

        private void ScheduleNextStep(ActiveWave wave, float secondsFromNow)
        {
            // Координата "будильника" для этого шага: следующий столбец,
            // если волна ещё не дошла до края, иначе — тот же столбец, что
            // уже отработал последним (нужен ровно один финальный тик,
            // чтобы снять с него опасность). RealTimeThreatScheduler
            // поддерживает повторную независимую регистрацию одной и той же
            // координаты (см. его тесты) — коллизии с уже обработанным
            // срабатыванием нет, запись в _waitingWaves на эту секунду уже
            // удалена в OnTileDue до вызова этого метода.
            var alarmColumn = IsColumnInRange(wave.NextColumnIndex)
                ? wave.NextColumnIndex
                : wave.PreviouslyArmedColumn.Value.Column;
            var alarmCoordinate = new GridCoordinate(wave.Row, alarmColumn);
            _waitingWaves[alarmCoordinate] = wave;
            _scheduler.ScheduleActivation(alarmCoordinate, secondsFromNow);
        }

        private bool IsColumnInRange(int column) => column >= 0 && column < _grid.Width;

        private static int FirstColumnIndex(RowWaveDirection direction, int width) =>
            direction == RowWaveDirection.LeftToRight ? 0 : width - 1;

        private static int StepColumnIndex(int column, RowWaveDirection direction) =>
            direction == RowWaveDirection.LeftToRight ? column + 1 : column - 1;
    }
}
