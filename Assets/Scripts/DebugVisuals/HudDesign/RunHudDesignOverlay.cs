using Burmalda.Bootstrap;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Movement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals.HudDesign
{
    /// <summary>
    /// HUD забега по дизайн-системе Claude Design (issue "Собрать Unity UI
    /// для HUD забега"), см. <c>docs/design/burmalda-hud-design-spec.md</c>.
    /// Строится кодом через Canvas/UI (ScaleWithScreenSize, референс
    /// 720×1600 — тот же масштаб, что в спеке), НЕ .prefab — тот же принцип,
    /// что у остальных UI-классов проекта (docs/rules/forbidden-actions.md).
    ///
    /// Состояния 1/2 (пусто/обычный тоннель) — один и тот же построенный
    /// UI, просто с разными живыми значениями: разницы в коде между ними
    /// нет, разница только в данных (issue сама описывает их как одно
    /// дерево). Состояние 3 (примеривание) — притушение части HUD + красная
    /// пульсирующая рамка + фиксированная подсказка, привязано к РЕАЛЬНОМУ
    /// событию (<see cref="GridTraceInputController.PreviewTarget"/>).
    /// Состояние 4 (Комната Босса) — статичный, неактивный по умолчанию
    /// каркас без единой связи с игровыми данными (см. doc-комментарий
    /// <see cref="BuildBossRoomShell"/>), виден только через
    /// <see cref="RunHudToggles.ShowBossRoomPreview"/> (<see cref="RunHudTogglePanel"/>) —
    /// задача «на игровом поле не должно быть ни одной отладочной кнопки»:
    /// раньше здесь была постоянная кнопка "Preview Boss Room" ровно там,
    /// куда игрок тапает при ходе.
    ///
    /// Этот класс — единственный player-facing HUD, остаётся без тумблера
    /// (не debug-инструмент). <see cref="RunHudOverlay"/> — отдельный,
    /// чисто отладочный текстовый слой поверх, дефолт ВЫКЛ (задача «HUD
    /// накладывается сам на себя» — оба default-ВКЛ рисовали дублирующие
    /// числа валют одновременно, владелец продукта увидел на устройстве
    /// буквально перечёркнутые друг другом цифры).
    /// </summary>
    public sealed class RunHudDesignOverlay : MonoBehaviour
    {
        private const float ReferenceWidth = 720f;
        private const float ReferenceHeight = 1600f;
        private const float BottomThumbZoneHeight = ReferenceHeight / 3f; // 533px — issue: "туда ничего не класть"
        private const float TopMargin = 96f; // ниже CAM/HUD (левый/центр) — тем не грозит пересечение с этим Canvas

        /// <summary>
        /// Задача «HUD накладывается сам на себя» (плейтест владельца:
        /// «ЯРУС 0» налезал на кнопку ECO) — TopMargin=96 был выбран "на
        /// глаз", без пересчёта в реальные device-пиксели. У этого Canvas
        /// (<see cref="CanvasScaler.ScaleMode.ScaleWithScreenSize"/>, референс
        /// 720×1600) 96 своих единиц — НЕ 96 экранных пикселей: на конкретном
        /// устройстве (Realme, 1080×2400) масштаб ×1.5, то есть фактически
        /// 144px — а <see cref="DebugPanelLayout.TopRightReservedHeight"/>
        /// (RESTART+ECO+TO CAMP) занимает 234 "сырых" device-пикселя, реально
        /// не занятых текстом "ЯРУС", даже если 144 больше формального 96.
        /// Отступ "ЯРУС" (правый верхний угол — единственный, где player-facing
        /// HUD физически соседствует с колонкой debug-кнопок) считается через
        /// эту функцию: резерв в device-пикселях делится на масштаб этого
        /// Canvas, переводя его в единицы референс-пространства — независимо
        /// от размера экрана устройства отступ гарантированно достаточен, не
        /// жёстко вписанное число, актуальное только для одного экрана.
        ///
        /// Масштаб — та же формула, что <see cref="CanvasScaler"/> сам
        /// использует для <see cref="CanvasScaler.ScaleMode.ScaleWithScreenSize"/>
        /// (см. её исходники), пересчитана напрямую из <see cref="Screen.width"/>/
        /// <see cref="Screen.height"/>, а не прочитана из компонента: на
        /// первом кадре, когда строится этот HUD (<see cref="BuildUi"/>,
        /// вызывается из <see cref="Start"/>), сам Canvas ещё не проходил
        /// ни один layout-пасс — <c>CanvasScaler.scaleFactor</c> в этот
        /// момент не гарантированно уже посчитан.
        /// </summary>
        private float TierTopOffset
        {
            get
            {
                var logWidth = Mathf.Log(Screen.width / ReferenceWidth, 2f);
                var logHeight = Mathf.Log(Screen.height / ReferenceHeight, 2f);
                var scaleFactor = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, 0.5f)); // 0.5f — тот же matchWidthOrHeight, что задан на Canvas ниже (BuildUi)
                return TopMargin + DebugPanelLayout.TopRightReservedHeight / scaleFactor;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(RunHudDesignOverlay));
            host.AddComponent<RunHudDesignOverlay>();
            DontDestroyOnLoad(host);
        }

        private GridTraceInputController _input;
        private CurrencyController _currency;
        private Camera _mainCamera;

        private RectTransform _canvasRect;
        private CanvasGroup _dimGroup;
        private HudCounterAnimator _manaAnimator;
        private HudCounterAnimator _keysAnimator;
        private Text _tierText;

        private GameObject _aimTooltipRoot;
        private RectTransform _aimTooltipRect;
        private GameObject _aimHighlight;

        private GameObject _bossRoomRoot;

        private GridCoordinate? _wiredPreviewTarget;

        private void Start()
        {
            EnsureEventSystemExists();
            BuildUi();
        }

        private static void EnsureEventSystemExists()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var host = new GameObject(nameof(EventSystem));
            DontDestroyOnLoad(host);
            host.AddComponent<EventSystem>();
            host.AddComponent<InputSystemUIInputModule>();
        }

        private void Update()
        {
            // Читаем из RunBootstrap (задача "композиционный корень
            // RunBootstrap") — единственный источник живых игровых систем.
            _input = RunBootstrap.Instance?.Input;
            _currency = RunBootstrap.Instance?.Currency;
            if (_mainCamera == null) _mainCamera = Camera.main;

            UpdateCurrencyCounters();
            UpdateAimState();
            UpdateBossRoomPreview();
        }

        // Задача «на игровом поле не должно быть ни одной отладочной
        // кнопки»: раньше "Preview Boss Room" висела прямо на поле, ровно
        // там, куда игрок тапает при ходе (BuildBossRoomPreviewToggle,
        // удалена). Теперь — тумблер в RunHudTogglePanel, дефолт ВЫКЛ;
        // SetActive идемпотентен, лишний вызов на кадр, где ничего не
        // изменилось, безвреден.
        private void UpdateBossRoomPreview()
        {
            if (_bossRoomRoot == null) return;
            if (_bossRoomRoot.activeSelf != RunHudToggles.ShowBossRoomPreview)
                _bossRoomRoot.SetActive(RunHudToggles.ShowBossRoomPreview);
        }

        private void UpdateCurrencyCounters()
        {
            if (_currency == null) return;
            if (_currency.RunManaCrystals != null) _manaAnimator.SetTarget(_currency.RunManaCrystals.Total);
            if (_currency.RunKeys != null) _keysAnimator.SetTarget(_currency.RunKeys.Total);

            // Ярус (design-spec.md: "подключи к RunDepthTier") — читаем ЖИВОЙ
            // экземпляр через RunBootstrap (задача "композиционный корень
            // RunBootstrap"), тот же, что реально продвигается победой над
            // Боссом (BossController.DepthTier) — раньше здесь держался
            // собственный пустой RunDepthTier, никогда не продвигавшийся
            // (честно показывал 0, но не тот 0, что в игре). Нет живого
            // источника — прочерк, не ноль (см. doc-комментарий задачи).
            if (_tierText != null)
            {
                var depthTier = RunBootstrap.Instance?.DepthTier;
                _tierText.text = depthTier != null ? $"ЯРУС {depthTier.CurrentTier}" : "ЯРУС —";
            }
        }

        private void UpdateAimState()
        {
            if (_input == null) return;

            var target = _input.PreviewTarget;
            if (target.Equals(_wiredPreviewTarget)) return;
            _wiredPreviewTarget = target;

            var aiming = target.HasValue;
            _dimGroup.alpha = aiming ? 0.55f : 1f; // притушение — общая обратная связь на ЛЮБОЕ примеривание (PRD 4.1 п.1), не только опасное

            // Задача «сделать тоннель играбельным», часть 4 (баг с плейтеста
            // владельца): раньше рамка+подсказка показывались на КАЖДОЙ
            // примериваемой плите (aiming == target.HasValue, без проверки
            // содержимого) — "непонятный тултип при наведении на каждую
            // плитку". Подсказка "с этой плитой что-то не так" осмысленна
            // только для триггера одной из пяти ловушек (тот же признак, что
            // уже гейтит текстовую строку в TilePreviewController) — активная
            // ловушка (Tile.LethalTrap) уже видна собственным цветом/
            // текстурой на полу (владелец, 2026-09-05 «оставить только пять
            // новых ловушек»: понятие "скрытая ловушка" и Core.TrapSignature
            // удалены — ни один LethalTrapType больше не бывает скрытым),
            // повторный неспецифичный тултип по ней был бы избыточен и
            // путал бы с реальной сигнатурой.
            var showDangerSignature = aiming && IsTrapTriggerTile(target.Value);
            _aimHighlight.SetActive(showDangerSignature);
            _aimTooltipRoot.SetActive(showDangerSignature);

            if (showDangerSignature) PositionAimVisuals(target.Value);
        }

        // Тот же набор триггеров, что Movement.TrapRevealSystem.HasHiddenDanger.
        private bool IsTrapTriggerTile(GridCoordinate coordinate) =>
            _input.Grid != null && _input.Grid.TryGetTile(coordinate, out var tile) &&
            (tile.ArrowWaveTargetRow.HasValue || tile.IsBombTrigger || tile.BladeTactTargetRow.HasValue ||
             tile.IsFallingRockTrigger || tile.IsLavaTrigger);

        private void PositionAimVisuals(GridCoordinate coordinate)
        {
            // WorldGridProjection — readonly struct, никогда не null (в отличие от Grid/Trail) — сравнивать не с чем.
            var worldPosition = _input.Projection.ToWorldPosition(coordinate) + Vector3.up * 0.2f;
            _aimHighlight.transform.position = worldPosition;

            if (_mainCamera == null) return;
            var screenPoint = _mainCamera.WorldToScreenPoint(worldPosition + Vector3.up * 0.6f);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPoint, null, out var localPoint))
                _aimTooltipRect.anchoredPosition = localPoint;
        }

        // ==================== Построение UI ====================

        private void BuildUi()
        {
            var canvasHost = new GameObject(nameof(RunHudDesignOverlay) + "_Canvas");
            canvasHost.transform.SetParent(transform, worldPositionStays: false);

            var canvas = canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasHost.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasHost.AddComponent<GraphicRaycaster>();
            _canvasRect = (RectTransform)canvasHost.transform;

            BuildNormalHud(canvasHost.transform);
            BuildAimVisuals(canvasHost.transform);
            BuildBossRoomShell(canvasHost.transform);
        }

        // Состояния 1/2 — один и тот же UI, живые значения делают разницу (см. doc-комментарий класса).
        private void BuildNormalHud(Transform parent)
        {
            // Мана — ВСЕГДА полная непрозрачность (issue: "кроме текущего числа маны и яруса").
            var manaCounter = HudUiPrimitives.CreateCurrencyCounter(parent, "ManaCounter", BurmaldaHudPalette.ManaPurple, HudUiPrimitives.CurrencyIconShape.Diamond);
            AnchorTopLeft((RectTransform)manaCounter.ValueText.transform.parent, new Vector2(24f, -TopMargin), new Vector2(160f, 40f));
            _manaAnimator = manaCounter.ValueText.gameObject.AddComponent<HudCounterAnimator>();

            // Ярус — ВСЕГДА полная непрозрачность, справа сверху. Отступ —
            // TierTopOffset, не голый TopMargin (см. её doc-комментарий):
            // единственный элемент этого HUD, соседствующий с колонкой
            // debug-кнопок RESTART/ECO/TO CAMP в том же верхнем правом углу.
            _tierText = HudUiPrimitives.CreateLabel(parent, "TierLabel", "ЯРУС 0", 20, BurmaldaHudPalette.TextSecondary, bold: true, TextAnchor.MiddleRight);
            AnchorTopRight((RectTransform)_tierText.transform, new Vector2(-24f, -TierTopOffset), new Vector2(160f, 32f));

            // Всё остальное (Монеты, Ключи, слоты билда, вес) — притушается при примеривании.
            var dimHost = HudUiPrimitives.CreateRect(parent, "DimmableGroup");
            dimHost.anchorMin = Vector2.zero;
            dimHost.anchorMax = Vector2.one;
            dimHost.offsetMin = Vector2.zero;
            dimHost.offsetMax = Vector2.zero;
            _dimGroup = dimHost.gameObject.AddComponent<CanvasGroup>();

            // Монеты убраны из этого HUD (PRD v9 раздел 5, задача по экономике
            // "Мана как доход забега, Монеты только в Лагере", issue #180) —
            // в забеге эта валюта больше не существует вовсе, ни начисления,
            // ни отображения. Раньше здесь был CoinsCounter, читавший
            // CurrencyController.RunCoins — само поле удалено этой задачей.
            var keysCounter = HudUiPrimitives.CreateCurrencyCounter(dimHost, "KeysCounter", BurmaldaHudPalette.CopperCoin, HudUiPrimitives.CurrencyIconShape.Circle);
            AnchorTopLeft((RectTransform)keysCounter.ValueText.transform.parent, new Vector2(24f, -TopMargin - 44f), new Vector2(160f, 40f));
            _keysAnimator = keysCounter.ValueText.gameObject.AddComponent<HudCounterAnimator>();

            BuildLoadoutSlotsRow(dimHost, new Vector2(24f, -TopMargin - 106f));
            BuildWeightPlaceholder(dimHost, new Vector2(24f, -TopMargin - 182f));
        }

        // issue: "здесь стоп — в коде нет понятия «активная сборка на забег», собери визуальный ряд, данные не подставляй".
        private void BuildLoadoutSlotsRow(Transform parent, Vector2 topLeftOffset)
        {
            var row = HudUiPrimitives.CreateRect(parent, "LoadoutSlotsRow");
            AnchorTopLeft(row, topLeftOffset, new Vector2(340f, 64f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            for (var i = 0; i < 2; i++)
            {
                var slot = HudUiPrimitives.CreateEmptyIconSlot(row, $"Talisman{i}");
                slot.gameObject.AddComponent<LayoutElement>().preferredWidth = 64f;
            }

            var divider = HudUiPrimitives.CreateRoundedPanel(row, "Divider", BurmaldaHudPalette.StoneBorder, BurmaldaHudPalette.StoneBorder, 0, 0);
            divider.gameObject.AddComponent<LayoutElement>().preferredWidth = 2f;

            for (var i = 0; i < 2; i++)
            {
                var slot = HudUiPrimitives.CreateEmptyIconSlot(row, $"Amulet{i}");
                slot.gameObject.AddComponent<LayoutElement>().preferredWidth = 64f;
            }
        }

        // issue: "в Relic.cs нет поля веса — плейсхолдер, число не выдумывать".
        private void BuildWeightPlaceholder(Transform parent, Vector2 topLeftOffset)
        {
            var text = HudUiPrimitives.CreateLabel(parent, "WeightPlaceholder", "Вес — / — реликвий", 18, BurmaldaHudPalette.TextTertiary, bold: false, TextAnchor.MiddleLeft);
            AnchorTopLeft((RectTransform)text.transform, topLeftOffset, new Vector2(300f, 32f));
        }

        // Состояние 3 — притушение уже в BuildNormalHud (CanvasGroup), здесь только рамка на плитке + фиксированная подсказка.
        private void BuildAimVisuals(Transform parent)
        {
            _aimTooltipRoot = HudUiPrimitives.CreateRoundedPanel(parent, "AimTooltip", BurmaldaHudPalette.SurfaceCard, BurmaldaHudPalette.Danger, 2, 12).gameObject;
            var tooltipRect = (RectTransform)_aimTooltipRoot.transform;
            tooltipRect.sizeDelta = new Vector2(280f, 56f);
            tooltipRect.pivot = new Vector2(0.5f, 0f); // подсказка растёт ВВЕРХ от точки над плиткой
            _aimTooltipRect = tooltipRect;

            // issue: текст СТРОГО фиксированный, никогда не тип ловушки/% — см. doc-комментарий класса.
            var label = HudUiPrimitives.CreateLabel(tooltipRect, "Label", "С этой плитой что-то не так…", 18, BurmaldaHudPalette.TextPrimary, bold: false, TextAnchor.MiddleCenter);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;

            _aimHighlight = new GameObject("AimHighlight");
            _aimHighlight.transform.SetParent(transform, worldPositionStays: false);
            var line = _aimHighlight.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.widthMultiplier = 0.05f;
            line.positionCount = 5;
            var half = 0.45f;
            line.SetPositions(new[]
            {
                new Vector3(-half, 0f, -half), new Vector3(half, 0f, -half), new Vector3(half, 0f, half),
                new Vector3(-half, 0f, half), new Vector3(-half, 0f, -half)
            });
            var material = new Material(Shader.Find("Sprites/Default")) { color = BurmaldaHudPalette.Danger };
            line.material = material;
            line.startColor = BurmaldaHudPalette.Danger;
            line.endColor = BurmaldaHudPalette.Danger;
            _aimHighlight.AddComponent<HudWorldPulseAnimation>(); // world-space вариант — HudPulseAnimation кастует к RectTransform, здесь обычный Transform

            _aimTooltipRoot.SetActive(false);
            _aimHighlight.SetActive(false);
        }

        // issue Шаг 4: "собери UI-каркас 1:1, НЕ подключай ни к чему, не пиши логику расчёта — оставь неактивным по умолчанию".
        private void BuildBossRoomShell(Transform parent)
        {
            _bossRoomRoot = HudUiPrimitives.CreateRect(parent, "BossRoomShell").gameObject;
            var root = (RectTransform)_bossRoomRoot.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var background = HudUiPrimitives.CreateRoundedGradientPanel(root, "Background", BurmaldaHudPalette.BossRoomBackgroundTop, BurmaldaHudPalette.BossRoomBackgroundBottom, 0);
            var backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            // Золотая светящаяся рамка экрана — вложенная рамка чуть меньше полного экрана.
            var frame = HudUiPrimitives.CreateRoundedPanel(root, "GoldFrame", new Color32(0, 0, 0, 0), BurmaldaHudPalette.GoldAccent, 6, 0);
            var frameRect = (RectTransform)frame.transform;
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = new Vector2(8f, 8f);
            frameRect.offsetMax = new Vector2(-8f, -8f);

            // Полоса "волна приближается" — статичный плейсхолдер-заполнение 60%.
            var waveBar = HudUiPrimitives.CreateRoundedPanel(root, "WaveBar", BurmaldaHudPalette.SurfaceCard, BurmaldaHudPalette.StoneBorder, 2, 8);
            AnchorTopLeft((RectTransform)waveBar.transform, new Vector2(40f, -TopMargin), new Vector2(ReferenceWidth - 80f, 24f));
            var waveFill = HudUiPrimitives.CreateRoundedPanel(waveBar.transform, "Fill", BurmaldaHudPalette.Danger, BurmaldaHudPalette.Danger, 0, 8);
            var waveFillRect = (RectTransform)waveFill.transform;
            waveFillRect.anchorMin = Vector2.zero;
            waveFillRect.anchorMax = new Vector2(0.6f, 1f); // статично — не подключено, см. doc-комментарий
            waveFillRect.offsetMin = Vector2.zero;
            waveFillRect.offsetMax = Vector2.zero;

            // Множитель — крупнейший элемент экрана (issue: 128px).
            var multiplierText = HudUiPrimitives.CreateLabel(root, "BossMultiplier", "×3.5", 128, BurmaldaHudPalette.TextPrimary, bold: true, TextAnchor.MiddleCenter);
            var multiplierRect = (RectTransform)multiplierText.transform;
            multiplierRect.anchorMin = new Vector2(0f, 0.35f);
            multiplierRect.anchorMax = new Vector2(1f, 0.65f);
            multiplierRect.offsetMin = Vector2.zero;
            multiplierRect.offsetMax = Vector2.zero;

            // "Отклик Шахты: Эхо ×N" — розовая плашка.
            var echoPanel = HudUiPrimitives.CreateRoundedPanel(root, "EchoBadge", BurmaldaHudPalette.LegendaryPink, BurmaldaHudPalette.LegendaryPink, 0, 999);
            AnchorTopLeft((RectTransform)echoPanel.transform, new Vector2(ReferenceWidth / 2f - 140f, -(TopMargin + 60f)), new Vector2(280f, 48f));
            var echoLabel = HudUiPrimitives.CreateLabel(echoPanel.transform, "Label", "ОТКЛИК ШАХТЫ: ЭХО ×2", 16, BurmaldaHudPalette.GoldButtonText, bold: true, TextAnchor.MiddleCenter);
            var echoLabelRect = (RectTransform)echoLabel.transform;
            echoLabelRect.anchorMin = Vector2.zero;
            echoLabelRect.anchorMax = Vector2.one;
            echoLabelRect.offsetMin = Vector2.zero;
            echoLabelRect.offsetMax = Vector2.zero;

            _bossRoomRoot.SetActive(false);
        }

        private static void AnchorTopLeft(RectTransform rect, Vector2 topLeftOffset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeftOffset;
            rect.sizeDelta = size;
        }

        private static void AnchorTopRight(RectTransform rect, Vector2 topRightOffset, Vector2 size)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = topRightOffset;
            rect.sizeDelta = size;
        }
    }
}
