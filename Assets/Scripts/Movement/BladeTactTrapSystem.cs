using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Ловушка «Лезвия» (docs/wiki/traps.md, issue #215) — раньше сосуществовала
    /// с более старой одноимённой механикой реального времени (<c>Core.TimedTrapType.Blade</c>/
    /// <c>Movement.TimedTrapSystem</c>: одна плита-цель на реальном времени,
    /// не пятитактовый паттерн по ряду ходами) — та старая механика удалена
    /// целиком владельцем 2026-09-05 («оставить только пять новых
    /// ловушек»). C#-идентификатор этого такта — <see cref="LethalTrapType.BladeTact"/>.
    ///
    /// Построена на <see cref="TurnBasedThreatScheduler"/> (issue #212, свой
    /// экземпляр на систему): проход трейла через плиту-триггер
    /// (<see cref="Tile.BladeTactTargetRow"/>, ещё не подключено ни к
    /// одному генератору сегментов — отдельная задача авторинга, здесь
    /// только механика) запускает такт: через <see cref="DelayTicks"/>
    /// ходов начинается пятитактовый паттерн по заявленному ряду —
    /// симметричные пары столбцов от края к центру и обратно (владелец:
    /// «1 и 5 → 2 и 4 → 3 → 2 и 4 → 1 и 5», здесь — обобщение на
    /// произвольную ширину сетки: крайняя пара → следующая пара внутрь →
    /// ... → центр(ы) → обратно наружу до крайней пары), каждый такт
    /// заявленные столбцы смертельны ровно <see cref="TactTicks"/> ходов,
    /// затем снова безопасны и наступает следующий такт.
    ///
    /// Цикл повторяется до <see cref="CycleCount"/> раз (владелец: «по
    /// умолчанию два полных цикла, затем ловушка затихает») ЛИБО
    /// останавливается раньше, если игрок покидает ряд ловушки (проверяется
    /// на каждом такте, см. <see cref="OnTileDue"/>) — что наступит раньше.
    /// <see cref="CycleCount"/> читается ОДИН раз при активации триггера,
    /// не на каждом такте — смена значения владельцем между забегами не
    /// ломает уже идущий цикл текущего забега.
    ///
    /// <b>Владелец НЕ указал явно задержку до первого такта</b> (в отличие
    /// от Стрелы «через 1 ход» и Бомбы «через 2 хода», у Лезвий раздел
    /// спецификации сразу описывает сам паттерн). <see cref="DelayTicks"/> =
    /// 1 — тот же минимальный дефолт, что и у остальных ловушек (общий
    /// принцип раздела traps.md: «угроза разворачивается через несколько
    /// ходов», не мгновенно), задокументирован здесь как предположение
    /// агента, mutable static.
    ///
    /// Внешний <see cref="Tick"/> нужно вызывать явно, один раз на ход
    /// игрока (тот же принцип, что и у <see cref="ArrowWaveTrapSystem"/>/
    /// <see cref="BombTrapSystem"/>).
    ///
    /// Одноразовая ловушка на триггер — повторный проход не запускает
    /// вторую параллельную последовательность (тот же приём, что у прочих
    /// систем этого семейства). Несколько одновременно активных
    /// последовательностей (разные триггеры) поддерживаются независимо.
    /// </summary>
    public sealed class BladeTactTrapSystem : IDisposable
    {
        // Не задано владельцем явно — см. doc-комментарий класса.
        public static int DelayTicks = 1;

        // "Один такт = 1 ход" — прямое требование владельца.
        public static int TactTicks = 1;

        // "По умолчанию два полных цикла" — прямое требование владельца.
        public static int CycleCount = 2;

        private sealed class ActiveBladeTact
        {
            public GridCoordinate Trigger;
            public int Row;
            public List<int[]> Sequence;
            public int NextTactIndex;
            public int[] PreviouslyArmedColumns;
        }

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly TurnBasedThreatScheduler _scheduler;
        private readonly HashSet<GridCoordinate> _firedTriggers = new HashSet<GridCoordinate>();

        // Ключ — координата триггера, тот же приём, что у BombTrapSystem:
        // каждый такт затрагивает НЕСКОЛЬКО столбцов одномоментно, поэтому
        // нет одной "естественной" координаты на шаг, как у ArrowWaveTrapSystem.
        private readonly Dictionary<GridCoordinate, ActiveBladeTact> _waitingTacts = new Dictionary<GridCoordinate, ActiveBladeTact>();

        private bool _disposed;

        public BladeTactTrapSystem(TunnelGrid grid, GridTraceTrail trail, TurnBasedThreatScheduler scheduler)
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
            if (!tile.BladeTactTargetRow.HasValue) return;
            if (!_firedTriggers.Add(coordinate)) return; // одноразовый триггер

            var wave = new ActiveBladeTact
            {
                Trigger = coordinate,
                Row = tile.BladeTactTargetRow.Value,
                Sequence = BuildFullSequence(_grid.Width),
                NextTactIndex = 0
            };
            ScheduleNextStep(wave, DelayTicks);
        }

        private void OnTileDue(GridCoordinate coordinate)
        {
            if (!_waitingTacts.TryGetValue(coordinate, out var wave)) return;
            _waitingTacts.Remove(coordinate);

            // Снимаем опасность с такта, опасного на ПРЕДЫДУЩЕМ шаге.
            if (wave.PreviouslyArmedColumns != null)
            {
                foreach (var column in wave.PreviouslyArmedColumns)
                    if (_grid.TryGetTile(new GridCoordinate(wave.Row, column), out var previousTile))
                        previousTile.ClearLethalTrap();
            }

            // "Пока игрок не покинет зону" — зона это ряд ловушки. Проверяем
            // ПОСЛЕ снятия предыдущего такта (уже безопасно), но ДО
            // активации следующего — досрочная остановка не должна армить
            // новый такт.
            if (_trail.CurrentPosition.Row != wave.Row) return;

            if (wave.NextTactIndex >= wave.Sequence.Count) return; // циклы закончились — этот тик был нужен только чтобы снять последний такт

            var columns = wave.Sequence[wave.NextTactIndex];
            foreach (var column in columns)
                _grid.GetOrCreateTile(new GridCoordinate(wave.Row, column)).TransitionToLethalTrap(LethalTrapType.BladeTact);
            wave.PreviouslyArmedColumns = columns;
            wave.NextTactIndex++;

            ScheduleNextStep(wave, TactTicks);
        }

        private void ScheduleNextStep(ActiveBladeTact wave, int ticksFromNow)
        {
            _waitingTacts[wave.Trigger] = wave;
            _scheduler.ScheduleActivation(wave.Trigger, ticksFromNow);
        }

        /// <summary>
        /// Полная развёрнутая последовательность тактов на <see cref="CycleCount"/>
        /// циклов: каждый такт — массив столбцов (1 или 2 элемента). Один
        /// цикл — "кольца" от края к центру и обратно наружу (см.
        /// doc-комментарий класса), центральное кольцо (одиночный столбец
        /// при нечётной ширине) не дублируется на развороте.
        /// </summary>
        private static List<int[]> BuildFullSequence(int width)
        {
            var rings = ComputeRingColumns(width);
            var oneCycle = new List<int[]>(rings);
            for (var i = rings.Count - 2; i >= 0; i--) oneCycle.Add(rings[i]);

            var full = new List<int[]>(oneCycle.Count * Math.Max(CycleCount, 0));
            for (var cycle = 0; cycle < CycleCount; cycle++) full.AddRange(oneCycle);
            return full;
        }

        /// <summary>
        /// Симметричные пары столбцов от краёв к центру: [0, width-1], [1,
        /// width-2], ... — при нечётной ширине последнее "кольцо" одиночный
        /// центральный столбец. Для width=5 (стандартная ширина тоннеля) даёт
        /// ровно [[0,4],[1,3],[2]] — то есть владельческие "1 и 5", "2 и 4",
        /// "3" в 0-индексации.
        /// </summary>
        private static List<int[]> ComputeRingColumns(int width)
        {
            var rings = new List<int[]>();
            var left = 0;
            var right = width - 1;
            while (left < right)
            {
                rings.Add(new[] { left, right });
                left++;
                right--;
            }
            if (left == right) rings.Add(new[] { left });
            return rings;
        }
    }
}
