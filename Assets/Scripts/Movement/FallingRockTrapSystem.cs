using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Ловушка «Падающий камень» (docs/wiki/traps.md, issue #217) — аналога
    /// в проекте нет вообще (ни одна существующая система не превращает
    /// плиту в <see cref="Tile.IsBlocked"/> ВО ВРЕМЯ забега, см.
    /// docs/wiki/traps.md «Сверка с уже существующим кодом»). Построена на
    /// <see cref="TurnBasedThreatScheduler"/> (issue #212, свой экземпляр на
    /// систему): проход трейла через плиту-триггер (<see cref="Tile.IsFallingRockTrigger"/>,
    /// ещё не подключено ни к одному генератору сегментов — отдельная
    /// задача авторинга, здесь только механика) запускает отсчёт. Через
    /// <see cref="DelayTicks"/> ходов камень падает НА САМУ плиту-триггер
    /// (владелец: «на плиту-триггер падает камень», в отличие от прочих
    /// ловушек здесь нет отдельной координаты цели):
    /// <list type="bullet">
    /// <item>если игрок в этот момент стоит на ней (<see cref="GridTraceTrail.CurrentPosition"/>) —
    /// поднимается <see cref="PlayerCrushed"/>;</item>
    /// <item>если игрок ушёл — плита становится непроходимой НАВСЕГДА
    /// (<see cref="Tile.TransitionToBlocked"/>, тот же путь, что уже
    /// действует для стен, но во время забега, не на генерации).</item>
    /// </list>
    ///
    /// <b>Намеренно НЕ <see cref="Core.LethalTrapType"/></b>, в отличие от
    /// Стрелы/Бомбы/Лезвий: та абстракция построена вокруг «игрок ПЫТАЕТСЯ
    /// ШАГНУТЬ на уже опасную плиту» (<see cref="GridTraceTrail.TryAdvanceTo"/>
    /// проверяет <see cref="Tile.LethalTrap"/> ТОЛЬКО на попытке нового
    /// хода) — здесь игрок УЖЕ стоит на плите, когда та становится опасной,
    /// новый ход не совершается, и присвоение <c>Tile.LethalTrap</c> само
    /// по себе ничего бы не убило. Симметрично уже действующему паттерну
    /// <c>Decay.TrailDecaySystem.TileDestroyed</c> →
    /// <c>RunLifecycle.RunState.OnTileDestroyed</c> (обрушение плиты под
    /// ногами — тоже не новый ход, тоже отдельное событие с явной проверкой
    /// текущей позиции) — эта система сама решает по <see cref="GridTraceTrail.CurrentPosition"/>,
    /// какой из двух исходов наступил, и поднимает <see cref="PlayerCrushed"/>
    /// только для смертельного.
    ///
    /// <b>Подключение <see cref="PlayerCrushed"/> к <c>RunLifecycle.RunState</c>
    /// (чтобы событие реально убивало игрока) — вне скоупа этой задачи</b>,
    /// тот же класс отсрочки, что и авторинг шаблонов/MonoBehaviour-driver:
    /// без генерации, создающей <see cref="Tile.MarkFallingRockTrigger"/>,
    /// эта ловушка всё равно недостижима в реальном забеге, независимо от
    /// того, подключено ли событие к <c>RunState</c> — откладывать оба
    /// решения до общей задачи по вводу пяти типов ловушек в реальный забег
    /// логичнее, чем провести половину подключения сейчас.
    ///
    /// Одноразовая ловушка на триггер — повторный проход не запускает
    /// вторую параллельную активацию (тот же приём, что у прочих систем
    /// этого семейства).
    /// </summary>
    public sealed class FallingRockTrapSystem : IDisposable
    {
        // "Через 1 ход" — прямое требование владельца. Балансное число,
        // mutable static, не const — дебаг-панель (issue #217, критерий приёмки).
        public static int DelayTicks = 1;

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly TurnBasedThreatScheduler _scheduler;
        private readonly HashSet<GridCoordinate> _firedTriggers = new HashSet<GridCoordinate>();
        private bool _disposed;

        public FallingRockTrapSystem(TunnelGrid grid, GridTraceTrail trail, TurnBasedThreatScheduler scheduler)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _trail.PositionChanged += OnPositionChanged;
            _scheduler.TileDue += OnTileDue;
        }

        /// <summary>
        /// Срабатывает, когда камень падает на плиту, а игрок в этот момент
        /// на ней стоит (<see cref="GridTraceTrail.CurrentPosition"/>) — сама
        /// эта система не знает, что значит "убить игрока", решение и
        /// сообщение о смерти — за вызывающей стороной (см. doc-комментарий
        /// класса — подключение отложено в отдельную задачу).
        /// </summary>
        public event Action<GridCoordinate> PlayerCrushed;

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
            if (!tile.IsFallingRockTrigger) return;
            if (!_firedTriggers.Add(coordinate)) return; // одноразовый триггер

            _scheduler.ScheduleActivation(coordinate, DelayTicks);
        }

        private void OnTileDue(GridCoordinate coordinate)
        {
            // Свой экземпляр TurnBasedThreatScheduler на систему (issue
            // #212) — эта система регистрирует только координаты своих же
            // триггеров, поэтому TileDue здесь всегда "свой".
            if (_trail.CurrentPosition == coordinate)
            {
                PlayerCrushed?.Invoke(coordinate);
            }
            else
            {
                _grid.GetOrCreateTile(coordinate).TransitionToBlocked();
            }
        }
    }
}
