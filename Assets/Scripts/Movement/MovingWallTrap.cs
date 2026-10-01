using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Ловушка «Давилка»/«Стена слева»/«Стена справа» (BURMALDA Trap System
    /// Spec v0.1, TR-05/06/07). «Для gameplay-каталога это три разные
    /// ловушки, для кода — один reusable-компонент» (владелец) —
    /// <see cref="MovingWallMode"/> на плите-триггере выбирает узор
    /// закрытия, вся остальная механика общая для всех трёх.
    ///
    /// <b>Глобальный жизненный цикл Hidden → Detected → WaitingForExit →
    /// Triggered</b> — тот же приём, что уже у <see cref="ArrowWaveTrapSystem"/>/
    /// <see cref="BombTrapSystem"/>/<see cref="BladeTactTrapSystem"/>/
    /// <see cref="FallingRockTrapSystem"/>: приход на триггер
    /// (<see cref="Tile.MovingWallTargetRow"/>) раскрывает сигнатуру
    /// опасности, ничего не запускает («Detected»). Пока игрок стоит на
    /// плите — ничего не происходит («WaitingForExit»). Закрытие стартует
    /// («Triggered») строго в момент, когда игрок ПОКИДАЕТ плиту-триггер —
    /// «Само наступление на T ничего не запускает. Игрок выбирает клетку и
    /// сходит с T. В этот момент ряд F становится активным» (владелец).
    ///
    /// <b>Необратимость — ключевое отличие от волновых ловушек.</b>
    /// ArrowWave/BladeTact — временное состояние (плита становится
    /// смертельной и возвращается назад). Давилка — постоянное: закрытая
    /// колонка НИКОГДА не открывается заново, узор не проигрывается в
    /// обратную сторону (в отличие от <see cref="BladeTactTrapSystem"/>,
    /// чей узор «крайняя пара → центр → обратно наружу» — визуально похож,
    /// механически другой).
    ///
    /// <b>Три узора закрытия ряда <see cref="Tile.MovingWallTargetRow"/>
    /// (<see cref="BuildStages"/>):</b>
    /// <list type="bullet">
    /// <item><see cref="MovingWallMode.Both"/> («Давилка», TR-05) —
    /// симметричные пары от краёв к центру, тот же порядок колонок, что
    /// <see cref="BladeTactTrapSystem.ComputeRingColumns"/> (переиспользуется
    /// напрямую), но БЕЗ обратного хода — каждое кольцо закрывается ровно
    /// один раз, необратимо.</item>
    /// <item><see cref="MovingWallMode.FromLeft"/> («Стена слева», TR-06) —
    /// колонки по одной слева направо (0 → Width-1).</item>
    /// <item><see cref="MovingWallMode.FromRight"/> («Стена справа», TR-07) —
    /// колонки по одной справа налево (Width-1 → 0).</item>
    /// </list>
    /// Для ширины 5 все три узора в итоге закрывают ряд целиком — «Давилка»
    /// за 3 стадии, «Стена слева»/«справа» за 5. Разница не в исходе (весь
    /// ряд всё равно закрывается), а в форме давления на игрока по пути.
    ///
    /// <b>Стена настигает игрока (владелец: «Смерть/d20, как остальные
    /// ловушки»).</b> Каждая стадия закрытия проверяет каждую свою колонку:
    /// если это <see cref="GridTraceTrail.CurrentPosition"/> ПРЯМО СЕЙЧАС (не
    /// новый ход) — плита становится <see cref="LethalTrapType.MovingWallCrush"/>
    /// (<see cref="Tile.TransitionToLethalTrap"/>), разрешается стандартным
    /// d20 (<see cref="GridTraceTrail.CheckCurrentPositionForLethalTrap"/>) —
    /// тот же примитив, что уже используют <see cref="BombTrapSystem"/>/
    /// <see cref="FallingRockTrapSystem"/>. Любая другая колонка стадии —
    /// становится непроходимой навсегда (<see cref="Tile.TransitionToBlocked"/>,
    /// тот же путь, что у Падающего камня/Бомбы).
    ///
    /// <b>Critical Generation Rule (владелец): на affected row (<see cref="Tile.MovingWallTargetRow"/>)
    /// запрещены любые другие триггеры ловушек.</b> Проверяется на этапе
    /// авторинга структурным инвариантом <c>Generation.SegmentTemplate</c>
    /// (бросает исключение при конструировании шаблона, та же дисциплина,
    /// что <c>ValidateLeverGates</c>/<c>ValidateGateVault</c>), не здесь —
    /// эта система ничего не проверяет, доверяет уже провалидированному
    /// контенту. Причина правила — игровая, не техническая: Давилка уже
    /// насильно ограничивает варианты перемещения, вторая неизвестная
    /// угроза может создать ситуацию без корректного решения (Fairness
    /// Rules спецификации, п.6).
    ///
    /// Одноразовая ловушка на триггер — повторный проход не запускает
    /// второе параллельное закрытие (тот же приём, что у прочих систем
    /// этого семейства). Несколько одновременно активных закрытий (разные
    /// триггеры) поддерживаются независимо друг от друга.
    /// </summary>
    public sealed class MovingWallTrap : IDisposable
    {
        // Единственный параметр скорости закрытия — время между стадиями
        // (владелец: "Меняется только скорость. Ранний Ярус — стены идут
        // медленно, поздний — быстро откусывают клетки"). Дефолт 0.3с — тот
        // же ориентир, что у остальных систем этого семейства, предположение
        // агента, не решение владельца. Mutable static — дебаг-панель.
        public static float StepSeconds = 0.3f;

        private sealed class ActiveClosure
        {
            public GridCoordinate Trigger;
            public int Row;
            public List<int[]> Stages;
            public int NextStageIndex;
        }

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly RealTimeThreatScheduler _scheduler;
        private readonly HashSet<GridCoordinate> _firedTriggers = new HashSet<GridCoordinate>();

        // Ключ — координата триггера, тот же приём, что у BladeTactTrapSystem:
        // каждая стадия затрagивает НЕСКОЛЬКО колонок одномоментно, нет
        // одной "естественной" координаты на шаг, как у ArrowWaveTrapSystem.
        private readonly Dictionary<GridCoordinate, ActiveClosure> _waitingClosures = new Dictionary<GridCoordinate, ActiveClosure>();

        // "WaitingForExit" — плита, на которой игрок стоит ПРЯМО СЕЙЧАС и
        // которая несёт ещё не сработавший триггер (см. doc-комментарий
        // класса). Null, если игрок не стоит на таком триггере.
        private GridCoordinate? _pendingTriggerCoordinate;

        private bool _disposed;

        public MovingWallTrap(TunnelGrid grid, GridTraceTrail trail, RealTimeThreatScheduler scheduler)
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
            // "Triggered" — только что покинули плиту с висящим триггером
            // (см. doc-комментарий класса). Проверяется ПЕРЕД "Detected"
            // ниже — если игрок шагнул с одного триггера сразу на другой,
            // старый обязан активироваться раньше, чем новый встанет в
            // ожидание (тот же приём, что у ArrowWaveTrapSystem).
            if (_pendingTriggerCoordinate.HasValue && _pendingTriggerCoordinate.Value != coordinate)
            {
                ActivateClosure(_pendingTriggerCoordinate.Value);
                _pendingTriggerCoordinate = null;
            }

            // "Detected" — пришли на ещё не сработавший триггер: раскрываем
            // сигнатуру опасности и встаём в ожидание ухода, ничего не
            // запускаем.
            if (!_grid.TryGetTile(coordinate, out var tile)) return;
            if (!tile.MovingWallTargetRow.HasValue) return;
            if (_firedTriggers.Contains(coordinate)) return; // уже сработал раньше

            _pendingTriggerCoordinate = coordinate;
            tile.RevealDangerSignature();
        }

        /// <summary>"Triggered" — запускает закрытие для триггера в <paramref name="coordinate"/>. Вызывается только из <see cref="OnPositionChanged"/> в момент ухода игрока с этой плиты.</summary>
        private void ActivateClosure(GridCoordinate coordinate)
        {
            if (!_grid.TryGetTile(coordinate, out var tile) || !tile.MovingWallTargetRow.HasValue) return;
            if (!_firedTriggers.Add(coordinate)) return; // одноразовый триггер

            var closure = new ActiveClosure
            {
                Trigger = coordinate,
                Row = tile.MovingWallTargetRow.Value,
                Stages = BuildStages(tile.MovingWallTriggerMode.Value, _grid.Width),
                NextStageIndex = 0
            };
            ScheduleNextStage(closure, StepSeconds);
        }

        private void OnTileDue(GridCoordinate coordinate)
        {
            if (!_waitingClosures.TryGetValue(coordinate, out var closure)) return;
            _waitingClosures.Remove(coordinate);

            if (closure.NextStageIndex >= closure.Stages.Count)
                return; // закрытие уже завершено — лишний тик не нужен (в отличие от волн, снимать здесь нечего — закрытие необратимо)

            var columns = closure.Stages[closure.NextStageIndex];
            var playerCaught = false;
            foreach (var column in columns)
            {
                var target = new GridCoordinate(closure.Row, column);
                var targetTile = _grid.GetOrCreateTile(target);

                if (_trail.CurrentPosition == target)
                {
                    targetTile.TransitionToLethalTrap(LethalTrapType.MovingWallCrush);
                    playerCaught = true;
                }
                else
                {
                    targetTile.TransitionToBlocked();
                }
            }
            // Игрок мог уже стоять на настигнутой плите, не совершая новый
            // ход — TryAdvanceTo тут ни при чём, см. doc-комментарий
            // GridTraceTrail.CheckCurrentPositionForLethalTrap (тот же
            // примитив, что уже использует BombTrapSystem/FallingRockTrapSystem).
            if (playerCaught) _trail.CheckCurrentPositionForLethalTrap();

            closure.NextStageIndex++;
            if (closure.NextStageIndex < closure.Stages.Count)
                ScheduleNextStage(closure, StepSeconds);
        }

        private void ScheduleNextStage(ActiveClosure closure, float secondsFromNow)
        {
            _waitingClosures[closure.Trigger] = closure;
            _scheduler.ScheduleActivation(closure.Trigger, secondsFromNow);
        }

        /// <summary>
        /// Развёрнутая последовательность стадий закрытия — каждая стадия
        /// список колонок, закрывающихся одномоментно. См. doc-комментарий
        /// класса про три узора.
        /// </summary>
        private static List<int[]> BuildStages(MovingWallMode mode, int width) => mode switch
        {
            MovingWallMode.Both => BladeTactTrapSystem.ComputeRingColumns(width),
            MovingWallMode.FromLeft => BuildSweep(width, leftToRight: true),
            MovingWallMode.FromRight => BuildSweep(width, leftToRight: false),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Неизвестный режим MovingWallMode.")
        };

        private static List<int[]> BuildSweep(int width, bool leftToRight)
        {
            var stages = new List<int[]>(width);
            for (var i = 0; i < width; i++)
            {
                var column = leftToRight ? i : width - 1 - i;
                stages.Add(new[] { column });
            }
            return stages;
        }
    }
}
