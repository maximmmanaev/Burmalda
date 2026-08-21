using Burmalda.Movement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Кнопка ручного рестарта забега поверх экрана (не финальный UI — по
    /// аналогии с <see cref="TunnelDebugVisual"/>, debug-инструмент для
    /// ручного тестирования, не система из PRD). Через
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/> создаёт себя сама
    /// при старте игры — без ручного добавления компонента на сцену
    /// (docs/rules/forbidden-actions.md: не трогаем .unity/.prefab).
    ///
    /// Рисуется через <see cref="Canvas"/>/<see cref="Button"/> (2026-08-18,
    /// прямой запрос владельца продукта — "нормальный UI на Canvas вместо
    /// OnGUI"), не через <c>OnGUI</c> (immediate mode), как раньше — тот же
    /// принцип "ни одного .prefab/.unity", просто другой Unity API: весь
    /// Canvas/EventSystem собирается кодом при старте, ничего не сохраняется
    /// на диск. <see cref="InputSystemUIInputModule"/> — проект работает на
    /// New Input System эксклюзивно (<c>ProjectSettings.activeInputHandler=1</c>),
    /// легаси <c>StandaloneInputModule</c> не работал бы.
    ///
    /// Вызывает уже существующий <see cref="GridTraceInputController.Restart"/>,
    /// который пересобирает мир/трейл с нуля; все остальные системы забега
    /// уже подписаны на его <see cref="GridTraceInputController.RunStarted"/>
    /// и пересоберут себя сами (см. паттерн, общий для всех Controller'ов
    /// проекта).
    /// </summary>
    public sealed class RestartButton : MonoBehaviour
    {
        private const float ButtonWidth = 220f;
        private const float ButtonHeight = 90f;
        private const float Margin = 24f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(RestartButton));
            host.AddComponent<RestartButton>();
            DontDestroyOnLoad(host);
        }

        private GridTraceInputController _input;

        private void Start()
        {
            EnsureEventSystemExists();
            BuildCanvasButton();
        }

        private void Update()
        {
            // Ленивый поиск — как и другие Controller'ы в проекте, находит
            // GridTraceInputController независимо от порядка Awake между
            // разными компонентами сцены.
            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
        }

        private static void EnsureEventSystemExists()
        {
            // UI Button не реагирует на тапы без EventSystem — сцена его не
            // содержит (в проекте вообще нет UI, см. doc-комментарий класса).
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var host = new GameObject(nameof(EventSystem));
            DontDestroyOnLoad(host);
            host.AddComponent<EventSystem>();
            host.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildCanvasButton()
        {
            var canvasHost = new GameObject(nameof(RestartButton) + "_Canvas");
            canvasHost.transform.SetParent(transform, worldPositionStays: false);

            var canvas = canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasHost.AddComponent<CanvasScaler>();
            canvasHost.AddComponent<GraphicRaycaster>();

            var buttonHost = new GameObject("RestartButtonWidget");
            buttonHost.transform.SetParent(canvasHost.transform, worldPositionStays: false);

            var image = buttonHost.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

            var rect = buttonHost.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            rect.anchoredPosition = new Vector2(-Margin, -Margin);

            var button = buttonHost.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(HandleRestartClicked);

            var textHost = new GameObject("Label");
            textHost.transform.SetParent(buttonHost.transform, worldPositionStays: false);
            var textRect = textHost.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textHost.AddComponent<Text>();
            text.text = "RESTART";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // встроенный шрифт — без арт-ассетов
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
        }

        private void HandleRestartClicked() => _input?.Restart();
    }
}
