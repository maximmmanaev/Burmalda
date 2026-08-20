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
    /// тап сбрасывает накопленный эдж-скролл на каждый новый тап (см.
    /// <see cref="TunnelCameraFollow.ResetManualForwardOffset"/>) — БЕЗ
    /// мгновенного снапа позиции (убрано 2026-08-14, читалось на
    /// устройстве как рывок камеры на несколько клеток, см.
    /// doc-комментарий <see cref="TunnelCameraFollow.SnapToTarget"/>).
    ///
    /// <b>Issue #157, спайк "дискретный ввод" — ТОЛЬКО в этой ветке
    /// (spike/discrete-input-dwell), НИКОГДА не мержится в main без
    /// отдельного явного решения владельца продукта (PRD v5 4.1 явно
    /// фиксирует протяжку как механику ввода — смена механиков это
    /// PRD-уровневое решение, не инженерное).</b> <see cref="DwellEnabled"/>
    /// переключает между двумя РАЗНЫМИ входными механиками на одном и том
    /// же трейле: протяжка (как выше, оригинал) или "примеривание" —
    /// удержание пальца на соседней плите показывает её (не двигая трейл),
    /// отпускание фиксирует шаг, шаг занимает <see cref="StepDurationSeconds"/>
    /// секунд, ввод на это время заблокирован. Обоснование и известные
    /// проблемы — see issue #157 и `docs/evidence/issue-157/` (видео +
    /// честный список).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridTraceInputController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private int _width = 5;

        // Issue #157 — см. doc-комментарий класса. По умолчанию ВКЛЮЧЕН на
        // этой ветке (смысл спайка — дать пощупать именно дискретный ввод),
        // в отличие от обычных тумблеров этого проекта (которые по
        // умолчанию выключены для побайтовой совместимости) — здесь
        // совместимость не нужна, ветка никогда не мержится.
        [SerializeField] private bool _dwellEnabled = true;
        [SerializeField] private float _stepDurationSeconds = 0.32f; // середина требуемого диапазона 0.25-0.4с

        private const float MinStepDurationSeconds = 0.25f;
        private const float MaxStepDurationSeconds = 0.4f;

        private readonly PointerPositionChangeFilter _positionChangeFilter = new PointerPositionChangeFilter();

        private TunnelGrid _grid;
        private GridTraceTrail _trail;
        private WorldGridProjection _projection;

        // Issue #157 — состояние "примеривания".
        private GridCoordinate? _dwellTarget;
        private bool _confirmFiredForCurrentDwell;
        private float _stepLockRemainingSeconds;
        private Renderer _highlightedRenderer;
        private Color _highlightedRendererOriginalColor;
        private static readonly Color DwellHighlightColor = new Color(1f, 0.95f, 0.4f, 1f); // тёплый жёлтый — отличим от игровых цветов (зелёный/фиолетовый/чёрный)

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
        /// Issue #157: тумблер механики ввода — публично для debug-панели
        /// (<c>DiscreteInputDebugPanel</c>). true = "примеривание" (дефолт на
        /// этой ветке), false = протяжка оригинала issue #60/#61.
        /// </summary>
        public bool DwellEnabled
        {
            get => _dwellEnabled;
            set
            {
                if (_dwellEnabled == value) return;
                _dwellEnabled = value;
                CancelDwell(); // смена механики на лету — не оставляем зависший превью/лок от другого режима
            }
        }

        /// <summary>Issue #157: длительность шага (сек), публично для debug-панели. Зажимается в [0.25, 0.4].</summary>
        public float StepDurationSeconds
        {
            get => _stepDurationSeconds;
            set => _stepDurationSeconds = Mathf.Clamp(value, MinStepDurationSeconds, MaxStepDurationSeconds);
        }

        /// <summary>
        /// Issue #157: плита, которую сейчас "примеривает" игрок (палец
        /// зажат на соседней валидной плите, шаг ещё НЕ зафиксирован) — null,
        /// если палец не зажат, зажат мимо валидной цели, или идёт
        /// <see cref="IsStepLocked"/>. Публично для HUD (decay/содержимое
        /// плиты — <see cref="TunnelGrid.TryGetTile"/>).
        /// </summary>
        public GridCoordinate? DwellTarget => _dwellTarget;

        /// <summary>Issue #157: true, пока идёт фиксация шага (<see cref="StepDurationSeconds"/> после отпускания) — ввод заблокирован.</summary>
        public bool IsStepLocked => _stepLockRemainingSeconds > 0f;

        /// <summary>Issue #157 — HUD/дебаг: сколько ещё секунд заблокирован ввод.</summary>
        public float StepLockRemainingSeconds => _stepLockRemainingSeconds;

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

        private void OnDisable()
        {
            CancelDwell(); // не оставляем подсветку висящей на выключенном компоненте
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
            CancelDwell();
            IsAlive = true;

            RunStarted?.Invoke();
        }

        private void Update()
        {
            if (!IsAlive) return;

            if (!_dwellEnabled)
            {
                UpdateDragMode();
                return;
            }

            UpdateDwellMode();
        }

        /// <summary>Оригинальная механика протяжки (issue #60/#61) — без изменений, для A/B через <see cref="DwellEnabled"/>.</summary>
        private void UpdateDragMode()
        {
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

        /// <summary>
        /// Issue #157 — "примеривание": удержание на соседней плите её
        /// показывает (не двигая трейл), отпускание фиксирует шаг на
        /// <see cref="StepDurationSeconds"/> секунд заблокированного ввода.
        /// Время (<see cref="Burmalda.Decay.TrailDecaySystem"/>) не
        /// останавливается на время примеривания — тикает независимо от
        /// этого контроллера (см. doc-комментарий TrailDecaySystem).
        /// </summary>
        private void UpdateDwellMode()
        {
            if (_stepLockRemainingSeconds > 0f)
            {
                _stepLockRemainingSeconds = Mathf.Max(0f, _stepLockRemainingSeconds - Time.deltaTime);
                return; // ввод заблокирован на время фиксации шага
            }

            var pointer = Pointer.current;
            var pressed = pointer != null && pointer.press.isPressed;

            if (pressed && !IsPressed) PressStarted?.Invoke();
            var wasPressed = IsPressed;
            IsPressed = pressed;

            if (!pressed)
            {
                if (wasPressed) CommitOrCancelDwell(); // переход "прижат" -> "не прижат" — момент фиксации/отмены
                return;
            }

            var screenPosition = pointer.position.ReadValue();
            CurrentScreenPosition = screenPosition;

            var candidate = ResolveTappedTile(_camera, screenPosition);
            SetDwellTarget(candidate);

            // Тап по своей же текущей позиции — сигнал ConfirmRun, не шаг
            // (см. doc-комментарий RunConfirmed). Не завязан на отпускание —
            // это не ход по трейлу, фиксировать нечего.
            if (candidate.HasValue && candidate.Value == _trail.CurrentPosition && !_confirmFiredForCurrentDwell)
            {
                RunConfirmed?.Invoke();
                _confirmFiredForCurrentDwell = true;
            }
        }

        private void CommitOrCancelDwell()
        {
            var target = _dwellTarget;
            ClearHighlight();
            _dwellTarget = null;
            _confirmFiredForCurrentDwell = false;

            if (!target.HasValue) return; // отпущено мимо валидной цели — отмена, трейл не трогаем
            if (target.Value == _trail.CurrentPosition) return; // тап по своей же позиции уже обработан в UpdateDwellMode

            var advanced = _trail.TryAdvanceTo(target.Value);
            if (advanced) _stepLockRemainingSeconds = _stepDurationSeconds; // шаг занял время — ввод в лок, ловушка/смерть не запускают лок (ход не засчитан)
        }

        private void CancelDwell()
        {
            ClearHighlight();
            _dwellTarget = null;
            _confirmFiredForCurrentDwell = false;
            _stepLockRemainingSeconds = 0f;
        }

        private void SetDwellTarget(GridCoordinate? candidate)
        {
            // Валидная цель примеривания — соседняя плита, на которую МОЖНО
            // шагнуть (не своя позиция, не препятствие/ловушка/закрытые
            // ворота — та же проверка, что и реальный ход, см.
            // GridTraceTrail.CanAdvanceTo). Невалидная/пустая цель — превью
            // снимается, палец может тащить дальше в поисках валидной.
            var isValidStepTarget = candidate.HasValue
                && candidate.Value != _trail.CurrentPosition
                && _trail.CanAdvanceTo(candidate.Value);

            if (!isValidStepTarget) candidate = null;

            if (_dwellTarget == candidate) return; // без изменений — не дёргаем подсветку/флаг подтверждения каждый кадр

            _confirmFiredForCurrentDwell = false; // цель сменилась — если это была плита ConfirmRun, разрешаем сигнал заново при возврате на неё
            ClearHighlight();
            _dwellTarget = candidate;
            if (candidate.HasValue) ApplyHighlight(candidate.Value);
        }

        private void ApplyHighlight(GridCoordinate coordinate)
        {
            // Раздельный raycast от ResolveTappedTile — тому нужна только
            // координата (переиспользуется и протяжкой, и confirm-веткой
            // выше), подсветке нужен сам Renderer, не стоит утяжелять общий
            // метод под один вызывающий сценарий.
            if (_camera == null) return;
            var ray = _camera.ScreenPointToRay(CurrentScreenPosition);
            if (!Physics.Raycast(ray, out var hit)) return;
            var marker = hit.collider.GetComponent<TileVisualMarker>();
            if (marker == null || marker.Coordinate != coordinate) return;

            var renderer = hit.collider.GetComponent<Renderer>();
            if (renderer == null) return;

            _highlightedRenderer = renderer;
            _highlightedRendererOriginalColor = renderer.material.color;
            renderer.material.color = DwellHighlightColor;
        }

        private void ClearHighlight()
        {
            if (_highlightedRenderer == null) return;
            _highlightedRenderer.material.color = _highlightedRendererOriginalColor;
            _highlightedRenderer = null;
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
