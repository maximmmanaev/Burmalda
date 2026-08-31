using Burmalda.Camp;
using Burmalda.Currencies;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Четыре стартовых значения новой экономики (PRD v9 раздел 5, задача по
    /// экономике "Мана как доход забега, Монеты только в Лагере") —
    /// <see cref="TrailManaIncomeSystem.BaseManaPerTile"/>,
    /// <see cref="CurrencyController.ManaPerSource"/>,
    /// <see cref="ReturnJourneySystem.ManaToCoinsRate"/>,
    /// <see cref="ReturnJourneySystem.KeysToCoinsRate"/> — все четыре
    /// помечены в объявлении "стартовое значение, уточняется на плейтесте" и
    /// намеренно НЕ <c>const</c>, а изменяемые статические поля именно ради
    /// этой панели.
    ///
    /// Тот же принцип, что <see cref="CameraAnchorDebugPanel"/>: Canvas/Slider
    /// вместо OnGUI, самобутстрап через
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/>, ничего не
    /// трогает в .unity/.prefab (docs/rules/forbidden-actions.md). Свёрнута
    /// по умолчанию (кнопка ECO в углу).
    ///
    /// Первые два значения (<see cref="TrailManaIncomeSystem.BaseManaPerTile"/>,
    /// <see cref="CurrencyController.ManaPerSource"/>) читаются вызывающей
    /// стороной только при пересборке run-систем (<c>CurrencyController.RebuildRunSystems</c>)
    /// — правка применяется со СЛЕДУЮЩЕГО забега, не посреди уже идущего.
    /// Курсы конвертации читаются прямо в момент возврата
    /// (<c>ReturnJourneySystem.OnPositionChanged</c>) — применяются
    /// мгновенно, даже посреди уже начатого возврата.
    ///
    /// Позиция — ВЕРХНИЙ ПРАВЫЙ угол, ПОД кнопкой <see cref="RestartButton"/>
    /// (тот же угол, 220×90 у самого края) — <c>CameraAnchorDebugPanel</c>
    /// (CAM) занимает левый верхний, <see cref="RunHudTogglePanel"/> (HUD) —
    /// верхний центр. Изначально ECO-кнопка была на том же смещении
    /// `(-Margin, -Margin)`, что и RESTART, и визуально перекрывалась с ней
    /// на устройстве (см. отчёт по задаче по экономике, скриншот проверки
    /// §7) — исправлено сдвигом вниз на высоту RESTART.
    /// </summary>
    public sealed class EconomyDebugPanel : MonoBehaviour
    {
        private const float PanelWidth = 460f;
        private const float Margin = 24f;
        private const float RowHeight = 110f;
        private const float ToggleButtonSize = 72f;
        // RestartButton — 220×90, тот же угол (1,1), офсет (-Margin,-Margin).
        private const float RestartButtonHeight = 90f;
        private const float TopOffset = Margin + RestartButtonHeight + Margin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(EconomyDebugPanel));
            host.AddComponent<EconomyDebugPanel>();
            DontDestroyOnLoad(host);
        }

        private GameObject _panelRoot;
        private bool _expanded;

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

        private void BuildUi()
        {
            var canvasHost = new GameObject(nameof(EconomyDebugPanel) + "_Canvas");
            canvasHost.transform.SetParent(transform, worldPositionStays: false);

            var canvas = canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasHost.AddComponent<CanvasScaler>();
            canvasHost.AddComponent<GraphicRaycaster>();

            BuildExpandButton(canvasHost.transform);
            BuildPanel(canvasHost.transform);
            _panelRoot.SetActive(_expanded);
        }

        private void BuildExpandButton(Transform parent)
        {
            var host = new GameObject("ToggleButton");
            host.transform.SetParent(parent, worldPositionStays: false);

            var image = host.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

            var rect = host.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(ToggleButtonSize, ToggleButtonSize);
            rect.anchoredPosition = new Vector2(-Margin, -TopOffset);

            var button = host.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                _expanded = !_expanded;
                _panelRoot.SetActive(_expanded);
            });

            var text = AddLabel(host.transform, "ECO", 20, TextAnchor.MiddleCenter);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private void BuildPanel(Transform parent)
        {
            _panelRoot = new GameObject("Panel");
            _panelRoot.transform.SetParent(parent, worldPositionStays: false);

            var image = _panelRoot.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            var rect = _panelRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(PanelWidth, RowHeight * 4f + Margin * 2f);
            rect.anchoredPosition = new Vector2(-Margin, -(TopOffset + ToggleButtonSize + Margin));

            BuildRow(_panelRoot.transform, 0,
                "Обычная плита (Мана)", 1f, 50f, TrailManaIncomeSystem.BaseManaPerTile,
                v => TrailManaIncomeSystem.BaseManaPerTile = Mathf.RoundToInt(v),
                v => Mathf.RoundToInt(v).ToString());

            BuildRow(_panelRoot.transform, 1,
                "Источник Маны", 10f, 500f, CurrencyController.ManaPerSource,
                v => CurrencyController.ManaPerSource = Mathf.RoundToInt(v),
                v => Mathf.RoundToInt(v).ToString());

            BuildRow(_panelRoot.transform, 2,
                "Курс Мана→Монеты", 5f, 50f, ReturnJourneySystem.ManaToCoinsRate,
                v => ReturnJourneySystem.ManaToCoinsRate = v,
                v => v.ToString("0") + ":1");

            BuildRow(_panelRoot.transform, 3,
                "Курс Ключи→Монеты", 0.2f, 5f, ReturnJourneySystem.KeysToCoinsRate,
                v => ReturnJourneySystem.KeysToCoinsRate = v,
                v => v.ToString("0.0") + ":1");
        }

        /// <summary>Строка "подпись + слайдер + текущее значение" — та же разметка, что CameraAnchorDebugPanel.BuildRow.</summary>
        private void BuildRow(Transform parent, int rowIndex, string label, float min, float max, float initialValue,
            System.Action<float> onValueChanged, System.Func<float, string> format)
        {
            var rowHost = new GameObject($"Row_{rowIndex}");
            rowHost.transform.SetParent(parent, worldPositionStays: false);
            var rowRect = rowHost.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.sizeDelta = new Vector2(-Margin * 2f, RowHeight);
            rowRect.anchoredPosition = new Vector2(Margin, -Margin - rowIndex * RowHeight);

            var labelText = AddLabel(rowHost.transform, label, 20, TextAnchor.UpperLeft);
            var labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(0.65f, 1f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var valueText = AddLabel(rowHost.transform, format(initialValue), 20, TextAnchor.UpperRight);
            var valueRect = valueText.GetComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(0.65f, 0.5f);
            valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;

            var sliderHost = new GameObject("Slider");
            sliderHost.transform.SetParent(rowHost.transform, worldPositionStays: false);
            var sliderRect = sliderHost.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0f);
            sliderRect.anchorMax = new Vector2(1f, 0.45f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;

            var bgHost = new GameObject("Background");
            bgHost.transform.SetParent(sliderHost.transform, worldPositionStays: false);
            var bgImage = bgHost.AddComponent<Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            var bgRect = bgHost.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.25f);
            bgRect.anchorMax = new Vector2(1f, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var fillAreaHost = new GameObject("Fill Area");
            fillAreaHost.transform.SetParent(sliderHost.transform, worldPositionStays: false);
            var fillAreaRect = fillAreaHost.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            var fillHost = new GameObject("Fill");
            fillHost.transform.SetParent(fillAreaHost.transform, worldPositionStays: false);
            var fillImage = fillHost.AddComponent<Image>();
            fillImage.color = new Color(0.3f, 0.7f, 1f, 1f);
            var fillRect = fillHost.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var handleAreaHost = new GameObject("Handle Slide Area");
            handleAreaHost.transform.SetParent(sliderHost.transform, worldPositionStays: false);
            var handleAreaRect = handleAreaHost.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = Vector2.zero;
            handleAreaRect.offsetMax = Vector2.zero;

            var handleHost = new GameObject("Handle");
            handleHost.transform.SetParent(handleAreaHost.transform, worldPositionStays: false);
            var handleImage = handleHost.AddComponent<Image>();
            handleImage.color = Color.white;
            var handleRect = handleHost.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(24f, 0f);

            var slider = sliderHost.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = initialValue;

            slider.onValueChanged.AddListener(v =>
            {
                onValueChanged(v);
                valueText.text = format(v);
            });
        }

        private Text AddLabel(Transform parent, string content, int fontSize, TextAnchor alignment)
        {
            var host = new GameObject("Label");
            host.transform.SetParent(parent, worldPositionStays: false);
            host.AddComponent<RectTransform>();

            var text = host.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // встроенный шрифт — без арт-ассетов
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }
    }
}
