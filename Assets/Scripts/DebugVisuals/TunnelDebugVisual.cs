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
                    // Задача «тёплый набор плит» (владелец): «плитка с ключом
                    // и маной после сбора должна становиться обычной, ключ и
                    // кристалл маны пропадать». Tile.IsManaSource/IsKeySource
                    // сознательно НЕ сбрасываются в Core — сбор валюты
                    // (Currencies.TrailTileCurrencySystem) и партиклы
                    // (PickupFeedback) независимо читают эти флаги на том же
                    // GridTraceTrail.Advanced, порядок подписчиков не
                    // гарантирован (PickupFeedback явно избегает трогать
                    // TrailTileCurrencySystem по этой же причине, см. её
                    // doc-комментарий) — обнулять флаг оттуда было бы гонкой.
                    // Вместо этого визуальный слой сам скрывает иконку, как
                    // только плита пройдена трейлом (HasVisited) — тот же
                    // момент, что и начисление валюты, без мутации Tile.
                    isManaSource: tile.IsManaSource && !_trail.HasVisited(coordinate),
                    isKeySource: tile.IsKeySource && !_trail.HasVisited(coordinate),
                    isLever: tile.IsLever,
                    isGated: tile.IsGated,
                    isLeverGateOpen: tile.IsLeverGateOpen,
                    isAltar: tile.IsAltar,
                    isDangerSignatureRevealed: tile.IsDangerSignatureRevealed);

                ApplyVisual(pair.Value, coordinate, state);
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
            // Задача «тёплый набор плит»: выбор фиксируется один раз здесь,
            // не в ApplyVisual — иначе вариант/поворот "плавали" бы каждый
            // кадр, пока плита ещё Fresh (см. PickFreshVariant/ApplyVisual).
            _freshVariants[tile.Coordinate] = PickFreshVariant();
        }

        // Задача «тёплый набор плит»: два рисунка обычной плиты — бесплатное
        // визуальное разнообразие пола, иначе однообразное повторение одной
        // картинки само становится читаемым ритмом. Поровну — третий
        // присланный вариант (tile-fresh-c) оказался дублем стадии распада
        // (не отдельным полом), удалён; веса ниже больше не нужны, но массив
        // оставлен ради единообразия с PickFreshVariant на случай, если
        // владелец пришлёт третий настоящий вариант позже.
        private static readonly int[] FreshVariantWeights = { 1, 1 };

        private readonly struct FreshTileVariant
        {
            public FreshTileVariant(int textureIndex, Quaternion rotation)
            {
                TextureIndex = textureIndex;
                Rotation = rotation;
            }

            public int TextureIndex { get; }
            public Quaternion Rotation { get; }
        }

        private readonly Dictionary<GridCoordinate, FreshTileVariant> _freshVariants = new Dictionary<GridCoordinate, FreshTileVariant>();

        private static FreshTileVariant PickFreshVariant()
        {
            var totalWeight = 0;
            foreach (var weight in FreshVariantWeights) totalWeight += weight;

            var roll = UnityEngine.Random.Range(0, totalWeight);
            var cursor = 0;
            var index = FreshVariantWeights.Length - 1;
            for (var i = 0; i < FreshVariantWeights.Length; i++)
            {
                cursor += FreshVariantWeights[i];
                if (roll < cursor) { index = i; break; }
            }

            // Случайный поворот на 0/90/180/270° (задача) — только у обычного
            // пола; у плит со смыслом (источники/сигнатуры/ворота/рычаг/
            // Алтарь/позиция игрока) ориентация постоянна, см. ApplyVisual.
            // TopFaceOrientationCorrectionDegrees складывается с этим шагом —
            // см. её doc-комментарий.
            var rotationSteps = UnityEngine.Random.Range(0, 4);
            return new FreshTileVariant(index, Quaternion.Euler(0f, TopFaceOrientationCorrectionDegrees + rotationSteps * 90f, 0f));
        }

        /// <summary>
        /// Владелец на билде «тёплый набор плит»: «почему все плитки
        /// перевёрнуты вверх ногами» — реальный баг, не показалось. Причина
        /// не в контенте, а в том, как <c>GameObject.CreatePrimitive(Cube)</c>
        /// разворачивает UV верхней грани: её V-ось (то, что художник рисует
        /// "верхом" картинки) у встроенного примитива Unity по умолчанию
        /// смотрит на -Z, а Row (глубина тоннеля, "вперёд/от камеры") — это
        /// +Z (см. <c>WorldGridProjection</c> — Row=+Z, doc-комментарий
        /// класса). При identity-повороте "верх" текстуры оказывался
        /// направлен К камере, а не ОТ неё — с прежними ненаправленными
        /// каменными текстурами этого никто не замечал, с направленными
        /// иконками (следы, ключ, кристалл) стало видно сразу. Поворот на
        /// 180° по Y выравнивает "верх" текстуры с "вперёд по тоннелю" —
        /// применяется КО ВСЕМ плитам (не только Fresh), см. места вызова.
        /// </summary>
        private const float TopFaceOrientationCorrectionDegrees = 180f;

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
        ///
        /// <b>Задача «тёплый набор плит»:</b> <c>tile-hidden-trap-signature.png</c>
        /// (владелец) — уже приглушённая, специально нарисованная под эту
        /// роль текстура (используется и для <see cref="TileArtKind.HiddenTrapSignature"/>,
        /// и для <see cref="TileArtKind.TriggerSignature"/> — см.
        /// <see cref="TileArtCatalog.Get"/>), показывается как есть, как и
        /// остальные текстуры. Прежний принудительный тон
        /// (<see cref="TileDebugColor.HiddenTrapSignatureTextureTint"/>)
        /// компенсировал СЛУЧАЙНО подвернувшуюся холодную текстуру
        /// (<c>tile-pit.png</c>), которая читалась как однозначная
        /// яма-ловушка — с целевой тёплой текстурой такой необходимости
        /// нет; константа оставлена в TileDebugColor на случай, если
        /// плейтест на устройстве покажет обратное.
        /// </summary>
        private void ApplyVisual(GameObject tileObject, GridCoordinate coordinate, TileVisualState state)
        {
            var renderer = tileObject.GetComponent<Renderer>();
            if (renderer == null) return;

            var kind = TileArtKindResolver.Resolve(state);
            Texture2D texture;
            if (kind == TileArtKind.Fresh && _artCatalog != null)
            {
                var variant = _freshVariants.TryGetValue(coordinate, out var v) ? v : new FreshTileVariant(0, Quaternion.identity);
                texture = _artCatalog.GetFreshVariant(variant.TextureIndex);
                tileObject.transform.localRotation = variant.Rotation;
            }
            else
            {
                // Плиты со смыслом (источники/сигнатуры/ворота/рычаг/Алтарь/
                // позиция игрока) — ориентация постоянна (задача), даже если
                // эта же плита раньше была повёрнутым Fresh-полом. НЕ
                // Quaternion.identity — см. TopFaceOrientationCorrectionDegrees.
                tileObject.transform.localRotation = Quaternion.Euler(0f, TopFaceOrientationCorrectionDegrees, 0f);
                texture = kind != TileArtKind.None && _artCatalog != null ? _artCatalog.Get(kind) : null;
            }

            var material = renderer.material;
            if (texture != null)
            {
                material.mainTexture = texture;
                material.color = Color.white;
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
