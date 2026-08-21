using System.Collections.Generic;
using Burmalda.Core;
using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Контурная подсветка примериваемой плиты (issue #157, PRD 4.1 п.1:
    /// "плита обводится контуром"). Контур — 4 тонких ярких бруска по
    /// периметру плиты, а не заливка всей грани: заливка прячет собственный
    /// цвет плиты (распад/ловушка/валюта —
    /// <c>Burmalda.DebugVisuals.TileDebugColor</c>), который одновременно
    /// показывает текстовая инфа о плите — они бы визуально противоречили
    /// друг другу.
    ///
    /// Не зависит от <c>Burmalda.DebugVisuals.TunnelDebugVisual</c> и его
    /// GameObject'ов плит (которые могут пересоздаваться при реколоризации) —
    /// позиционируется напрямую от <see cref="WorldGridProjection"/>, как и
    /// сама механика хода.
    /// </summary>
    public sealed class TilePreviewHighlighter
    {
        private const float FrameThickness = 0.06f;
        private const float FrameHeight = 0.08f;

        // TunnelDebugVisual.TileHeight = 0.1, плита центрирована в y=0 (её
        // верхняя грань — y=0.05) — контур чуть выше, чтобы не проваливаться
        // в геометрию плиты и не z-fighting'овать.
        private const float YOffset = 0.09f;

        private readonly Transform _parent;
        private readonly Material _material;
        private readonly List<GameObject> _bars = new List<GameObject>(4);
        private GridCoordinate? _current;

        public TilePreviewHighlighter(Transform parent, Color color)
        {
            _parent = parent;

            // Тот же порядок перебора шейдеров и та же защита от
            // build-time стриппинга/Shader.Find==null на устройстве, что и
            // в TunnelDebugVisual.CreateTemplateMaterial (issue 2026-08-14).
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");
            _material = shader != null ? new Material(shader) : null;
            if (_material == null) return;

            _material.color = color;
            if (_material.HasProperty("_EmissionColor"))
            {
                _material.EnableKeyword("_EMISSION");
                _material.SetColor("_EmissionColor", color);
            }
        }

        /// <summary>Ложь, если ни один шейдер не найден в этой сборке (см. конструктор) — <see cref="Show"/>/<see cref="Hide"/> становятся не-op.</summary>
        public bool IsEnabled => _material != null;

        /// <summary>Показывает контур на <paramref name="coordinate"/>. Не-op (без пересоздания геометрии), если это уже текущая подсвеченная плита.</summary>
        public void Show(GridCoordinate coordinate, WorldGridProjection projection)
        {
            if (!IsEnabled) return;
            if (_current.HasValue && _current.Value == coordinate) return;

            Hide();
            _current = coordinate;

            var center = projection.ToWorldPosition(coordinate);
            var half = projection.TileSize * 0.5f;

            CreateBar(center + new Vector3(0f, YOffset, half), new Vector3(projection.TileSize, FrameHeight, FrameThickness));
            CreateBar(center + new Vector3(0f, YOffset, -half), new Vector3(projection.TileSize, FrameHeight, FrameThickness));
            CreateBar(center + new Vector3(half, YOffset, 0f), new Vector3(FrameThickness, FrameHeight, projection.TileSize));
            CreateBar(center + new Vector3(-half, YOffset, 0f), new Vector3(FrameThickness, FrameHeight, projection.TileSize));
        }

        /// <summary>Убирает контур, если он показан. Безопасно вызывать, даже если ничего не показано.</summary>
        public void Hide()
        {
            _current = null;
            foreach (var bar in _bars)
                if (bar != null) Object.Destroy(bar);
            _bars.Clear();
        }

        /// <summary>Убирает геометрию и материал. Вызывать при уничтожении владеющего компонента.</summary>
        public void Dispose()
        {
            Hide();
            if (_material != null) Object.Destroy(_material);
        }

        private void CreateBar(Vector3 position, Vector3 scale)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "TilePreviewFrameBar";

            // CreatePrimitive(Cube) даёт BoxCollider по умолчанию — здесь он
            // не нужен и вреден: брусок сидит прямо на краю плиты и мог бы
            // перехватить Physics.Raycast тапа раньше настоящей плитки
            // (см. GridTraceInputController.ResolveTappedTile).
            var collider = bar.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            bar.transform.SetParent(_parent, worldPositionStays: false);
            bar.transform.position = position;
            bar.transform.localScale = scale;

            var renderer = bar.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = _material;

            _bars.Add(bar);
        }
    }
}
