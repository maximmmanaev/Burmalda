using System;
using Burmalda.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Burmalda.Movement
{
    /// <summary>
    /// Приём трейс-ввода пальцем (PRD 4.1): пока палец/курсор прижат,
    /// каждая новая плита под ним, соседняя текущей позиции, продвигает
    /// трейл игрока вперёд по тоннелю. Механика ввода не меняется
    /// относительно HTML-прототипа (legacy/burmolda_demo.html). Позиция
    /// курсора обрабатывается только при реальном изменении с прошлого
    /// обработанного кадра (#60) — иначе статичный зажатый клик, опрашиваемый
    /// каждый кадр, мог засчитать несколько шагов подряд на одной и той же
    /// позиции. Владеет жизненным циклом забега: <see cref="Grid"/>/<see cref="Trail"/>
    /// пересоздаются в <see cref="Restart"/> (мир перегенерируется заново, по
    /// прямому запросу владельца продукта) — зависимые системы (Decay,
    /// камера, debug-визуал, Burmalda.RunLifecycle) подписываются на
    /// <see cref="RunStarted"/>, чтобы пересобрать себя под новый забег.
    ///
    /// <see cref="IsPressed"/>/<see cref="CurrentScreenPosition"/>/
    /// <see cref="PressStarted"/> — публично читаемое состояние прижатия
    /// пальца, нужно смежным системам (см. <see cref="TunnelCameraController"/>:
    /// тап сбрасывает накопленный эдж-скролл (без мгновенного снапа
    /// позиции, см. doc-комментарий <see cref="PressStarted"/>), драг у
    /// верхней границы нижней трети экрана — автоскролл вперёд), не
    /// только этому классу для собственной логики хода.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridTraceInputController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private int _width = 5;

        private readonly PointerPositionChangeFilter _positionChangeFilter = new PointerPositionChangeFilter();

        private TunnelGrid _grid;
        private GridTraceTrail _trail;
        private WorldGridProjection _projection;

        /// <summary>Трейл текущего забега — для чтения смежными системами (визуал трейла, камера).</summary>
        public GridTraceTrail Trail => _trail;

        /// <summary>Сетка тоннеля текущего забега — нужна смежным системам (напр. Decay) для доступа к плитам трейла.</summary>
        public TunnelGrid Grid => _grid;

        /// <summary>Проекция координата↔мир текущего забега — нужна смежным системам (напр. камере) для перевода позиции трейла в мировые координаты.</summary>
        public WorldGridProjection Projection => _projection;

        /// <summary>
        /// Жив ли игрок в текущем забеге. Пока в проекте нет d20-испытания
        /// (Спринт 7, см. Burmalda.RunLifecycle) — false выставляется сразу
        /// при смерти (без броска) и держится, пока не вызван <see cref="Restart"/>.
        /// Ввод игнорируется, пока false (см. <see cref="Update"/>).
        /// </summary>
        public bool IsAlive { get; private set; } = true;

        /// <summary>Прижат ли сейчас палец/курсор. Обновляется каждый кадр в <see cref="Update"/>.</summary>
        public bool IsPressed { get; private set; }

        /// <summary>Последняя известная экранная позиция пальца/курсора — валидна, пока <see cref="IsPressed"/>.</summary>
        public Vector2 CurrentScreenPosition { get; private set; }

        /// <summary>
        /// Срабатывает сразу после того, как <see cref="Grid"/>/<see cref="Trail"/>
        /// (пере)созданы — и при первом запуске (<see cref="Awake"/>), и при
        /// каждом <see cref="Restart"/>. Зависимые контроллеры (Decay/камера/
        /// debug-визуал/RunLifecycle) должны пересобрать свои системы по
        /// этому событию, а не полагаться на разовую ленивую инициализацию.
        /// </summary>
        public event Action RunStarted;

        /// <summary>
        /// Срабатывает в момент перехода "не прижат" -> "прижат" — ровно
        /// один раз на новое нажатие, не каждый кадр, пока палец удерживается
        /// (для этого — <see cref="IsPressed"/>). Камера подписывается, чтобы
        /// сбросить накопленный эдж-скролл на каждый новый тап (см.
        /// <see cref="TunnelCameraFollow.ResetManualForwardOffset"/>) — БЕЗ
        /// мгновенного снапа позиции (убрано 2026-08-14, читалось на
        /// устройстве как рывок камеры на несколько клеток, см.
        /// doc-комментарий <see cref="TunnelCameraFollow.SnapToTarget"/>).
        /// </summary>
        public event Action PressStarted;

        /// <summary>
        /// Срабатывает, когда тап попадает ровно на ТЕКУЩУЮ позицию игрока
        /// (<c>Trail.CurrentPosition</c>) — на практике это в первую очередь
        /// самый первый тап по стартовой плите, ДО того, как игрок реально
        /// сдвинулся (прямой запрос владельца продукта, 2026-08-14): смена
        /// архитектуры камеры-интро с row-based интерполяции на явный
        /// тап-триггер + time-based твин (см. <see cref="TunnelCameraFollow.ConfirmRun"/>).
        /// Тап по своей же текущей позиции сам по себе НЕ засчитывается как
        /// обычный ход (см. <see cref="TryAdvanceAtScreenPosition"/> —
        /// <c>IsAdjacentTo</c> и так вернула бы false для координаты,
        /// равной самой себе, это не меняет обычное поведение трейла,
        /// только добавляет сигнал для случая, который раньше молча
        /// игнорировался). Камера подписывается, чтобы запустить твин в
        /// устоявшийся игровой режим, минуя top-down интро — без него
        /// игроку было бы обязательно свайпать вперёд, чтобы вообще увидеть
        /// обычный ракурс камеры.
        /// </summary>
        public event Action RunConfirmed;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
            _projection = new WorldGridProjection(_tileSize, _width);

            InitializeRun();
        }

        /// <summary>
        /// Помечает игрока мёртвым — дальнейший ввод игнорируется (см.
        /// <see cref="Update"/>) до вызова <see cref="Restart"/>. Вызывается
        /// снаружи (Burmalda.RunLifecycle) — сам контроллер не решает, когда
        /// наступает смерть, только подчиняется этому состоянию.
        /// </summary>
        public void MarkDead()
        {
            IsAlive = false;
        }

        /// <summary>
        /// Начинает новый забег: пересоздаёт <see cref="Grid"/>/<see cref="Trail"/>
        /// с нуля (мир перегенерируется заново, не переиспользуется — прямой
        /// запрос владельца продукта) и поднимает <see cref="RunStarted"/>.
        /// </summary>
        public void Restart()
        {
            InitializeRun();
        }

        private void InitializeRun()
        {
            _grid = new TunnelGrid(_width);
            var start = new GridCoordinate(0, _width / 2);
            _trail = new GridTraceTrail(_grid, start);
            _positionChangeFilter.Reset();
            IsAlive = true;

            RunStarted?.Invoke();
        }

        private void Update()
        {
            if (!IsAlive) return;

            var pointer = Pointer.current;
            var pressed = pointer != null && pointer.press.isPressed;

            if (pressed && !IsPressed) PressStarted?.Invoke(); // переход "не прижат" -> "прижат"
            IsPressed = pressed;

            if (!pressed)
            {
                _positionChangeFilter.Reset();
                return;
            }

            var screenPosition = pointer.position.ReadValue();
            CurrentScreenPosition = screenPosition;
            var hasChanged = _positionChangeFilter.HasChanged(screenPosition); // ЕДИНСТВЕННЫЙ вызов — метод мутирует состояние фильтра.

            if (!hasChanged) return;

            TryAdvanceAtScreenPosition(screenPosition);
        }

        private void TryAdvanceAtScreenPosition(Vector2 screenPosition)
        {
            if (_camera == null) return;

            var target = ResolveTappedTile(_camera, screenPosition);
            if (!target.HasValue) return; // тап мимо любой отрендеренной плитки (пустой пол/за пределами сетки) — тихо ничего не делаем

            if (target.Value == _trail.CurrentPosition)
            {
                RunConfirmed?.Invoke();
                return; // тап по своей же текущей позиции — кнопка, не обычный ход
            }

            _trail.TryAdvanceTo(target.Value);
        }

        /// <summary>
        /// Разрешает экранную точку тапа в координату ФИЗИЧЕСКИ
        /// отрендеренной плитки — <c>Physics.Raycast</c> против реальных
        /// коллайдеров (<see cref="TileVisualMarker"/>), не абстрактная
        /// математическая плоскость пола (issue 2026-08-14: раньше луч на
        /// бесконечную <c>Plane(Vector3.up, Vector3.zero)</c> давал
        /// какую-то мировую точку для ЛЮБОГО тапа, включая пустой пол за
        /// пределами раскрытой сетки — а дальше эта точка пересчитывалась
        /// в GridCoordinate, которая могла случайно пройти
        /// <c>grid.Contains</c>/<c>IsAdjacentTo</c> в <see cref="GridTraceTrail"/>,
        /// хотя визуально тайла там нет). Координата читается ПРЯМО с
        /// компонента попавшего коллайдера, не пересчитывается обратно из
        /// точки удара — обратный пересчёт был бы той же самой ошибкой.
        /// Null, если луч не попал ни в одну плитку (мимо/пустое место/
        /// коллайдер без <see cref="TileVisualMarker"/>) — вызывающий код
        /// должен в этом случае тихо ничего не делать.
        ///
        /// Статический и публичный — как и <see cref="PointerPositionChangeFilter"/>,
        /// тестируется напрямую, без Update()/реального Input System.
        /// </summary>
        public static GridCoordinate? ResolveTappedTile(Camera camera, Vector2 screenPosition)
        {
            if (camera == null) return null;

            var ray = camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit)) return null;

            var marker = hit.collider.GetComponent<TileVisualMarker>();
            return marker != null ? marker.Coordinate : (GridCoordinate?)null;
        }

        /// <summary>
        /// Фильтр повторной обработки одной и той же позиции курсора за
        /// соседние кадры (#60): пока палец зажат неподвижно, повторные кадры
        /// с той же экранной позицией не должны продвигать трейл заново.
        /// Выделен как отдельный проверяемый тип — сравнение позиций не
        /// зависит от Input System/сцены.
        /// </summary>
        public sealed class PointerPositionChangeFilter
        {
            private Vector2? _lastProcessedPosition;

            /// <summary>
            /// True, если <paramref name="position"/> отличается от последней
            /// обработанной позиции (или обработанной позиции ещё не было).
            /// В любом случае запоминает её как последнюю обработанную.
            /// </summary>
            public bool HasChanged(Vector2 position)
            {
                if (_lastProcessedPosition.HasValue && _lastProcessedPosition.Value == position) return false;

                _lastProcessedPosition = position;
                return true;
            }

            /// <summary>Сбрасывает запомненную позицию — вызывать, когда палец отпущен.</summary>
            public void Reset()
            {
                _lastProcessedPosition = null;
            }
        }
    }
}
