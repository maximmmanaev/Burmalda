using Burmalda.Movement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Три настраиваемых параметра непрерывного следования камеры с
    /// компенсацией скорости + жёсткий/мягкий кламп (issue #155/#158) — доля
    /// якоря, допуск клампа сверху и точка начала нарастания мягкой границы
    /// (<see cref="TunnelCameraController.AnchorViewportY"/>,
    /// <see cref="TunnelCameraController.ToleranceViewportFraction"/>,
    /// <see cref="TunnelCameraController.SoftBoundaryStartFraction"/>).
    ///
    /// <b>Задача 1 (владелец продукта принял на записи с устройства):</b>
    /// само следование теперь ВСЕГДА включено — тумблер вкл/выкл (раньше
    /// единственный оставшийся после #153, закрытый PR #154, тумблеры
    /// 1/step-tween и 3/cursor-offset запрещены) убран вместе со старым
    /// путём, который он выбирал (см. doc-комментарий
    /// <c>Movement.TunnelCameraFollow</c>). Панель — только три ползунка,
    /// без чекбокса. Тот же принцип: Canvas/Slider вместо OnGUI,
    /// самобутстрапится через
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/>, ничего не
    /// трогает в .unity/.prefab (docs/rules/forbidden-actions.md). Нужна для
    /// тонкой подстройки на устройстве — дефолты уже дают принятое на записи
    /// поведение без единого движения по панели.
    ///
    /// Свёрнута по умолчанию (кнопка CAM в углу).
    /// </summary>
    public sealed class CameraAnchorDebugPanel : MonoBehaviour
    {
        private const float PanelWidth = 460f;
        private const float Margin = 24f;
        private const float RowHeight = 110f;
        private const float ToggleButtonSize = 72f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(CameraAnchorDebugPanel));
            host.AddComponent<CameraAnchorDebugPanel>();
            DontDestroyOnLoad(host);
        }

        private TunnelCameraController _camera;
        private GameObject _panelRoot;
        private bool _expanded;

        private void Start()
        {
            EnsureEventSystemExists();
            BuildUi();
        }

        private void Update()
        {
            // Ленивый поиск — как и RestartButton, не зависит от порядка
            // Awake между компонентами сцены.
            if (_camera == null) _camera = FindFirstObjectByType<TunnelCameraController>();
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
            var canvasHost = new GameObject(nameof(CameraAnchorDebugPanel) + "_Canvas");
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
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(ToggleButtonSize, ToggleButtonSize);
            rect.anchoredPosition = new Vector2(Margin, -Margin);

            var button = host.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                _expanded = !_expanded;
                _panelRoot.SetActive(_expanded);
            });

            var text = AddLabel(host.transform, "CAM", 20, TextAnchor.MiddleCenter);
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
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(PanelWidth, RowHeight * 3f + Margin * 2f);
            rect.anchoredPosition = new Vector2(Margin, -(Margin + ToggleButtonSize + Margin));

            BuildRow(_panelRoot.transform, 0,
                "Anchor (%)", 10f, 60f, 32f,
                v => { if (_camera != null) _camera.AnchorViewportY = v / 100f; },
                v => v.ToString("0") + "%");

            BuildRow(_panelRoot.transform, 1,
                "Tolerance (%)", 0f, 30f, 12f,
                v => { if (_camera != null) _camera.ToleranceViewportFraction = v / 100f; },
                v => v.ToString("0") + "%");

            BuildRow(_panelRoot.transform, 2,
                "Soft boundary start (%)", 20f, 95f, TunnelCameraSoftBoundary.DefaultStartFraction * 100f,
                v => { if (_camera != null) _camera.SoftBoundaryStartFraction = v / 100f; },
                v => v.ToString("0") + "%");
        }

        /// <summary>Строка "подпись + слайдер + текущее значение" — общий layout для всех строк.</summary>
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
            labelRect.offsetMin = Vector2.zero; // без тумблера слева — не нужен отступ 48px под него
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
