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
    /// камеры — отдельная задача (#8), здесь не реализуется.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridTraceInputController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] private int _width = 5;

        private TunnelGrid _grid;
        private GridTraceTrail _trail;
        private WorldGridProjection _projection;

        /// <summary>Трейл текущего забега — для чтения смежными системами (визуал трейла, камера).</summary>
        public GridTraceTrail Trail => _trail;

        /// <summary>Сетка тоннеля текущего забега — нужна смежным системам (напр. Decay) для доступа к плитам трейла.</summary>
        public TunnelGrid Grid => _grid;

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
            if (pointer == null || !pointer.press.isPressed) return;

            TryAdvanceAtScreenPosition(pointer.position.ReadValue());
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
    }
}
