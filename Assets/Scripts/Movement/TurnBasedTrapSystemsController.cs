using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Тикает пять систем ловушек на <see cref="TurnBasedThreatScheduler"/>
    /// (issue #212, Стрела/Бомба/Лезвия/Падающий камень/Лава — issues
    /// #213–#217) на каждый забег. Баг с устройства (владелец, 2026-09-04,
    /// «новых ловушек в игре нет») — все пять были написаны и покрыты
    /// тестами, но ни разу не собирались в реальной сцене: не было ни
    /// символа шаблона (<see cref="Generation.SegmentTileType.ArrowWaveTrigger"/>
    /// и остальные четыре — отдельный фикс), ни этого Controller'а. Оба
    /// нужны вместе — символ без тикающей системы ничего не сделает, а
    /// система без символа никогда не увидит триггер.
    ///
    /// По паттерну <see cref="TimedTrapController"/> (тикается по ходу, а не
    /// по кадру real-time) — но здесь "ход" это <see cref="GridTraceTrail.PositionChanged"/>,
    /// а не <c>Time.deltaTime</c> (см. <see cref="TurnBasedThreatScheduler"/>).
    /// Добавляется динамически через <c>Bootstrap.RunBootstrap</c>, а не
    /// вручную в Editor — ровно то, чего не хватало: MonoBehaviour-driver,
    /// который не зависит от того, вспомнил ли кто-то добавить компонент на
    /// сцену.
    ///
    /// <b>Порядок подписки на <see cref="GridTraceTrail.PositionChanged"/>
    /// важен</b>: каждая из пяти систем сама подписывается на это событие в
    /// своём конструкторе (регистрирует отложенную активацию, если игрок
    /// только что встал на триггер). Этот Controller подписывает
    /// <see cref="TickAllSystems"/> ПОСЛЕ того, как все пять уже
    /// сконструированы — тикает планировщики строго ПОСЛЕ регистрации новой
    /// активации на этом же ходу, а не до неё (иначе только что
    /// зарегистрированная активация потеряла бы один тик сразу же, тот же
    /// класс гонки порядка подписки, что уже ловили на двух генераторах
    /// тоннеля, задача «двойные флаги на плитах»).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TurnBasedTrapSystemsController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;

        private GridTraceTrail _trail;
        private ArrowWaveTrapSystem _arrowWave;
        private BombTrapSystem _bomb;
        private BladeTactTrapSystem _bladeTact;
        private FallingRockTrapSystem _fallingRock;
        private LavaWaveTrapSystem _lavaWave;

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

        private void HandleRunStarted() => Rebuild();

        private void Rebuild()
        {
            DisposeAll();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;

            _trail = _input.Trail;
            var grid = _input.Grid;

            // Каждая система — свой независимый TurnBasedThreatScheduler
            // (тот же принцип, что отдельные экземпляры TimedTrapSystem/
            // ExplosiveTrapArmingSystem на забег, см. doc-комментарий
            // TurnBasedThreatScheduler) — конструкторы подписываются на
            // _trail.PositionChanged здесь, ДО TickAllSystems ниже.
            _arrowWave = new ArrowWaveTrapSystem(grid, _trail, new TurnBasedThreatScheduler());
            _bomb = new BombTrapSystem(grid, _trail, new TurnBasedThreatScheduler());
            _bladeTact = new BladeTactTrapSystem(grid, _trail, new TurnBasedThreatScheduler());
            _fallingRock = new FallingRockTrapSystem(grid, _trail, new TurnBasedThreatScheduler());
            _lavaWave = new LavaWaveTrapSystem(grid, _trail, new TurnBasedThreatScheduler());

            _trail.PositionChanged += TickAllSystems;
        }

        private void TickAllSystems(Core.GridCoordinate coordinate)
        {
            _arrowWave.Tick();
            _bomb.Tick();
            _bladeTact.Tick();
            _fallingRock.Tick();
            _lavaWave.Tick();
        }

        private void DisposeAll()
        {
            if (_trail != null) _trail.PositionChanged -= TickAllSystems;
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
