using System;
using System.Collections.Generic;
using Burmalda.Core;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Debug-визуализация плит тоннеля в рантайме (issue #58): примитивная
    /// геометрия, создаваемая кодом (без .prefab), с цветом по
    /// <see cref="TileDebugColor"/>. Не финальный арт, не анимации/партиклы —
    /// только чтобы видеть механики Core/Movement/Decay глазами, а не только
    /// через Test Runner. По паттерну <c>Decay.TrailDecaySystem</c>: обычный
    /// C#-класс с логикой, тикается тонким driver'ом (<see cref="TunnelDebugVisualController"/>).
    ///
    /// <b>Найдено на реальном Android-билде (2026-08-14):</b> <c>Shader.Find</c>
    /// надёжен только в Editor — на устройстве шейдер-страппинг может урезать
    /// все три перебираемых варианта, и <c>Shader.Find</c> возвращает null для
    /// каждого. Раньше это приводило к необработанному <c>ArgumentNullException</c>
    /// в конструкторе КАЖДЫЙ кадр (см. <see cref="TunnelDebugVisualController.Update"/> —
    /// ленивая пересборка ретраится, пока <c>_visual</c> не создан) — реально
    /// вешало игру на сером экране после сплэша. Теперь при отсутствии
    /// доступного шейдера визуал безопасно САМООТКЛЮЧАЕТСЯ (одно предупреждение
    /// в лог, не создаёт геометрию) — debug-инфраструктура необязательна для
    /// игры и не должна иметь возможность её сломать.
    /// </summary>
    public sealed class TunnelDebugVisual : IDisposable
    {
        // Небольшой зазор между плитами, как s=cellSize-2 в draw() прототипа.
        private const float TileScale = 0.92f;
        private const float TileHeight = 0.1f;

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly WorldGridProjection _projection;
        private readonly Transform _parent;
        private readonly Dictionary<GridCoordinate, GameObject> _tileObjects = new Dictionary<GridCoordinate, GameObject>();
        private readonly Material _templateMaterial;
        private bool _disposed;

        /// <summary>Ложь, если ни один шейдер не найден в этой сборке — визуал не создаёт геометрию и ничего не делает.</summary>
        public bool IsEnabled => _templateMaterial != null;

        public TunnelDebugVisual(TunnelGrid grid, GridTraceTrail trail, WorldGridProjection projection, Transform parent)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _projection = projection;
            _parent = parent;
            _templateMaterial = CreateTemplateMaterial();

            if (!IsEnabled)
            {
                Debug.LogWarning("TunnelDebugVisual: ни один шейдер не найден (Shader.Find вернул null для всех вариантов) — debug-визуализация плит отключена в этой сборке.");
                return;
            }

            _grid.TileMaterialized += OnTileMaterialized;

            // Стартовая плита трейла материализуется в конструкторе
            // GridTraceTrail — до того, как визуал успевает подписаться на
            // событие выше. Досоздаём всё, что уже есть в трейле на момент
            // подписки, чтобы не потерять её и любые более ранние ходы.
            foreach (var coordinate in _trail.Path)
                OnTileMaterialized(_grid.GetOrCreateTile(coordinate));
        }

        /// <summary>Пересчитывает цвет всех созданных плит по их текущему состоянию. Не-op, если <see cref="IsEnabled"/> ложь.</summary>
        public void Tick()
        {
            if (!IsEnabled) return;

            var startCoordinate = _trail.Path[0];
            var currentCoordinate = _trail.CurrentPosition;

            foreach (var pair in _tileObjects)
            {
                var coordinate = pair.Key;
                var tile = _grid.GetOrCreateTile(coordinate);
                var state = new TileVisualState(
                    isStart: coordinate == startCoordinate,
                    isCurrentPosition: coordinate == currentCoordinate,
                    isDestroyed: tile.IsDestroyed,
                    isBlocked: tile.IsBlocked,
                    lethalTrap: tile.LethalTrap,
                    decayProgress01: tile.DecayProgress01,
                    isExplosiveTrapTrigger: tile.ExplosiveTrapTarget.HasValue,
                    isTimedTrapTrigger: tile.TimedTrapTarget.HasValue,
                    activeTimedTrap: tile.IsTimedTrapActive ? tile.TimedTrapKind : null);

                ApplyColor(pair.Value, TileDebugColor.Resolve(state));
            }
        }

        /// <summary>Отписывается от сетки и уничтожает всю созданную геометрию. Безопасно вызывать, даже если <see cref="IsEnabled"/> ложь.</summary>
        public void Dispose()
        {
            if (_disposed) return;

            if (IsEnabled)
            {
                _grid.TileMaterialized -= OnTileMaterialized;

                foreach (var tileObject in _tileObjects.Values)
                    UnityEngine.Object.Destroy(tileObject);
                _tileObjects.Clear();

                UnityEngine.Object.Destroy(_templateMaterial);
            }

            _disposed = true;
        }

        private void OnTileMaterialized(Tile tile)
        {
            if (_tileObjects.ContainsKey(tile.Coordinate)) return;

            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            primitive.name = $"DebugTile {tile.Coordinate}";
            primitive.transform.SetParent(_parent, worldPositionStays: false);
            primitive.transform.position = _projection.ToWorldPosition(tile.Coordinate);
            primitive.transform.localScale = new Vector3(
                _projection.TileSize * TileScale,
                TileHeight,
                _projection.TileSize * TileScale);

            var renderer = primitive.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(_templateMaterial);

            _tileObjects[tile.Coordinate] = primitive;
        }

        private static void ApplyColor(GameObject tileObject, Color color)
        {
            var renderer = tileObject.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = color;
        }

        private static Material CreateTemplateMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");
            return shader != null ? new Material(shader) : null;
        }
    }
}
