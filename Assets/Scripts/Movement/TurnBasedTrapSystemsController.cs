using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Тикает пять систем ловушек (issue #212, Стрела/Бомба/Лезвия/Падающий
    /// камень/Лава — issues #213–#217) на каждый забег. Баг с устройства
    /// (владелец, 2026-09-04, «новых ловушек в игре нет») — все пять были
    /// написаны и покрыты тестами, но ни разу не собирались в реальной
    /// сцене: не было ни символа шаблона (<see cref="Generation.SegmentTileType.ArrowWaveTrigger"/>
    /// и остальные четыре — отдельный фикс), ни этого Controller'а. Оба
    /// нужны вместе — символ без тикающей системы ничего не сделает, а
    /// система без символа никогда не увидит триггер.
    ///
    /// Добавляется динамически через <c>Bootstrap.RunBootstrap</c>, а не
    /// вручную в Editor — MonoBehaviour-driver, который не зависит от того,
    /// вспомнил ли кто-то добавить компонент на сцену.
    ///
    /// <b>Имя устарело, оставлено намеренно (владелец, 2026-09-14, issue
    /// #254 — «Волновые ловушки переходят на реальное время»).</b> Раньше
    /// ВСЕ пять систем тикались ровно на каждый шаг игрока
    /// (<see cref="GridTraceTrail.PositionChanged"/>) — из-за этого волна
    /// (Стрела/Лезвия/Лава) физически не могла догнать игрока: она
    /// продвигалась ровно тогда же, когда шагал игрок, то есть была
    /// безопасна по построению, а не по игровому замыслу. Теперь этот
    /// Controller тикает ДВУМЯ разными способами:
    /// <list type="bullet">
    /// <item><b>На ходах</b> (без изменений) — <see cref="BombTrapSystem"/>/
    /// <see cref="FallingRockTrapSystem"/>, задержка до активации ("через 2
    /// хода взорвётся"/"через 1 ход упадёт камень") остаётся тактами ходов,
    /// свой <see cref="TurnBasedThreatScheduler"/> на систему, тикается из
    /// <see cref="TickTurnBasedSystems"/> на <see cref="GridTraceTrail.PositionChanged"/>.</item>
    /// <item><b>В реальном времени</b> (issue #254) — <see cref="ArrowWaveTrapSystem"/>/
    /// <see cref="BladeTactTrapSystem"/>/<see cref="LavaWaveTrapSystem"/>,
    /// движение уже активной волны не завязано на шаги игрока, свой
    /// <see cref="RealTimeThreatScheduler"/> на систему, тикается из
    /// <see cref="Update"/> с <c>Time.deltaTime</c>.</item>
    /// </list>
    /// Переименование класса оставлено на будущее (класс тикает и то, и
    /// другое — ни "TurnBased", ни "RealTime" по отдельности точным именем
    /// уже не будут) — минимальная правка сейчас, а не churn по всем
    /// ссылкам (<c>Bootstrap.RunBootstrap.TurnBasedTraps</c>) ради
    /// переименования без функциональной необходимости.
    ///
    /// <b>Порядок подписки на <see cref="GridTraceTrail.PositionChanged"/>
    /// важен для ходовых систем</b>: каждая из пяти систем сама
    /// подписывается на это событие в своём конструкторе (регистрирует
    /// отложенную активацию, если игрок только что встал на триггер). Этот
    /// Controller подписывает <see cref="TickTurnBasedSystems"/> ПОСЛЕ того,
    /// как все пять уже сконструированы — тикает ходовые планировщики
    /// строго ПОСЛЕ регистрации новой активации на этом же ходу, а не до
    /// неё (иначе только что зарегистрированная активация потеряла бы один
    /// тик сразу же, тот же класс гонки порядка подписки, что уже ловили на
    /// двух генераторах тоннеля, задача «двойные флаги на плитах»). Для
    /// трёх реал-таймовых систем этой гонки нет по конструкции —
    /// <see cref="Update"/> тикает независимо от <see cref="GridTraceTrail.PositionChanged"/>
    /// на своей собственной кадровой частоте.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TurnBasedTrapSystemsController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;

        private GridTraceTrail _trail;

        // Реальное время (issue #254) — тикаются из Update().
        private ArrowWaveTrapSystem _arrowWave;
        private BladeTactTrapSystem _bladeTact;
        private LavaWaveTrapSystem _lavaWave;

        // Ходы, без изменений — тикаются из TickTurnBasedSystems на PositionChanged.
        private BombTrapSystem _bomb;
        private FallingRockTrapSystem _fallingRock;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
        }

        private void OnEnable()
        {
            if (_input != null) _input.RunStarted += HandleRunStarted;
        }

        private void OnDisable()
        {
            if (_input != null) _input.RunStarted -= HandleRunStarted;
            DisposeAll();
        }

        private void Update()
        {
            // До первого Rebuild() (или после DisposeAll) реал-таймовые
            // системы ещё/уже не существуют — не тикаем несуществующее.
            if (_arrowWave == null) return;

            var deltaSeconds = Time.deltaTime;
            _arrowWave.Tick(deltaSeconds);
            _bladeTact.Tick(deltaSeconds);
            _lavaWave.Tick(deltaSeconds);
        }

        private void HandleRunStarted() => Rebuild();

        private void Rebuild()
        {
            DisposeAll();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;

            _trail = _input.Trail;
            var grid = _input.Grid;

            // Каждая система — свой независимый планировщик (тот же принцип,
            // что был у отдельных экземпляров старых, удалённых 2026-09-05
            // контроллеров ловушек реального времени) — конструкторы
            // подписываются на _trail.PositionChanged здесь, ДО
            // TickTurnBasedSystems ниже (см. doc-комментарий класса про
            // порядок подписки — актуально только для ходовых систем).
            _arrowWave = new ArrowWaveTrapSystem(grid, _trail, new RealTimeThreatScheduler());
            _bomb = new BombTrapSystem(grid, _trail, new TurnBasedThreatScheduler());
            _bladeTact = new BladeTactTrapSystem(grid, _trail, new RealTimeThreatScheduler());
            _fallingRock = new FallingRockTrapSystem(grid, _trail, new TurnBasedThreatScheduler());
            _lavaWave = new LavaWaveTrapSystem(grid, _trail, new RealTimeThreatScheduler());

            _trail.PositionChanged += TickTurnBasedSystems;
        }

        private void TickTurnBasedSystems(Core.GridCoordinate coordinate)
        {
            _bomb.Tick();
            _fallingRock.Tick();
        }

        private void DisposeAll()
        {
            if (_trail != null) _trail.PositionChanged -= TickTurnBasedSystems;
            _trail = null;

            _arrowWave?.Dispose();
            _arrowWave = null;
            _bomb?.Dispose();
            _bomb = null;
            _bladeTact?.Dispose();
            _bladeTact = null;
            _fallingRock?.Dispose();
            _fallingRock = null;
            _lavaWave?.Dispose();
            _lavaWave = null;
        }
    }
}
