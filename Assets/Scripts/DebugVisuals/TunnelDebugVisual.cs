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
    ///
    /// <b>Задача «разрушение плиты» (владелец, 2026-09-01):</b> раньше
    /// каждая плита получала СВОЙ экземпляр материала
    /// (<c>renderer.material = new Material(_templateMaterial)</c>) — при
    /// ~100 плитах на экране это ~100 материалов, каждый рвёт батчинг.
    /// Обязательное требование задачи — не создавать материал на плиту.
    /// Теперь все плиты делят ОДИН <see cref="_templateMaterial"/>
    /// (<c>renderer.sharedMaterial</c> + <c>enableInstancing</c>), а
    /// параметр, уникальный для конкретной плиты (текстура/цвет/сглаженность/
    /// эмиссия), передаётся через <see cref="MaterialPropertyBlock"/> —
    /// именно тот путь, который Unity предусмотрел для GPU-инстансинга
    /// (SRP Batcher продолжает работать, т.к. материал общий). Тот же
    /// принцип — для нового оверлея трещин (<see cref="_crackOverlayMaterial"/>).
    /// См. <see cref="ApplyVisual"/>/<see cref="UpdateCrackOverlay"/> и отчёт
    /// задачи — что выбрано и почему.
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

        // Задача «разрушение плиты»: MaterialPropertyBlock ОДИН, переиспользуется
        // на каждый вызов ApplyVisual/UpdateCrackOverlay (Clear() + заново
        // заполняется) — не аллоцируется на плиту и не на кадр, только сам
        // объект-контейнер создаётся один раз на всю жизнь TunnelDebugVisual.
        private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();
        private readonly bool _supportsSmoothness;
        private readonly bool _supportsEmission;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // Оверлей трещин (задача «разрушение плиты», п.1: "непрерывный
        // оверлей трещин... одна общая маска на весь набор"). Null — как и
        // _templateMaterial, оборонительно: либо шейдер не найден на этой
        // сборке, либо TileArtCatalog.CrackMaskTexture ещё не подключена
        // (ArtIntegrationSetup не запускался) — в обоих случаях оверлей
        // просто не создаётся, распад по-прежнему виден (через
        // TileDebugColor.ResolveDecayGradient — тот путь не тронут).
        private readonly Material _crackOverlayMaterial;
        private readonly Dictionary<GridCoordinate, GameObject> _overlayObjects = new Dictionary<GridCoordinate, GameObject>();

        // Задача «разрушение плиты», п.2 (обвал в конце). Состояние
        // анимации хранится отдельно от Core.Tile — это чисто визуальный
        // проигрыш, Tile уже необратимо разрушена (IsDestroyed), геометрия
        // просто донашивает анимацию поверх уже случившегося факта.
        private readonly struct CollapseState
        {
            public CollapseState(Vector3 basePosition, Quaternion baseRotation, Quaternion tiltTarget)
            {
                BasePosition = basePosition;
                BaseRotation = baseRotation;
                TiltTarget = tiltTarget;
            }

            public Vector3 BasePosition { get; }
            public Quaternion BaseRotation { get; }
            public Quaternion TiltTarget { get; }
        }

        private readonly Dictionary<GridCoordinate, CollapseState> _collapseStates = new Dictionary<GridCoordinate, CollapseState>();
        private readonly Dictionary<GridCoordinate, float> _collapseElapsedSeconds = new Dictionary<GridCoordinate, float>();
        private readonly HashSet<GridCoordinate> _previouslyDestroyed = new HashSet<GridCoordinate>();

        // Накопленное время — только для непрерывной визуальной пульсации
        // (см. UpdateCrackOverlay), синхронизировать с чем-то внешним не
        // нужно, важна только фаза синуса.
        private float _elapsedSeconds;

        // Задача «Ворота и Рычаги» (issue #193, владелец, 2026-09-01):
        // "закрытые Ворота указывают направление на свою открывашку" —
        // обязательное условие ("без него не реализовывать"), рычаг теперь
        // скрыт той же сигнатурой, что и ловушки, искать его наугад с
        // ценой в распад за каждое примеривание невозможно. Процедурная
        // геометрия (вытянутый куб-указатель, БЕЗ текстуры) — тот же
        // принцип "без арт-ассета", что партиклы PickupFeedback/осыпи
        // обвала: направление считается через Quaternion.LookRotation, не
        // требует UV/поворота текстуры, риска несовпадения "верха
        // картинки" с направлением здесь нет вовсе.
        private readonly Material _gateHintMaterial;
        private readonly Dictionary<GridCoordinate, GameObject> _gateDirectionHints = new Dictionary<GridCoordinate, GameObject>();

        public TunnelDebugVisual(TunnelGrid grid, GridTraceTrail trail, WorldGridProjection projection, Transform parent)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _projection = projection;
            _parent = parent;
            _templateMaterial = CreateTemplateMaterial();
            _artCatalog = TileArtCatalog.Load();
            _crackOverlayMaterial = CreateCrackOverlayMaterial(_artCatalog?.CrackMaskTexture);
            _gateHintMaterial = CreateGateHintMaterial();

            if (_templateMaterial != null)
            {
                _templateMaterial.enableInstancing = true;
                _supportsSmoothness = _templateMaterial.HasProperty("_Smoothness");
                _supportsEmission = _templateMaterial.HasProperty("_EmissionColor");
                // Задача «разрушение плиты»: раньше EnableKeyword/DisableKeyword
                // дёргались НА ЭКЗЕМПЛЯРЕ материала конкретной плиты (у каждой
                // был свой). Материал теперь общий — переключать ключевое
                // слово поинстансно уже нельзя. Вместо этого ключевое слово
                // включено здесь ОДИН РАЗ навсегда, а "светится плита или
                // нет" решает per-instance _EmissionColor через
                // MaterialPropertyBlock (см. ApplyVisual) — чёрный цвет
                // эмиссии визуально неотличим от выключенной эмиссии.
                if (_supportsEmission) _templateMaterial.EnableKeyword("_EMISSION");
            }

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

        /// <summary>
        /// Пересчитывает визуал всех созданных плит по их текущему
        /// состоянию и продвигает анимацию обрушения/пульсации.
        /// Не-op, если <see cref="IsEnabled"/> ложь.
        /// </summary>
        /// <param name="deltaSeconds">
        /// Реальное время с прошлого тика (задача «разрушение плиты») —
        /// нужно для анимации обвала (<see cref="DecayCollapseFeedback.CollapseDurationSeconds"/>)
        /// и фазы визуальной пульсации (см. <see cref="UpdateCrackOverlay"/>).
        /// </param>
        public void Tick(float deltaSeconds)
        {
            if (!IsEnabled) return;

            _elapsedSeconds += Mathf.Max(0f, deltaSeconds);

            var startCoordinate = _trail.Path[0];
            var currentCoordinate = _trail.CurrentPosition;

            foreach (var pair in _tileObjects)
            {
                var coordinate = pair.Key;
                var tileObject = pair.Value;
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

                var kind = ApplyVisual(tileObject, coordinate, state);
                UpdateCrackOverlay(coordinate, kind, state);
                UpdateGateDirectionHint(coordinate, tileObject, tile);

                if (state.IsDestroyed && !_previouslyDestroyed.Contains(coordinate))
                {
                    _previouslyDestroyed.Add(coordinate);
                    TriggerCollapse(coordinate, tileObject);
                }

                if (_collapseElapsedSeconds.ContainsKey(coordinate))
                    AdvanceCollapse(coordinate, tileObject, deltaSeconds);
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

                foreach (var overlayObject in _overlayObjects.Values)
                    UnityEngine.Object.Destroy(overlayObject);
                _overlayObjects.Clear();

                foreach (var hintObject in _gateDirectionHints.Values)
                    UnityEngine.Object.Destroy(hintObject);
                _gateDirectionHints.Clear();

                _collapseStates.Clear();
                _collapseElapsedSeconds.Clear();
                _previouslyDestroyed.Clear();

                UnityEngine.Object.Destroy(_templateMaterial);
                if (_crackOverlayMaterial != null) UnityEngine.Object.Destroy(_crackOverlayMaterial);
                if (_gateHintMaterial != null) UnityEngine.Object.Destroy(_gateHintMaterial);
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
            // Задача «разрушение плиты»: sharedMaterial, не "new Material(...)"
            // на каждую плиту (см. doc-комментарий класса — обязательное
            // требование, ~100 плит на экране не должны рвать батчинг).
            if (renderer != null) renderer.sharedMaterial = _templateMaterial;

            _tileObjects[tile.Coordinate] = primitive;
            // Задача «тёплый набор плит»: выбор фиксируется один раз здесь,
            // не в ApplyVisual — иначе вариант/поворот "плавали" бы каждый
            // кадр, пока плита ещё Fresh (см. PickFreshVariant/ApplyVisual).
            _freshVariants[tile.Coordinate] = PickFreshVariant();

            CreateCrackOverlayObject(tile.Coordinate, primitive.transform.position);
        }

        // Задача «разрушение плиты»: небольшой зазор над верхней гранью
        // куба — избегает z-fighting между базовой текстурой плиты и
        // оверлеем трещин поверх неё.
        private const float CrackOverlayHeightOffset = 0.003f;

        // Задача «разрушение плиты», найдено на реальном Android-билде
        // (2026-09-01): `GameObject.CreatePrimitive(PrimitiveType.Quad)`
        // ВНУТРИ движка сам добавляет `MeshCollider` — на этой сборке класс
        // оказался вырезан стриппингом (тот же класс проблем, что уже ловили
        // на `Shader.Find` 2026-08-14, см. doc-комментарий класса), и на
        // устройстве это заваливало экран сотнями "Can't add component
        // because class 'MeshCollider' doesn't exist!" (Development Console
        // оверлей) — по одной на каждый созданный тайл. Тапу коллайдер
        // оверлея всё равно не нужен (см. TileVisualMarker на базовой
        // плите) — вместо CreatePrimitive строим GameObject вручную из
        // готового меша (тот же `Quad.fbx`, что использует сам CreatePrimitive
        // под капотом) и MeshRenderer, коллайдер вообще никогда не создаётся
        // и не уничтожается постфактум.
        private static Mesh _overlayQuadMesh;

        private static Mesh GetOverlayQuadMesh()
        {
            if (_overlayQuadMesh == null) _overlayQuadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            return _overlayQuadMesh;
        }

        private void CreateCrackOverlayObject(GridCoordinate coordinate, Vector3 tileWorldPosition)
        {
            if (_crackOverlayMaterial == null) return;

            var overlay = new GameObject($"DebugTileCrackOverlay {coordinate}");
            overlay.AddComponent<MeshFilter>().sharedMesh = GetOverlayQuadMesh();
            overlay.AddComponent<MeshRenderer>();

            overlay.transform.SetParent(_parent, worldPositionStays: false);
            overlay.transform.position = tileWorldPosition + new Vector3(0f, TileHeight * 0.5f + CrackOverlayHeightOffset, 0f);
            // Фиксированный поворот, без учёта случайного 0/90/180/270°
            // Fresh-варианта (PickFreshVariant) — маска трещин не несёт
            // читаемого "верха" как иконки (ключ/Мана/т.п.), согласовывать
            // её поворот с полом не требуется для читаемости.
            overlay.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            overlay.transform.localScale = new Vector3(_projection.TileSize * TileScale, _projection.TileSize * TileScale, 1f);

            var overlayRenderer = overlay.GetComponent<Renderer>();
            if (overlayRenderer != null) overlayRenderer.sharedMaterial = _crackOverlayMaterial;

            // Скрыт, пока UpdateCrackOverlay не найдёт прогресс распада > 0
            // (обычная нераспадающаяся плита не должна показывать трещины).
            overlay.SetActive(false);
            _overlayObjects[coordinate] = overlay;
        }

        // Задача «разрушение плиты»: обвал сдвигает плиту вниз на глубину,
        // заметно превышающую её собственную толщину (TileHeight) — должно
        // читаться как "провалилась", а не "чуть просела".
        private const float CollapseSinkDepth = 0.4f;
        private const float CollapseTiltMinDegrees = 20f;
        private const float CollapseTiltMaxDegrees = 45f;

        private void TriggerCollapse(GridCoordinate coordinate, GameObject tileObject)
        {
            var basePosition = tileObject.transform.position;
            var baseRotation = tileObject.transform.localRotation;

            // Случайная ось наклона в плоскости пола (не вокруг вертикали —
            // крен должен выглядеть как падение обломка, не как поворот на
            // месте) и случайный угол в разумных пределах.
            var tiltAxis = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f));
            if (tiltAxis.sqrMagnitude < 0.0001f) tiltAxis = Vector3.right;
            tiltAxis.Normalize();
            var tiltAngle = UnityEngine.Random.Range(CollapseTiltMinDegrees, CollapseTiltMaxDegrees);
            var tiltTarget = Quaternion.AngleAxis(tiltAngle, tiltAxis) * baseRotation;

            _collapseStates[coordinate] = new CollapseState(basePosition, baseRotation, tiltTarget);
            _collapseElapsedSeconds[coordinate] = 0f;

            SpawnCollapseDebris(basePosition);
        }

        private void AdvanceCollapse(GridCoordinate coordinate, GameObject tileObject, float deltaSeconds)
        {
            var elapsed = _collapseElapsedSeconds[coordinate] + Mathf.Max(0f, deltaSeconds);
            _collapseElapsedSeconds[coordinate] = elapsed;

            var duration = Mathf.Max(0.01f, DecayCollapseFeedback.CollapseDurationSeconds);
            var t = Mathf.Clamp01(elapsed / duration);
            var eased = t * t; // ease-in — обвал ускоряется по ходу падения, не едет равномерно

            var collapse = _collapseStates[coordinate];
            tileObject.transform.position = collapse.BasePosition - Vector3.up * (CollapseSinkDepth * eased);
            tileObject.transform.localRotation = Quaternion.Slerp(collapse.BaseRotation, collapse.TiltTarget, eased);
            // t достигает 1 — анимация просто перестаёт продвигаться дальше
            // (elapsed продолжает расти, но eased уже зажат в 1), плита
            // остаётся в осевшей позе до конца забега. Из
            // _collapseElapsedSeconds координату намеренно не убираем —
            // иначе ApplyVisual на следующем Tick() отменил бы крен обратно
            // до TopFaceOrientationCorrectionDegrees (см. её ветку "else").
        }

        // Партиклы осыпи — тот же процедурный приём без арт-ассета, что
        // PickupFeedback.SpawnBurst (её doc-комментарий: "прямой запрос
        // владельца продукта... без единого арт-ассета"), тонировка — через
        // startColor.
        //
        // <b>Найдено на реальном Android-билде (2026-09-01), тот же класс
        // бага, что MeshCollider выше:</b> PickupFeedback НЕ назначает
        // материал вручную (полагается на дефолтный материал
        // ParticleSystemRenderer) — по её же комментарию это осознанный
        // выбор, "здесь его просто негде допустить" (Shader.Find). На ЭТОЙ
        // сборке дефолтный материал партикла оказался закрашен ярко-розовым
        // (классический Unity-фолбэк "шейдер не найден") — та же категория
        // риска шейдер-стриппинга, что уже ловили на "Universal Render
        // Pipeline/Lit" (2026-08-14) и на "Sprites/Default" в этой же задаче:
        // "дефолтный" не значит "всегда включённый в билд". Не трогаем
        // PickupFeedback (чужой, уже протестированный код, вне скоупа этой
        // задачи) — здесь материал назначается явно, тем же уже проверенным
        // и добавленным в Always Included Shaders "Sprites/Default"
        // (см. CreateCrackOverlayMaterial/BuildScript.FixRenderingConfiguration),
        // не третий новый шейдер, а переиспользование уже подтверждённого.
        private const float DebrisLifetimeSeconds = 0.7f;
        private static readonly Color DebrisColor = new Color(90f / 255f, 70f / 255f, 55f / 255f);
        private static Material _debrisMaterial;

        private static void SpawnCollapseDebris(Vector3 worldPosition)
        {
            var host = new GameObject("DebugTileCollapseDebris");
            host.transform.position = worldPosition;

            var particles = host.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.duration = DebrisLifetimeSeconds;
            main.loop = false;
            main.startLifetime = DebrisLifetimeSeconds;
            main.startSpeed = 1.2f;
            main.startSize = 0.12f;
            main.startColor = DebrisColor;
            main.gravityModifier = 1.5f; // осколки падают, не разлетаются в невесомости
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            if (_debrisMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) _debrisMaterial = new Material(shader);
            }

            if (_debrisMaterial != null)
            {
                var particleRenderer = host.GetComponent<ParticleSystemRenderer>();
                if (particleRenderer != null) particleRenderer.sharedMaterial = _debrisMaterial;
            }

            UnityEngine.Object.Destroy(host, DebrisLifetimeSeconds + 0.2f);
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
        /// <b>Задача «разрушение плиты»:</b> раньше писала прямо в
        /// <c>renderer.material</c> (свой экземпляр на каждую плиту) —
        /// теперь заполняет переиспользуемый <see cref="_propertyBlock"/> и
        /// применяет его к <c>renderer.sharedMaterial</c> (см.
        /// doc-комментарий класса). Возвращает вычисленный
        /// <see cref="TileArtKind"/> — нужен вызывающему <see cref="Tick"/>
        /// для <see cref="UpdateCrackOverlay"/>, чтобы не резолвить состояние
        /// дважды.
        /// </summary>
        private TileArtKind ApplyVisual(GameObject tileObject, GridCoordinate coordinate, TileVisualState state)
        {
            var renderer = tileObject.GetComponent<Renderer>();
            if (renderer == null) return TileArtKind.None;

            var kind = TileArtKindResolver.Resolve(state);
            Texture2D texture;
            if (kind == TileArtKind.Fresh && _artCatalog != null)
            {
                var variant = _freshVariants.TryGetValue(coordinate, out var v) ? v : new FreshTileVariant(0, Quaternion.identity);
                texture = _artCatalog.GetFreshVariant(variant.TextureIndex);
                // Во время анимации обвала (см. AdvanceCollapse) поворотом
                // владеет она — Fresh здесь означает "ещё не разрушена",
                // конфликта нет (обвал начинается только на IsDestroyed).
                tileObject.transform.localRotation = variant.Rotation;
            }
            else
            {
                // Плиты со смыслом (источники/сигнатуры/ворота/рычаг/Алтарь/
                // позиция игрока) — ориентация постоянна (задача), даже если
                // эта же плита раньше была повёрнутым Fresh-полом. НЕ
                // Quaternion.identity — см. TopFaceOrientationCorrectionDegrees.
                // Destroyed — тоже сюда, но AdvanceCollapse перезапишет
                // rotation следом в этом же Tick(), если обвал уже идёт.
                tileObject.transform.localRotation = Quaternion.Euler(0f, TopFaceOrientationCorrectionDegrees, 0f);
                texture = kind != TileArtKind.None && _artCatalog != null ? _artCatalog.Get(kind) : null;
            }

            _propertyBlock.Clear();
            if (texture != null)
            {
                // _BaseMap/_MainTex и _BaseColor/_Color выставлены ОБА —
                // MaterialPropertyBlock молча игнорирует имя свойства,
                // которого нет в шейдере назначенного материала, это
                // безопасно и покрывает все три варианта из
                // CreateTemplateMaterial (URP/Lit, Standard, Unlit/Color)
                // одним и тем же кодом без ветвления по типу шейдера.
                _propertyBlock.SetTexture(BaseMapId, texture);
                _propertyBlock.SetTexture(MainTexId, texture);
                _propertyBlock.SetColor(BaseColorId, Color.white);
                _propertyBlock.SetColor(ColorId, Color.white);
                if (_supportsSmoothness) _propertyBlock.SetFloat(SmoothnessId, 0.35f);
                if (_supportsEmission) _propertyBlock.SetColor(EmissionColorId, Color.black);
            }
            else
            {
                var color = TileDebugColor.Resolve(state);
                _propertyBlock.SetColor(BaseColorId, color);
                _propertyBlock.SetColor(ColorId, color);
                if (_supportsSmoothness) _propertyBlock.SetFloat(SmoothnessId, 0.55f);
                if (_supportsEmission) _propertyBlock.SetColor(EmissionColorId, color * EmissionIntensity);
            }

            renderer.SetPropertyBlock(_propertyBlock);
            return kind;
        }

        /// <summary>
        /// Задача «разрушение плиты», п.1 и п.3: непрерывный оверлей трещин
        /// (альфа = <see cref="Tile.DecayProgress01"/>, не дискретный
        /// <see cref="TileArtKind"/>) плюс визуальная половина пульсации в
        /// последней трети распада (звук/вибро — <see cref="DecayPulseController"/>).
        /// Видим только там, куда доходит <see cref="TileArtKindResolver.ResolveDecayGradient"/>
        /// (сейчас — всегда <see cref="TileArtKind.Fresh"/>) — на структурных
        /// плитах (стена/ловушка/источник/Алтарь/...) распад не рисуется
        /// вовсе, тот же порядок приоритета, что у самого резолвера, а не
        /// отдельный параллельный список веток, который мог бы разойтись с
        /// ним со временем.
        /// </summary>
        private void UpdateCrackOverlay(GridCoordinate coordinate, TileArtKind kind, TileVisualState state)
        {
            if (!_overlayObjects.TryGetValue(coordinate, out var overlay)) return;

            var progress01 = Mathf.Clamp01(state.DecayProgress01);
            var visible = kind == TileArtKind.Fresh && progress01 > 0f;

            if (overlay.activeSelf != visible) overlay.SetActive(visible);
            if (!visible) return;

            var pulseBoost = 0f;
            if (state.IsCurrentPosition && progress01 >= DecayPulseController.LastThirdThreshold01)
            {
                var withinLastThird01 = Mathf.InverseLerp(DecayPulseController.LastThirdThreshold01, 1f, progress01);
                // Частота пульса растёт вместе с прогрессом — тот же принцип
                // учащения, что у DecayPulseController.Update() для
                // звука/вибро (оба — визуальная и звуко-вибро половины
                // ОДНОГО непрерывного сигнала, задача п.3).
                var pulseFrequencyHz = Mathf.Lerp(2f, 9f, withinLastThird01);
                pulseBoost = (Mathf.Sin(_elapsedSeconds * pulseFrequencyHz * Mathf.PI * 2f) * 0.5f + 0.5f) * 0.35f * withinLastThird01;
            }

            var alpha = Mathf.Clamp01(progress01 + pulseBoost);

            var overlayRenderer = overlay.GetComponent<Renderer>();
            if (overlayRenderer == null) return;

            _propertyBlock.Clear();
            _propertyBlock.SetColor(ColorId, new Color(1f, 1f, 1f, alpha));
            overlayRenderer.SetPropertyBlock(_propertyBlock);
        }

        // Высота над плитой (тот же принцип, что CrackOverlayHeightOffset —
        // зазор от верхней грани куба, чтобы не резать полигоны насквозь);
        // указатель ощутимо выше оверлея трещин — гейт-плита их не носит
        // одновременно (Gated не распадается, см. TileDebugColor/
        // TileArtKindResolver — LethalTrap/градиент распада проверяются
        // раньше и уже "заняты" другими типами плит), но высота на всякий
        // случай разведена.
        private const float GateHintHeightOffset = 0.06f;
        private const float GateHintLength = 0.5f; // доля от TileSize
        private const float GateHintThickness = 0.08f; // доля от TileSize

        /// <summary>
        /// Issue #193 (владелец, 2026-09-01): "закрытые Ворота указывают
        /// направление на свою открывашку" — обязательное условие задачи,
        /// без него рычаг (теперь скрытый той же сигнатурой, что и ловушки)
        /// пришлось бы искать наугад, а каждое примеривание стоит игроку
        /// распада — без подсказки поиск был бы невыгоден и игрок бы его
        /// не предпринимал. Процедурный вытянутый куб-указатель (см.
        /// doc-комментарий <see cref="_gateHintMaterial"/>) — создаётся
        /// лениво при первом Tick(), где плита оказывается Gated с известной
        /// <see cref="Tile.LeverCoordinate"/> (тот же принцип "по требованию
        /// из реального Tile.cs", что overlay трещин), дальше только
        /// включается/выключается вместе с <see cref="Tile.IsLeverGateOpen"/>
        /// — открытые Ворота уже не тайна, подсказка не нужна.
        /// </summary>
        private void UpdateGateDirectionHint(GridCoordinate coordinate, GameObject tileObject, Tile tile)
        {
            if (_gateHintMaterial == null) return;
            if (!tile.IsGated || !tile.LeverCoordinate.HasValue)
            {
                // Плита перестала бы быть Gated только при полном
                // пересборе сетки нового забега (Dispose очищает всё) — но
                // защищаемся симметрично на случай уже созданной подсказки.
                if (_gateDirectionHints.TryGetValue(coordinate, out var staleHint)) staleHint.SetActive(false);
                return;
            }

            if (!_gateDirectionHints.TryGetValue(coordinate, out var hint))
            {
                hint = CreateGateDirectionHint(coordinate, tileObject.transform.position, tile.LeverCoordinate.Value);
                _gateDirectionHints[coordinate] = hint;
            }

            if (hint == null) return;
            hint.SetActive(!tile.IsLeverGateOpen);
        }

        private GameObject CreateGateDirectionHint(GridCoordinate gateCoordinate, Vector3 gateWorldPosition, GridCoordinate leverCoordinate)
        {
            var hint = new GameObject($"DebugGateDirectionHint {gateCoordinate}");
            hint.transform.SetParent(_parent, worldPositionStays: false);

            var leverWorldPosition = _projection.ToWorldPosition(leverCoordinate);
            var direction = leverWorldPosition - gateWorldPosition;
            direction.y = 0f;
            // Рычаг и Ворота физически не могут совпасть координатой (issue
            // #193/SegmentTemplate.ValidateLeverGates), но защищаемся от
            // вырожденного вектора на случай будущих изменений генерации.
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;

            hint.transform.position = gateWorldPosition + Vector3.up * (TileHeight * 0.5f + GateHintHeightOffset);
            hint.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            hint.transform.localScale = new Vector3(
                _projection.TileSize * GateHintThickness,
                _projection.TileSize * GateHintThickness,
                _projection.TileSize * GateHintLength);

            var meshFilter = hint.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = GetGateHintMesh();
            hint.AddComponent<MeshRenderer>().sharedMaterial = _gateHintMaterial;
            // Ни один Collider не добавляется вообще (тот же приём, что
            // GetOverlayQuadMesh — прямая сборка меша в обход
            // GameObject.CreatePrimitive, где Unity сама цепляет коллайдер) —
            // тап должен попадать только в BoxCollider базовой плиты.

            return hint;
        }

        // Тот же приём, что GetOverlayQuadMesh — берём готовый Cube.fbx
        // напрямую из Resources.GetBuiltinResource, минуя
        // GameObject.CreatePrimitive (сама добавляет коллайдер, см.
        // doc-комментарий класса про баг MeshCollider 2026-09-01).
        private static Mesh _gateHintMesh;

        private static Mesh GetGateHintMesh()
        {
            if (_gateHintMesh == null) _gateHintMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            return _gateHintMesh;
        }

        private static Material CreateTemplateMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");
            return shader != null ? new Material(shader) : null;
        }

        /// <summary>
        /// Задача «разрушение плиты»: <c>Sprites/Default</c> — не
        /// URP-специфичный шейдер, встроен в Unity практически всегда
        /// (даже когда URP-стриппинг на устройстве вырезает
        /// "Universal Render Pipeline/Lit", см. doc-комментарий класса про
        /// баг 2026-08-14), поддерживает альфа-блендинг "из коробки" (в
        /// отличие от непрозрачного <c>Unlit/Color</c> — последнего резерва
        /// <see cref="CreateTemplateMaterial"/>) и имеет ровно те свойства
        /// (<c>_MainTex</c>/<c>_Color</c>), которые нужны для маски трещин.
        /// Не собственный HLSL-шейдер — риск ошибки компиляции без
        /// возможности быстро итерировать в этом окружении не оправдан для
        /// декоративного оверлея. Null, если даже этот шейдер недоступен —
        /// вызывающий код просто не создаёт оверлей (см. <see cref="CreateCrackOverlayObject"/>),
        /// та же оборонительная логика, что у отсутствующего базового шейдера.
        /// </summary>
        private static Material CreateCrackOverlayMaterial(Texture2D crackMask)
        {
            if (crackMask == null) return null;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;

            var material = new Material(shader) { mainTexture = crackMask, color = Color.white };
            material.enableInstancing = true;
            return material;
        }

        // Ярко-голубой (циан) — не пересекается ни с одним уже занятым
        // цветом debug-палитры (TileDebugColor): опасность/сигнатуры —
        // сизо-фиолетовый/оливково-жёлтый/пурпур, ворота — янтарный,
        // источники — фиолетовый/медный. Циан читается как явно
        // служебная/навигационная подсказка, не игровая сущность.
        private static readonly Color GateHintColor = new Color(0.2f, 0.9f, 1f);

        /// <summary>
        /// Материал для указателя направления на рычаг (issue #193) — тот
        /// же fallback-список шейдеров, что <see cref="CreateTemplateMaterial"/>
        /// (уже подтверждён на устройстве, уже в Always Included Shaders,
        /// см. <c>BuildScript.FixRenderingConfiguration</c>) — не текстура,
        /// поэтому не нужен отдельный шейдер, как у оверлея трещин.
        /// </summary>
        private static Material CreateGateHintMaterial()
        {
            var material = CreateTemplateMaterial();
            if (material == null) return null;

            material.color = GateHintColor;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", GateHintColor * 0.6f); // заметен даже в тени — тот же приём, что EmissionIntensity у цветного фолбэка
            }
            material.enableInstancing = true;
            return material;
        }
    }
}
