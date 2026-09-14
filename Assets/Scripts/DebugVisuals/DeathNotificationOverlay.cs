using Burmalda.Movement;
using Burmalda.RunLifecycle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// [TEMP] Issue #266: «непонятно, когда игрок умер» — сейчас
    /// <see cref="RunController.OnDied"/> только блокирует ввод
    /// (<c>GridTraceInputController.MarkDead</c>) и запоминает причину, без
    /// единого видимого сигнала — экран просто перестаёт реагировать,
    /// неотличимо от зависания на ручном тесте. Задача ссылалась на issue
    /// #11 как на «уже готовый полноценный экран Game Over с кнопкой
    /// Restart» — ПРОВЕРЕНО ЯВНО: #11 это другая, закрытая задача (система
    /// множителя добычи), никак не связанная со смертью; полноценного
    /// экрана Game Over в проекте нет нигде (см. doc-комментарий
    /// <see cref="RunController"/>, прямым текстом: "UI экрана смерти/кнопки
    /// рестарта в проекте ещё нет").
    ///
    /// <b>ВРЕМЕННОЕ РЕШЕНИЕ.</b> Минимум, ничего лишнего: полноэкранный
    /// оверлей с текстом причины смерти на <see cref="RunState.Died"/> — без
    /// интеграции с экономикой/статистикой забега, это в скоупе полноценного
    /// экрана Game Over, не этой заглушки. Удаляется целиком, когда тот
    /// экран будет готов — каким бы issue он ни описывался.
    ///
    /// <b>Кнопка "ИГРАТЬ СНОВА" (владелец, живой тест на устройстве,
    /// 2026-09-14) — ревизия исходного решения задачи.</b> Первая версия
    /// сознательно была БЕЗ кнопки рестарта — предполагалось, что отдельная
    /// всегда видимая <see cref="RestartButton"/> её заменяет. На практике
    /// полноэкранный <see cref="Image"/>-фон оверлея (нужен для
    /// "невозможное не заметить") перехватывает raycast поверх ВСЕГО, что
    /// под ним, включая <see cref="RestartButton"/> на отдельном Canvas ниже
    /// по sortingOrder — владелец подтвердил на устройстве: "уведомление не
    /// даёт нажать рестарт". Кнопка теперь на самом оверлее.
    ///
    /// Тот же паттерн самобутстрапа/переподписки на смену <see cref="RunState"/>
    /// при рестарте, что <see cref="PickupFeedback"/>/<see cref="StepClickController"/>
    /// — здесь источник не <c>GridTraceTrail</c>, а <see cref="RunController.RunState"/>
    /// (единственное место, где реально поднимается <see cref="RunState.Died"/>).
    /// </summary>
    public sealed class DeathNotificationOverlay : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(DeathNotificationOverlay));
            host.AddComponent<DeathNotificationOverlay>();
            DontDestroyOnLoad(host);
        }

        private GridTraceInputController _input;
        private RunController _runController;
        private RunState _wiredRunState;
        private GameObject _overlayRoot;
        private Text _reasonText;
        private string _lastMessage;

        /// <summary>Тестируемый флаг (issue #266, критерий приёмки — "проверяемо через состояние/флаг") — истинно, пока уведомление показано.</summary>
        public bool IsShowingDeathNotification { get; private set; }

        private void Start()
        {
            EnsureEventSystemExists();
            BuildOverlay();
        }

        private void Update()
        {
            // Ленивый поиск/пересборка — тот же принцип, что у остальных
            // debug/game driver'ов проекта. RunController.RunState
            // пересобирается на каждый забег (см. её doc-комментарий) —
            // сверяем по ссылке, не только на null, чтобы переподписаться
            // на новый экземпляр после рестарта, а не слушать старый.
            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
            if (_runController == null) _runController = FindFirstObjectByType<RunController>();
            if (_runController == null || _runController.RunState == null) return;

            if (_runController.RunState != _wiredRunState)
            {
                if (_wiredRunState != null) _wiredRunState.Died -= HandleDied;
                _wiredRunState = _runController.RunState;
                _wiredRunState.Died += HandleDied;
                Hide(); // новый забег — скрыть уведомление прошлого, если оно ещё висело
            }
        }

        private void OnDisable()
        {
            if (_wiredRunState != null) _wiredRunState.Died -= HandleDied;
            _wiredRunState = null;
        }

        private void HandleDied(string reason)
        {
            _lastMessage = BuildMessage(reason);
            IsShowingDeathNotification = true;

            if (_reasonText != null) _reasonText.text = _lastMessage;
            if (_overlayRoot != null) _overlayRoot.SetActive(true);
        }

        private void Hide()
        {
            IsShowingDeathNotification = false;
            if (_overlayRoot != null) _overlayRoot.SetActive(false);
        }

        private static string BuildMessage(string reason) =>
            string.IsNullOrEmpty(reason) ? "ВЫ ПОГИБЛИ" : $"ВЫ ПОГИБЛИ\n{reason}";

        private static void EnsureEventSystemExists()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var host = new GameObject(nameof(EventSystem));
            DontDestroyOnLoad(host);
            host.AddComponent<EventSystem>();
            host.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildOverlay()
        {
            var canvasHost = new GameObject(nameof(DeathNotificationOverlay) + "_Canvas");
            canvasHost.transform.SetParent(transform, worldPositionStays: false);

            var canvas = canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Поверх любого другого UI проекта (дебаг-панели/RestartButton) —
            // "невозможное не заметить" (критерий приёмки issue #266)
            // обязано читаться даже если открыта дебаг-панель.
            canvas.sortingOrder = 1000;
            canvasHost.AddComponent<CanvasScaler>();
            canvasHost.AddComponent<GraphicRaycaster>();

            _overlayRoot = new GameObject("Overlay");
            _overlayRoot.transform.SetParent(canvasHost.transform, worldPositionStays: false);

            var background = _overlayRoot.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.85f);
            var backgroundRect = _overlayRoot.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            var textHost = new GameObject("ReasonText");
            textHost.transform.SetParent(_overlayRoot.transform, worldPositionStays: false);
            var textRect = textHost.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _reasonText = textHost.AddComponent<Text>();
            _reasonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // встроенный шрифт — без арт-ассетов, тот же приём, что везде в проекте
            _reasonText.fontSize = 64;
            _reasonText.fontStyle = FontStyle.Bold;
            _reasonText.alignment = TextAnchor.MiddleCenter;
            _reasonText.color = Color.white;
            _reasonText.text = "ВЫ ПОГИБЛИ";

            BuildPlayAgainButton(_overlayRoot.transform);

            // Скрыт по умолчанию — до первой смерти показывать нечего.
            _overlayRoot.SetActive(false);
        }

        private void BuildPlayAgainButton(Transform parent)
        {
            var buttonHost = new GameObject("PlayAgainButton");
            buttonHost.transform.SetParent(parent, worldPositionStays: false);

            var image = buttonHost.AddComponent<Image>();
            image.color = new Color(0.85f, 0.15f, 0.1f, 0.95f); // тревожный красный — отличим от нейтральной RestartButton

            var rect = buttonHost.GetComponent<RectTransform>();
            // Фиксированная точка ниже центра экрана (не привязана к
            // высоте _reasonText — та растёт с длиной причины смерти) —
            // 25% высоты экрана от низа, с запасом от текста при любой
            // разумной длине причины.
            rect.anchorMin = new Vector2(0.5f, 0.25f);
            rect.anchorMax = new Vector2(0.5f, 0.25f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(420f, 130f);
            rect.anchoredPosition = Vector2.zero;

            var button = buttonHost.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(HandlePlayAgainClicked);

            var textHost = new GameObject("Label");
            textHost.transform.SetParent(buttonHost.transform, worldPositionStays: false);
            var textRect = textHost.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textHost.AddComponent<Text>();
            text.text = "ИГРАТЬ СНОВА";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 38;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
        }

        private void HandlePlayAgainClicked() => _input?.Restart();
    }
}
