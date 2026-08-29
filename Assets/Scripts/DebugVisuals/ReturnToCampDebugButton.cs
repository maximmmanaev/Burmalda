using Burmalda.Camp;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Кнопка ручного запуска возврата в Лагерь (не финальный UI — по
    /// аналогии с <see cref="RestartButton"/>, debug-инструмент для ручного
    /// тестирования, не система из PRD).
    ///
    /// <b>Обнаруженный при проверке задачи по экономике пробел</b>: PRD
    /// раздел 10 описывает "Кнопку «Вернуться в лагерь»", и
    /// <c>Camp.ReturnJourneySystem.BeginReturn()</c> полностью реализован и
    /// покрыт тестами — но НИ ОДИН вызывающий код в проекте его не вызывает
    /// (сама кнопка — предмет будущего UI-спринта, не этой задачи). Без неё
    /// критерий приёмки §7 задачи по экономике ("на возврате в Лагерь
    /// показан блок конвертации") физически не проверить на устройстве —
    /// та же ситуация, что уже была с <c>CurrencyControllerBootstrap</c>
    /// (Задача 2): не создаёт новую игровую систему, только даёт ручной
    /// доступ к уже существующей.
    ///
    /// Тот же паттерн, что <see cref="RestartButton"/>: Canvas/Button,
    /// самобутстрап, ничего не трогает в .unity/.prefab
    /// (docs/rules/forbidden-actions.md). Позиция — под кнопками
    /// RESTART/ECO в верхнем правом углу (тот же принцип разведения углов,
    /// что у <see cref="EconomyDebugPanel"/>).
    /// </summary>
    public sealed class ReturnToCampDebugButton : MonoBehaviour
    {
        private const float ButtonWidth = 220f;
        private const float ButtonHeight = 90f;
        private const float Margin = 24f;
        // RestartButton (90) + ECO-кнопка EconomyDebugPanel (72), тот же верхний правый угол.
        private const float TopOffset = Margin + 90f + Margin + 72f + Margin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(ReturnToCampDebugButton));
            host.AddComponent<ReturnToCampDebugButton>();
            DontDestroyOnLoad(host);
        }

        private CampController _camp;

        private void Start()
        {
            EnsureEventSystemExists();
            BuildCanvasButton();
        }

        private void Update()
        {
            // Ленивый поиск — тот же принцип, что у остальных debug-driver'ов проекта.
            if (_camp == null) _camp = FindFirstObjectByType<CampController>();
        }

        private static void EnsureEventSystemExists()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var host = new GameObject(nameof(EventSystem));
            DontDestroyOnLoad(host);
            host.AddComponent<EventSystem>();
            host.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildCanvasButton()
        {
            var canvasHost = new GameObject(nameof(ReturnToCampDebugButton) + "_Canvas");
            canvasHost.transform.SetParent(transform, worldPositionStays: false);

            var canvas = canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasHost.AddComponent<CanvasScaler>();
            canvasHost.AddComponent<GraphicRaycaster>();

            var buttonHost = new GameObject("ReturnToCampButtonWidget");
            buttonHost.transform.SetParent(canvasHost.transform, worldPositionStays: false);

            var image = buttonHost.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

            var rect = buttonHost.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            rect.anchoredPosition = new Vector2(-Margin, -TopOffset);

            var button = buttonHost.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(HandleClicked);

            var textHost = new GameObject("Label");
            textHost.transform.SetParent(buttonHost.transform, worldPositionStays: false);
            var textRect = textHost.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textHost.AddComponent<Text>();
            text.text = "TO CAMP";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // встроенный шрифт — без арт-ассетов
            text.fontSize = 24;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
        }

        private void HandleClicked() => _camp?.Journey?.BeginReturn();
    }
}
