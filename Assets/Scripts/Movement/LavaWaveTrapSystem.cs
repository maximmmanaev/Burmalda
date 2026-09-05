using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Ловушка «Лава» (docs/wiki/traps.md, issue #216) — НЕ статичный
    /// <see cref="LethalTrapType.Lava"/> (плита с генерации, не трогается).
    /// Построена на <see cref="TurnBasedThreatScheduler"/> (issue #212, свой
    /// экземпляр на систему): проход трейла через плиту-триггер
    /// (<see cref="Tile.IsLavaTrigger"/>, ещё не подключено ни к одному
    /// генератору сегментов — отдельная задача авторинга, здесь только
    /// механика) запускает волну «сразу» (владелец) — на следующем же ходу
    /// ряд самого триггера становится лавой, затем каждый следующий ход ещё
    /// один ряд НАЗАД (в сторону убывания <see cref="GridCoordinate.Row"/>,
    /// откуда пришёл игрок — владелец: «волна идёт от ряда триггера назад»),
    /// пока не наберётся <see cref="MaxRows"/> рядов (владелец: «6 рядами,
    /// затем волна останавливается») или волна не дойдёт до начала тоннеля
    /// (<c>Row &lt; 0</c>). Весь ряд целиком (все столбцы) становится лавой
    /// одномоментно на своём шаге — не по одному тайлу, как Стрела.
    ///
    /// Лава НЕ возвращается в безопасное состояние (в отличие от Стрелы/
    /// Бомбы/Лезвий) — «отрезая путь назад» и есть весь смысл ловушки,
    /// необратимость намеренна, как у статичной <see cref="LethalTrapType.Lava"/>.
    ///
    /// <b>Инвариант волны (владелец, 2026-09-04, решение по конфликту с
    /// Воротами — docs/wiki/traps.md, раздел «4. Лава»): волна НИКОГДА не
    /// превращает в лаву ряд, на котором ПРЯМО СЕЙЧАС стоит игрок
    /// (<see cref="GridTraceTrail.CurrentPosition"/>), и ни один ряд
    /// ВПЕРЕДИ него — только позади.</b> Перед каждым шагом волны это
    /// проверяется явно (<see cref="OnTileDue"/>): если целевой ряд ещё не
    /// строго позади игрока, шаг ОТКЛАДЫВАЕТСЯ на 1 ход и проверяется снова
    /// (не пропускается навсегда, не режет бюджет <see cref="MaxRows"/>) —
    /// волна «ждёт», пока игрок не продвинется вперёд достаточно, чтобы
    /// целевой ряд оказался позади него. Это НЕ решение конфликта с Воротами
    /// само по себе (сочетание Лавы и Ворот в одном сегменте владелец прямо
    /// разрешил как намеренную цену жадного возврата, PRD v9 §4.3 —
    /// <see cref="Generation.SegmentReachabilityValidator"/> не трогается) —
    /// это отдельный, более фундаментальный инвариант самой волны: без него
    /// волна могла бы перекрыть ОСНОВНОЙ путь вперёд, что было бы настоящим
    /// тупиком без контригры, а не ценой возврата за наградой.
    ///
    /// Внешний <see cref="Tick"/> нужно вызывать явно, один раз на ход
    /// игрока (тот же принцип, что и у прочих систем этого семейства).
    /// Одноразовая ловушка на триггер.
    /// </summary>
    public sealed class LavaWaveTrapSystem : IDisposable
    {
        // "6 рядами" — прямое требование владельца. Балансное число,
        // mutable static, не const — дебаг-панель (issue #216, критерий приёмки).
        public static int MaxRows = 6;

        private sealed class ActiveWave
        {
            public GridCoordinate Trigger;
            public int TriggerRow;
            public int NextRowOffset; // 0 = ряд триггера, 1 = на 1 ряд назад, и т.д.
            public int RowsConverted;
        }

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly TurnBasedThreatScheduler _scheduler;
        private readonly HashSet<GridCoordinate> _firedTriggers = new HashSet<GridCoordinate>();

        // Ключ — координата триггера (тот же приём, что у BombTrapSystem/
        // FallingRockTrapSystem): у волны нет естественного "следующего
        // столбца" на такт, как у ArrowWaveTrapSystem, зато есть один
        // источник — сам триггер.
        private readonly Dictionary<GridCoordinate, ActiveWave> _activeWaves = new Dictionary<GridCoordinate, ActiveWave>();

        private bool _disposed;

        public LavaWaveTrapSystem(TunnelGrid grid, GridTraceTrail trail, TurnBasedThreatScheduler scheduler)
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
            if (!tile.IsLavaTrigger) return;
            if (!_firedTriggers.Add(coordinate)) return; // одноразовый триггер

            var wave = new ActiveWave
            {
                Trigger = coordinate,
                TriggerRow = coordinate.Row,
                NextRowOffset = 0,
                RowsConverted = 0
            };
            _activeWaves[coordinate] = wave;
            _scheduler.ScheduleActivation(coordinate, ticksFromNow: 1);
        }

        private void OnTileDue(GridCoordinate coordinate)
        {
            if (!_activeWaves.TryGetValue(coordinate, out var wave)) return;

            var candidateRow = wave.TriggerRow - wave.NextRowOffset;
            if (candidateRow < 0)
            {
                // Волна дошла до начала тоннеля раньше, чем набрала MaxRows — дальше рядов физически нет.
                _activeWaves.Remove(coordinate);
                return;
            }

            // Инвариант волны (см. doc-комментарий класса): целевой ряд
            // обязан быть строго позади игрока. Пока это не так — не
            // пропускаем шаг навсегда, а откладываем на 1 ход и проверяем
            // снова, тот же NextRowOffset/RowsConverted.
            if (candidateRow >= _trail.CurrentPosition.Row)
            {
                _scheduler.ScheduleActivation(coordinate, ticksFromNow: 1);
                return;
            }

            for (var column = 0; column < _grid.Width; column++)
                _grid.GetOrCreateTile(new GridCoordinate(candidateRow, column)).TransitionToLethalTrap(LethalTrapType.LavaWave);

            wave.NextRowOffset++;
            wave.RowsConverted++;

            if (wave.RowsConverted >= MaxRows)
            {
                _activeWaves.Remove(coordinate);
                return;
            }

            _scheduler.ScheduleActivation(coordinate, ticksFromNow: 1);
        }
    }
}
