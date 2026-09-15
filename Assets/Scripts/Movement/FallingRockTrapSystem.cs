using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Ловушка «Падающий камень» (docs/wiki/traps.md, issue #217).
    ///
    /// <b>Новая спецификация (владелец, задача 4 спринта «Стены вместо
    /// ловушек») — ловушка не срабатывала по двум подтверждённым
    /// причинам.</b>
    /// <list type="bullet">
    /// <item><see cref="PlayerCrushed"/> не имел ни одного подписчика —
    /// событие объявлялось и вызывалось, но уходило в пустоту. Отложенное
    /// подключение к <c>RunLifecycle.RunState</c> (ранее "вне скоупа
    /// задачи") подключено этой же задачей — см.
    /// <c>RunState.ReportFallingRockCrush</c>/<c>Movement.TrapSystemsController.FallingRockPlayerCrushed</c>/
    /// <c>Bootstrap.RunBootstrap.EnsureControllersWired</c>.</item>
    /// <item>Структурная: раньше камень падал на ТУ ЖЕ плиту, на которую
    /// наступил игрок (<c>_trail.CurrentPosition == coordinate</c>, где
    /// <c>coordinate</c> — плита-триггер). Шаг ~0.3с, задержка ~1с — игрок
    /// почти всегда уже уходил дальше к моменту падения, срабатывала ветка
    /// "плита позади становится непроходимой" — позади, где игрок не
    /// смотрит, и где пол и так осыпается распадом. Ловушка дублировала
    /// распад, не создавала риска.</item>
    /// </list>
    ///
    /// Новое поведение: камень падает на плиту ВПЕРЕДИ
    /// (<see cref="Tile.FallingRockTargetCoordinate"/> плиты-триггера, не
    /// координата самого триггера) — целевая плита подсвечивается заранее
    /// (<see cref="Tile.BeginFallingRockWarning"/>, тот же приём, что уже
    /// применяет Бомба) — однозначно видно, куда упадёт, ЗАДОЛГО до
    /// падения, не внезапно под ногами.
    ///
    /// <b>Реальное время, не ходы (владелец, 2026-09-14, issue #254).</b> См.
    /// тот же аргумент в doc-комментарии <see cref="BombTrapSystem"/> —
    /// отсчёт до падения камня не должен зависеть от того, продолжает ли
    /// игрок идти. Построена на <see cref="RealTimeThreatScheduler"/>:
    /// проход трейла через плиту-триггер (<see cref="Tile.FallingRockTargetCoordinate"/>,
    /// заданный на генерации — <c>Generation.SegmentRowProvider</c>/
    /// <c>Core.TunnelObstacleGenerator</c>) запускает отсчёт и сразу
    /// поднимает предупреждение на целевой плите. Через
    /// <see cref="DelaySeconds"/> секунд:
    /// <list type="bullet">
    /// <item>если игрок в этот момент стоит на целевой плите
    /// (<see cref="GridTraceTrail.CurrentPosition"/>) — поднимается
    /// <see cref="PlayerCrushed"/>;</item>
    /// <item>если игрок не там — целевая плита становится непроходимой
    /// НАВСЕГДА (<see cref="Tile.TransitionToBlocked"/>, тот же путь, что
    /// уже действует для стен, но во время забега, не на генерации).</item>
    /// </list>
    ///
    /// <b>Намеренно НЕ <see cref="Core.LethalTrapType"/></b>, в отличие от
    /// Стрелы/Бомбы/Лезвий: та абстракция построена вокруг «игрок ПЫТАЕТСЯ
    /// ШАГНУТЬ на уже опасную плиту» (<see cref="GridTraceTrail.TryAdvanceTo"/>
    /// проверяет <see cref="Tile.LethalTrap"/> ТОЛЬКО на попытке нового
    /// хода) — целевая плита ловушки может стать опасной, пока игрок УЖЕ
    /// стоит на ней (например, отступил назад на уже подсвеченную клетку и
    /// ждёт), новый ход не совершается, и присвоение <c>Tile.LethalTrap</c>
    /// само по себе ничего бы не убило. Симметрично уже действующему
    /// паттерну <c>Decay.TrailDecaySystem.TileDestroyed</c> →
    /// <c>RunLifecycle.RunState.OnTileDestroyed</c> (обрушение плиты под
    /// ногами — тоже не новый ход, тоже отдельное событие с явной проверкой
    /// текущей позиции) — эта система сама решает по <see cref="GridTraceTrail.CurrentPosition"/>,
    /// какой из двух исходов наступил, и поднимает <see cref="PlayerCrushed"/>
    /// только для смертельного.
    ///
    /// Одноразовая ловушка на триггер — повторный проход не запускает
    /// вторую параллельную активацию (тот же приём, что у прочих систем
    /// этого семейства).
    /// </summary>
    public sealed class FallingRockTrapSystem : IDisposable
    {
        // "Через 1 ход" в исходной спецификации ходов — переведено в
        // секунды тем же ориентиром, что и остальные (0.3с ≈ 1 ход,
        // docs/wiki/traps.md), issue #254. Балансное число, mutable static,
        // не const — дебаг-панель (критерий приёмки).
        public static float DelaySeconds = 0.3f;

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly RealTimeThreatScheduler _scheduler;
        private readonly HashSet<GridCoordinate> _firedTriggers = new HashSet<GridCoordinate>();
        private bool _disposed;

        public FallingRockTrapSystem(TunnelGrid grid, GridTraceTrail trail, RealTimeThreatScheduler scheduler)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _trail.PositionChanged += OnPositionChanged;
            _scheduler.TileDue += OnTileDue;
        }

        /// <summary>
        /// Срабатывает, когда камень падает на целевую плиту, а игрок в
        /// этот момент на ней стоит (<see cref="GridTraceTrail.CurrentPosition"/>).
        /// Сама эта система не знает, что значит "убить игрока" — решение и
        /// сообщение о смерти за вызывающей стороной, см.
        /// <c>RunLifecycle.RunState.ReportFallingRockCrush</c> (подключено
        /// задачей 4 спринта «Стены вместо ловушек» через
        /// <c>Movement.TrapSystemsController.FallingRockPlayerCrushed</c>/
        /// <c>Bootstrap.RunBootstrap</c> — <c>Movement</c> не ссылается на
        /// <c>RunLifecycle</c>, см. asmdef).
        /// </summary>
        public event Action<GridCoordinate> PlayerCrushed;

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
            if (!tile.FallingRockTargetCoordinate.HasValue) return;
            if (!_firedTriggers.Add(coordinate)) return; // одноразовый триггер

            var target = tile.FallingRockTargetCoordinate.Value;
            // "Целевая плита подсвечивается заранее — однозначно видно,
            // куда упадёт" (владелец) — предупреждение поднимается сразу
            // при активации, не ждёт истечения задержки (тот же момент, что
            // у предупреждения Бомбы).
            _grid.GetOrCreateTile(target).BeginFallingRockWarning();
            _scheduler.ScheduleActivation(target, DelaySeconds);
        }

        private void OnTileDue(GridCoordinate coordinate)
        {
            // Свой экземпляр RealTimeThreatScheduler на систему (issue
            // #212/#254) — эта система регистрирует только координаты своих
            // же целей, поэтому TileDue здесь всегда "своя" целевая плита
            // (не координата триггера — см. doc-комментарий класса).
            var tile = _grid.GetOrCreateTile(coordinate);
            tile.EndFallingRockWarning();

            if (_trail.CurrentPosition == coordinate)
            {
                PlayerCrushed?.Invoke(coordinate);
            }
            else
            {
                tile.TransitionToBlocked();
            }
        }
    }
}
