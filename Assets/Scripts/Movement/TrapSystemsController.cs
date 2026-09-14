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
    /// <b>Переименован из TurnBasedTrapSystemsController (владелец,
    /// 2026-09-14, issue #254, второй раунд): «ловушки в такт шагам это
    /// ошибка в ТЗ, никаких ловушек в такт быть не должно, только
    /// тайминги».</b> Первый раунд этой задачи (issue #254) переводил на
    /// реальное время только три волновые системы (Стрела/Лезвия/Лава),
    /// оставляя Бомбу/Падающий камень тикать на шагах игрока — рассуждение
    /// было "у них нет волны, которую нужно догонять, задержка до
    /// одномоментного события — другая сущность". Живой плейтест показал ту
    /// же проблему в другой форме: если игрок наводится на раскрытый
    /// триггер (сигнатура/вибрация срабатывают), отпускает, встаёт на плиту
    /// триггера и дальше просто СТОИТ, разглядывая — отсчёт до взрыва/камня
    /// не шёл вообще, потому что планировщик тикался только на его
    /// собственные ходы. Теперь ВСЕ ПЯТЬ систем тикаются из <see cref="Update"/>
    /// с <c>Time.deltaTime</c>, ни одна не завязана на
    /// <see cref="GridTraceTrail.PositionChanged"/> для продвижения времени
    /// (обнаружение самого триггера по-прежнему висит на нём — см.
    /// <c>OnPositionChanged</c> каждой системы, это МОМЕНТ активации, не
    /// единица времени после неё).
    ///
    /// <b>Ленивая самопроверка в <see cref="Update"/> (владелец, 2026-09-14,
    /// плейтест «ловушки вообще пропали», issue #256) — баг с устройства, не
    /// гипотеза.</b> <c>Bootstrap.RunBootstrap.EnsureControllersWired</c>
    /// добавляет этот Controller на сцену ПОЗЖЕ, чем
    /// <see cref="GridTraceInputController.Awake"/> синхронно поднимает
    /// самый первый <see cref="GridTraceInputController.RunStarted"/> — тот
    /// же класс гонки, что уже был найден и решён для лоадаута артефактов
    /// (см. <c>RunBootstrap.EnsureLoadoutReady</c>), но здесь решён не был.
    /// Без самопроверки Controller безвозвратно пропускал единственное
    /// событие, которое должно было его построить — все пять систем ловушек
    /// молча оставались null на весь забег. Подтверждено логом на реальном
    /// устройстве: конструирование систем ни разу не происходило за весь
    /// забег, хотя <see cref="Update"/> честно тикал каждый кадр. Тот же
    /// приём "ленивая инициализация", что уже у <c>Boss.BossController.Update</c>/
    /// <c>Generation.SegmentGenerationController.EnsureBuilt</c> — этот
    /// класс был единственным исключением из уже устоявшегося паттерна.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrapSystemsController : MonoBehaviour
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

        // Тикает все пять систем реальным временем (issue #254) и заодно
        // служит ленивой самопроверкой (issue #256, см. doc-комментарий
        // класса) — дёшево не-op на кадрах, где уже построено или ещё не
        // готово.
        private void Update()
        {
            if (_arrowWave == null)
            {
                if (IsReady()) Rebuild();
                return;
            }

            var deltaSeconds = Time.deltaTime;
            _arrowWave.Tick(deltaSeconds);
            _bomb.Tick(deltaSeconds);
            _bladeTact.Tick(deltaSeconds);
            _fallingRock.Tick(deltaSeconds);
            _lavaWave.Tick(deltaSeconds);
        }

        private bool IsReady() => _input != null && _input.Grid != null && _input.Trail != null;

        private void HandleRunStarted() => Rebuild();

        private void Rebuild()
        {
            DisposeAll();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;

            _trail = _input.Trail;
            var grid = _input.Grid;

            // Каждая система — свой независимый RealTimeThreatScheduler
            // (тот же принцип, что был у отдельных экземпляров старых,
            // удалённых 2026-09-05 контроллеров ловушек реального времени —
            // см. doc-комментарий RealTimeThreatScheduler). Порядок
            // подписки на PositionChanged (внутри конструктора каждой
            // системы) больше ни на что не влияет — раньше это было важно
            // для TickAllSystems, тикавшего на том же событии; теперь
            // продвижение времени полностью отвязано от PositionChanged
            // (см. Update() выше), гонки, которая была описана в старом
            // doc-комментарии этого класса, больше не существует по
            // конструкции.
            _arrowWave = new ArrowWaveTrapSystem(grid, _trail, new RealTimeThreatScheduler());
            _bomb = new BombTrapSystem(grid, _trail, new RealTimeThreatScheduler());
            _bladeTact = new BladeTactTrapSystem(grid, _trail, new RealTimeThreatScheduler());
            _fallingRock = new FallingRockTrapSystem(grid, _trail, new RealTimeThreatScheduler());
            _lavaWave = new LavaWaveTrapSystem(grid, _trail, new RealTimeThreatScheduler());
        }

        private void DisposeAll()
        {
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
