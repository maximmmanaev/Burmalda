using System;
using Burmalda.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Burmalda.Movement
{
    /// <summary>
    /// Приём ввода (PRD 4.1, переработка §4.1 v8, issue #157 — перенос
    /// схемы A из спайка): удержание пальца на соседней ВАЛИДНОЙ плите —
    /// примеривание, трейл не двигается, только показывает
    /// <see cref="PreviewTarget"/> (подсветка/инфо — см.
    /// <c>Burmalda.DebugVisuals.TilePreviewController</c>); ведение пальцем
    /// без отрыва переносит примеривание на другую соседнюю плиту, сколько
    /// угодно раз, бесплатно; отпускание фиксирует шаг
    /// (<see cref="GridTraceTrail.TryAdvanceTo"/>) и блокирует ввод на
    /// <see cref="StepDurationSeconds"/> (0.15–0.5с, см. её doc-комментарий);
    /// отпускание вне валидной соседней плиты — отмена, шага нет, лока нет
    /// (отменять нечего анимировать). Отменяет непрерывную протяжку (PRD v5
    /// 4.1) прямым решением владельца продукта после спайка (issue #157).
    ///
    /// <b>Распад НЕ гейтится этим контроллером</b> — <c>Decay.TrailDecaySystem.Tick</c>
    /// вызывается независимо, каждый кадр, вне зависимости от того,
    /// удерживает ли игрок примеривание или ждёт лока после шага (PRD:
    /// "время не останавливается никогда", "стоимость информации — время").
    /// Раздумье прогорает под ногами буквально — как и раньше, только
    /// теперь раздумье имеет явную механическую форму (удержание).
    ///
    /// Владеет жизненным циклом забега: <see cref="Grid"/>/<see cref="Trail"/>
    /// пересоздаются в <see cref="Restart"/> (мир перегенерируется заново, по
    /// прямому запросу владельца продукта) — зависимые системы (Decay,
    /// камера, debug-визуал, Burmalda.RunLifecycle) подписываются на
    /// <see cref="RunStarted"/>, чтобы пересобрать себя под новый забег.
    ///
    /// <see cref="IsPressed"/> — публично читаемое состояние прижатия
    /// пальца, нужно <see cref="TunnelCameraController"/>: камера обновляется
    /// ТОЛЬКО между шагами (issue #157, часть механики, не оптимизация) —
    /// пока <see cref="IsPressed"/> истинно, <see cref="TunnelCameraController.Update"/>
    /// не трогает ни FOV, ни позицию/поворот вообще.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridTraceInputController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private int _width = 5;

        // Issue #157 (перенос схемы A): анимация шага/блокировка ввода после
        // отпускания, секунды. Диапазон 0.15-0.5 задан задачей — тюнинг
        // владельца продукта, не переносится в отдельный дебаг-слайдер
        // (задача Part 2 его не просит, в отличие от Part 1/issue #158).
        [SerializeField] private float _stepDurationSeconds = 0.3f;

        private TunnelGrid _grid;
        private GridTraceTrail _trail;
        private WorldGridProjection _projection;

        private bool _wasPressed;
        private float _lockedUntilTime;

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

        /// <summary>Прижат ли сейчас палец/курсор. Обновляется каждый кадр в <see cref="Update"/> — см. doc-комментарий класса насчёт того, зачем это публично.</summary>
        public bool IsPressed { get; private set; }

        /// <summary>Последняя известная экранная позиция пальца/курсора — валидна, пока <see cref="IsPressed"/>.</summary>
        public Vector2 CurrentScreenPosition { get; private set; }

        /// <summary>
        /// Плита, которую сейчас примеривают — соседняя текущей позиции и
        /// проходимая (<see cref="GridTraceTrail.CanAdvanceTo"/>), под
        /// пальцем прямо сейчас. Null, если палец не прижат, или прижат, но
        /// не над валидной соседней плитой (в т.ч. над собственной текущей
        /// позицией — тап-кнопка, не примеривание, см. <see cref="RunConfirmed"/>).
        /// Публично — для визуала подсветки/инфо (issue #157, PRD 4.1 п.1).
        /// </summary>
        public GridCoordinate? PreviewTarget { get; private set; }

        /// <summary>Длительность анимации шага/блокировки ввода после отпускания, секунды (0.15–0.5, PRD 4.1 п.3) — публично для чтения debug-визуалом/HUD.</summary>
        public float StepDurationSeconds => _stepDurationSeconds;

        /// <summary>Истинно, пока ввод заблокирован после недавнего шага (см. <see cref="StepDurationSeconds"/>) — публично для HUD.</summary>
        public bool IsInputLocked => Time.time < _lockedUntilTime;

        /// <summary>
        /// Срабатывает сразу после того, как <see cref="Grid"/>/<see cref="Trail"/>
        /// (пере)созданы — и при первом запуске (<see cref="Awake"/>), и при
        /// каждом <see cref="Restart"/>. Зависимые контроллеры (Decay/камера/
        /// debug-визуал/RunLifecycle) должны пересобрать свои системы по
        /// этому событию, а не полагаться на разовую ленивую инициализацию.
        /// </summary>
        public event Action RunStarted;

        /// <summary>
        /// Срабатывает в момент перехода "не прижат" -> "прижат" — ровно один
        /// раз на новое нажатие, не каждый кадр, пока палец удерживается (для
        /// этого — <see cref="IsPressed"/>). Срабатывает и во время
        /// <see cref="IsInputLocked"/> (сам факт касания экрана не зависит от
        /// того, засчитает ли игра его игровым действием) — на данный момент
        /// без подписчиков в производственном коде (эдж-скролл, для которого
        /// событие заводили, убран), оставлено для истории/возможных будущих
        /// подписчиков.
        /// </summary>
        public event Action PressStarted;

        /// <summary>
        /// Срабатывает, когда палец прижат ровно над ТЕКУЩЕЙ позицией игрока
        /// (<c>Trail.CurrentPosition</c>) — на практике это в первую очередь
        /// самый первый тап по стартовой плите, ДО того, как игрок реально
        /// сдвинулся: тап-триггер камеры-интро (см. <see cref="TunnelCameraFollow.ConfirmRun"/>).
        /// Тап по своей же текущей позиции сам по себе НЕ засчитывается как
        /// примеривание/ход (<c>IsAdjacentTo</c> и так вернула бы false для
        /// координаты, равной самой себе) — просто кнопка. Может сработать
        /// повторно на каждом кадре, пока палец удерживается ровно над своей
        /// позицией — безопасно, <see cref="TunnelCameraFollow.ConfirmRun"/>
        /// идемпотентна.
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
            PreviewTarget = null;
            _wasPressed = false;
            _lockedUntilTime = 0f;
            IsAlive = true;

            RunStarted?.Invoke();
        }

        private void Update()
        {
            if (!IsAlive) return;

            var pointer = Pointer.current;
            var pressed = pointer != null && pointer.press.isPressed;

            // IsPressed/PressStarted — ДО проверки лока ниже: камера должна
            // видеть реальное состояние пальца на экране (issue #157,
            // "камера неподвижна, пока палец на экране") независимо от того,
            // засчитает ли игровая логика это касание.
            if (pressed && !IsPressed) PressStarted?.Invoke();
            IsPressed = pressed;
            if (pressed) CurrentScreenPosition = pointer.position.ReadValue();

            if (IsInputLocked)
            {
                // Ввод заблокирован на время анимации шага (PRD 4.1 п.3) —
                // тачи игнорируются целиком, никакого нового примеривания.
                // Не трогаем _wasPressed здесь: если палец отпустили ДО
                // конца лока, HandlePointerReleased уже отработал в кадре
                // самого отпускания (см. ниже — та ветка идёт ДО этой
                // проверки для releasing-кадра, лок стартует только ПОСЛЕ
                // него), так что здесь ничего досрочно завершать не нужно.
                return;
            }

            if (pressed)
                HandlePointerPressed(CurrentScreenPosition);
            else if (_wasPressed)
                HandlePointerReleased();

            _wasPressed = pressed;
        }

        private void HandlePointerPressed(Vector2 screenPosition)
        {
            if (_camera == null) return;

            var target = ResolveTappedTile(_camera, screenPosition);

            if (target.HasValue && target.Value == _trail.CurrentPosition)
            {
                RunConfirmed?.Invoke();
                PreviewTarget = null;
                return;
            }

            PreviewTarget = target.HasValue && _trail.CanAdvanceTo(target.Value) ? target : (GridCoordinate?)null;
        }

        private void HandlePointerReleased()
        {
            if (PreviewTarget.HasValue)
            {
                var committed = _trail.TryAdvanceTo(PreviewTarget.Value);
                if (committed) _lockedUntilTime = Time.time + _stepDurationSeconds;
            }
            // Иначе — отпускание вне валидной соседней плиты: отмена, шага
            // нет (PRD 4.1 п.4). Лок НЕ ставим — отменять нечего анимировать,
            // следующая попытка разрешена немедленно.

            PreviewTarget = null;
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
        /// Статический и публичный — тестируется напрямую, без Update()/
        /// реального Input System.
        /// </summary>
        public static GridCoordinate? ResolveTappedTile(Camera camera, Vector2 screenPosition)
        {
            if (camera == null) return null;

            var ray = camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit)) return null;

            var marker = hit.collider.GetComponent<TileVisualMarker>();
            return marker != null ? marker.Coordinate : (GridCoordinate?)null;
        }
    }
}
