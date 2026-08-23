using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Тумблеры для <see cref="RunHudToggles"/> (issue "Задача 2 —
    /// отладочный HUD"): "Run HUD" (числа/множитель/билд, дефолт ВКЛ) и
    /// "Tile debug" (координаты/% распада/тип ловушки примериваемой плиты,
    /// ранее выводившиеся всегда — теперь под тумблером, дефолт ВЫКЛ).
    ///
    /// Тот же принцип, что <see cref="CameraAnchorDebugPanel"/>: свёрнутая
    /// кнопка-кнопка в углу разворачивает панель, Canvas/Toggle вместо
    /// OnGUI, самобутстрапится через
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/>, ничего не
    /// трогает в .unity/.prefab. Позиция — ВЕРХНИЙ ЦЕНТР (не левый/правый
    /// верхние углы — там уже <c>CameraAnchorDebugPanel</c>/<c>RestartButton</c>),
    /// чтобы три независимых debug-панели не перекрывали друг друга.
    /// </summary>
    public sealed class RunHudTogglePanel : MonoBehaviour
    {
        private const float PanelWidth = 360f;
        private const float Margin = 24f;
        private const float RowHeight = 90f;
        private const float ButtonWidth = 160f;
        private const float ButtonHeight = 72f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(RunHudTogglePanel));
            host.AddComponent<RunHudTogglePanel>();
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
            var canvasHost = new GameObject(nameof(RunHudTogglePanel) + "_Canvas");
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
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            rect.anchoredPosition = new Vector2(0f, -Margin);

            var button = host.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                _expanded = !_expanded;
                _panelRoot.SetActive(_expanded);
            });

            var text = AddLabel(host.transform, "HUD", 20, TextAnchor.MiddleCenter);
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
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(PanelWidth, RowHeight * 2f + Margin * 2f);
            rect.anchoredPosition = new Vector2(0f, -(Margin + ButtonHeight + Margin));

            BuildToggleRow(_panelRoot.transform, 0, "Run HUD", RunHudToggles.ShowRunHud, v => RunHudToggles.ShowRunHud = v);
            BuildToggleRow(_panelRoot.transform, 1, "Tile debug", RunHudToggles.ShowTileDebugOverlay, v => RunHudToggles.ShowTileDebugOverlay = v);
        }

        private void BuildToggleRow(Transform parent, int rowIndex, string label, bool initialValue, System.Action<bool> onValueChanged)
        {
            var rowHost = new GameObject($"Row_{rowIndex}");
            rowHost.transform.SetParent(parent, worldPositionStays: false);
            var rowRect = rowHost.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.sizeDelta = new Vector2(-Margin * 2f, RowHeight);
            rowRect.anchoredPosition = new Vector2(Margin, -Margin - rowIndex * RowHeight);

            var toggleHost = new GameObject("Toggle");
            toggleHost.transform.SetParent(rowHost.transform, worldPositionStays: false);
            var toggleRect = toggleHost.AddComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0f, 0.5f);
            toggleRect.anchorMax = new Vector2(0f, 0.5f);
            toggleRect.pivot = new Vector2(0f, 0.5f);
            toggleRect.sizeDelta = new Vector2(44f, 44f);
            toggleRect.anchoredPosition = Vector2.zero;

            var toggleBg = toggleHost.AddComponent<Image>();
            toggleBg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            var checkHost = new GameObject("Checkmark");
            checkHost.transform.SetParent(toggleHost.transform, worldPositionStays: false);
            var checkImage = checkHost.AddComponent<Image>();
            checkImage.color = new Color(0.3f, 0.9f, 0.4f, 1f);
            var checkRect = checkHost.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.15f, 0.15f);
            checkRect.anchorMax = new Vector2(0.85f, 0.85f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;

            var toggle = toggleHost.AddComponent<Toggle>();
            toggle.targetGraphic = toggleBg;
            toggle.graphic = checkImage;
            toggle.isOn = initialValue;
            toggle.onValueChanged.AddListener(v => onValueChanged(v));

            var labelText = AddLabel(rowHost.transform, label, 22, TextAnchor.MiddleLeft);
            var labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(56f, 0f);
            labelRect.offsetMax = Vector2.zero;
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
