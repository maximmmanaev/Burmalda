using Burmalda.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Burmalda.Movement
{
    /// <summary>
    /// Приём трейс-ввода пальцем (PRD 4.1): пока палец/курсор прижат,
    /// каждая новая плита под ним, соседняя текущей позиции, продвигает
    /// трейл игрока вперёд по тоннелю. Механика ввода не меняется
    /// относительно HTML-прототипа (legacy/burmolda_demo.html); автоскролл
    /// камеры — отдельная задача (#8), здесь не реализуется. Позиция курсора
    /// обрабатывается только при реальном изменении с прошлого обработанного
    /// кадра (#60) — иначе статичный зажатый клик, опрашиваемый каждый кадр,
    /// мог засчитать несколько шагов подряд на одной и той же позиции.
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

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;

            _projection = new WorldGridProjection(_tileSize, _width);
            _grid = new TunnelGrid(_width);

            var start = new GridCoordinate(0, _width / 2);
            _trail = new GridTraceTrail(_grid, start);
        }

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.isPressed)
            {
                _positionChangeFilter.Reset();
                return;
            }

            var screenPosition = pointer.position.ReadValue();
            if (!_positionChangeFilter.HasChanged(screenPosition)) return;

            TryAdvanceAtScreenPosition(screenPosition);
        }

        private void TryAdvanceAtScreenPosition(Vector2 screenPosition)
        {
            if (_camera == null) return;

            var ray = _camera.ScreenPointToRay(screenPosition);
            var tunnelFloor = new Plane(Vector3.up, Vector3.zero);
            if (!tunnelFloor.Raycast(ray, out var distance)) return;

            var worldPoint = ray.GetPoint(distance);
            var target = _projection.ToGridCoordinate(worldPoint);
            _trail.TryAdvanceTo(target);
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
