using Burmalda.Camp;
using Burmalda.Core;
using Burmalda.Movement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Выбор на Алтаре (PRD v10 §13.1, §15.2): «Дальше» / «Вернуться в Лагерь».
    /// Две кнопки внизу экрана, видны только пока игрок стоит на Алтаре и ещё не
    /// эвакуировался. «Вернуться» — мгновенная эвакуация
    /// (<c>Camp.EvacuationSystem.Evacuate</c>); «Дальше» скрывает выбор, пока
    /// игрок не сойдёт с Алтаря. Временный UI (примитивы в рантайме, как
    /// <see cref="RestartButton"/>), финальный арт — позже.
    /// Ничего не трогает в .unity/.prefab (docs/rules/forbidden-actions.md).
    /// </summary>
    public sealed class ReturnToCampDebugButton : MonoBehaviour
    {
        private const float ButtonWidth = 300f;
        private const float ButtonHeight = 110f;
        private const float Gap = 24f;
        private const float BottomOffset = 120f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(ReturnToCampDebugButton));
            host.AddComponent<ReturnToCampDebugButton>();
            DontDestroyOnLoad(host);
        }

        private CampController _camp;
        private GridTraceInputController _input;
        private GameObject _panel;
        private GridCoordinate? _dismissedAt;

        private void Start()
        {
            EnsureEventSystemExists();
            BuildPanel();
        }

        private void Update()
        {
            // Ленивый поиск — тот же принцип, что у остальных debug-driver'ов проекта.
            if (_camp == null) _camp = FindFirstObjectByType<CampController>();
            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
            if (_panel == null) return;

            var evacuation = _camp != null ? _camp.Evacuation : null;
            var position = _input != null && _input.Trail != null ? (GridCoordinate?)_input.Trail.CurrentPosition : null;
            if (_dismissedAt.HasValue && position != _dismissedAt) _dismissedAt = null; // сошёл с Алтаря
            _panel.SetActive(evacuation != null && evacuation.CanEvacuate && !_dismissedAt.HasValue);
        }

        private static void EnsureEventSystemExists()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var host = new GameObject(nameof(EventSystem));
            DontDestroyOnLoad(host);
            host.AddComponent<EventSystem>();
            host.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildPanel()
        {
            var canvasHost = new GameObject(nameof(ReturnToCampDebugButton) + "_Canvas");
            canvasHost.transform.SetParent(transform, worldPositionStays: false);

            var canvas = canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasHost.AddComponent<CanvasScaler>();
            canvasHost.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("AltarChoicePanel", typeof(RectTransform));
            _panel.transform.SetParent(canvasHost.transform, worldPositionStays: false);
            var panelRect = (RectTransform)_panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.sizeDelta = new Vector2(ButtonWidth * 2f + Gap, ButtonHeight);
            panelRect.anchoredPosition = new Vector2(0f, BottomOffset);

            BuildButton("Дальше", -(ButtonWidth + Gap) * 0.5f, HandleContinue);
            BuildButton("Домой", (ButtonWidth + Gap) * 0.5f, HandleGoHome);
            _panel.SetActive(false);
        }

        private void BuildButton(string label, float x, UnityEngine.Events.UnityAction onClick)
        {
            var buttonHost = new GameObject(label + "Button");
            buttonHost.transform.SetParent(_panel.transform, worldPositionStays: false);

            var image = buttonHost.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

            var rect = buttonHost.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            rect.anchoredPosition = new Vector2(x, 0f);

            var button = buttonHost.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var textHost = new GameObject("Label");
            textHost.transform.SetParent(buttonHost.transform, worldPositionStays: false);
            var textRect = textHost.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textHost.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // встроенный шрифт — без арт-ассетов
            text.fontSize = 36;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
        }

        private void HandleContinue()
        {
            if (_input != null && _input.Trail != null) _dismissedAt = _input.Trail.CurrentPosition;
        }

        private void HandleGoHome() => _camp?.Evacuation?.Evacuate();
    }
}
