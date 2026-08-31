using System;
using System.Collections.Generic;
using Burmalda.Core;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Debug-визуализация плит тоннеля в рантайме (issue #58): примитивная
    /// геометрия, создаваемая кодом (без .prefab). С первой генерации арта
    /// ("Завести арт в игру") плита получает текстуру из
    /// <see cref="TileArtCatalog"/> по <see cref="TileArtKindResolver"/>,
    /// если она для её состояния есть — иначе однотонный fallback-цвет по
    /// <see cref="TileDebugColor"/> (см. <see cref="ApplyVisual"/>). Не всё
    /// ещё финальный арт (анимации/партиклы отсутствуют), только чтобы
    /// видеть механики Core/Movement/Decay глазами, а не только через Test
    /// Runner. По паттерну <c>Decay.TrailDecaySystem</c>: обычный C#-класс
    /// с логикой, тикается тонким driver'ом (<see cref="TunnelDebugVisualController"/>).
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
        // Issue "Завести арт в игру": может быть null (ассет ещё не создан
        // Editor-скриптом/удалён) — в этом случае ApplyVisual падает
        // обратно на TileDebugColor для ВСЕХ плит, не только для None.
        private readonly TileArtCatalog _artCatalog;
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
            _artCatalog = TileArtCatalog.Load();

            if (!IsEnabled)
            {
                Debug.LogWarning("TunnelDebugVisual: ни один шейдер не найден (Shader.Find вернул null для всех вариантов) — debug-визуализация плит отключена в этой сборке.");
                return;
            }

            _grid.TileMaterialized += OnTileMaterialized;

            // Реальный баг, подтверждён логами на устройстве (2026-08-14,
            // не гипотеза): TileMaterialized — идемпотентное событие
            // (стреляет один раз за координату навсегда, см.
            // TunnelGrid.GetOrCreateTile), а порядок Update() между
            // TunnelObstacleController (материализует TunnelGridReveal
            // рядов вперёд игрока) и TunnelDebugVisualController (создаёт
            // этот класс) Unity не гарантирует — тот же класс проблем, что
            // и с RunStarted/Awake/OnEnable (см. док-комментарии там).
            // Если reveal успевает материализовать плиты РАНЬШЕ подписки
            // выше, их события улетают в пустоту без второго шанса — эти
            // плиты остаются без GameObject НАВСЕГДА (не только до
            // следующего хода), визуально это была "одинокая плитка"
            // вместо всей раскрытой вперёд сетки. Раньше здесь бэкфиллился
            // только _trail.Path (пройденные клетки, на старте — одна
            // стартовая) — недостаточно, раскрытые-но-непосещённые плиты
            // TunnelGridReveal в него не попадают. Теперь бэкфиллим ВСЕ уже
            // материализованные на текущий момент плиты грида
            // (TunnelGrid.MaterializedTiles) — это надмножество Path,
            // накрывает и старт, и любой уже раскрытый перед игроком запас.
            // Сам race по Update()-порядку НЕ трогаем (точечный фикс
            // достаточен — см. TunnelGrid.MaterializedTiles).
            foreach (var tile in _grid.MaterializedTiles)
                OnTileMaterialized(tile);
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
                    activeTimedTrap: tile.IsTimedTrapActive ? tile.TimedTrapKind : null,
                    isBoss: tile.IsBoss,
                    isManaSource: tile.IsManaSource,
                    isKeySource: tile.IsKeySource,
                    isLever: tile.IsLever,
                    isGated: tile.IsGated,
                    isLeverGateOpen: tile.IsLeverGateOpen);

                ApplyVisual(pair.Value, state);
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

            // Тап должен физически попадать в эту плитку (см. TileVisualMarker) —
            // CreatePrimitive(Cube) уже даёт BoxCollider, отдельно добавлять не нужно.
            primitive.AddComponent<TileVisualMarker>().Initialize(tile.Coordinate);

            var renderer = primitive.GetComponent<Renderer>();
            if (renderer != null) renderer.material = new Material(_templateMaterial);

            _tileObjects[tile.Coordinate] = primitive;
        }

        // 2026-08-18 ("ещё больше процедурного полиша без арта"): лёгкая
        // эмиссия поверх базового цвета — плитки заметно "светятся" под
        // Bloom (DebugVisuals.ScenePostProcessing) независимо от угла
        // падения света, не только от прямого освещения сцены. Оставлена
        // только для цветного fallback-режима (см. ApplyVisual) — поверх
        // настоящей текстуры равномерное белое свечение искажает сам арт,
        // а не подсвечивает его, как было задумано для плоского debug-цвета.
        private const float EmissionIntensity = 0.35f;

        /// <summary>
        /// Issue "Завести арт в игру": текстура из <see cref="_artCatalog"/>
        /// по <see cref="TileArtKindResolver"/>, если она есть — иначе
        /// прежний однотонный <see cref="TileDebugColor"/> (тот же
        /// оборонительный fallback, что и на отсутствующий шейдер, см.
        /// doc-комментарий класса). Ветвление "что показывать когда" не
        /// менялось — только источник (текстура вместо Color) для тех
        /// состояний, для которых в пакете есть готовый арт.
        /// </summary>
        private void ApplyVisual(GameObject tileObject, TileVisualState state)
        {
            var renderer = tileObject.GetComponent<Renderer>();
            if (renderer == null) return;

            var kind = TileArtKindResolver.Resolve(state);
            var texture = kind != TileArtKind.None && _artCatalog != null ? _artCatalog.Get(kind) : null;

            var material = renderer.material;
            if (texture != null)
            {
                material.mainTexture = texture;
                // Задача «сделать тоннель играбельным», часть 3: tile-pit.png
                // (кислотно-зелёная мозаика с чёрной дырой) читается как
                // однозначная ловушка, не как "с плитой что-то не так" — см.
                // TileDebugColor.HiddenTrapSignatureTextureTint. Единственное
                // состояние, где текстура не показывается как есть.
                material.color = kind == TileArtKind.HiddenTrapSignature
                    ? TileDebugColor.HiddenTrapSignatureTextureTint
                    : Color.white;
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
                if (material.HasProperty("_EmissionColor")) material.DisableKeyword("_EMISSION");
                return;
            }

            var color = TileDebugColor.Resolve(state);
            material.mainTexture = null;
            material.color = color;

            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.55f);
            if (!material.HasProperty("_EmissionColor")) return;

            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * EmissionIntensity);
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
