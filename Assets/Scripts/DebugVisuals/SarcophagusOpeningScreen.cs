using Burmalda.Camp;
using Burmalda.Movement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Экран Лагеря после эвакуации (PRD v10 §22.3): Саркофаги открываются по одному —
    /// закрытый → открытый (Монеты) → следующий … → «Новая экспедиция». Без рекламы.
    /// Редкость, части и Реликвии добавятся сюда позже (см. <see cref="Sarcophagus.Open"/>).
    /// Показывается по <see cref="EvacuationSystem.Returned"/> и при старте, если с прошлой
    /// сессии остались неоткрытые. Временный UI на примитивах (как <see cref="RestartButton"/>).
    /// Ничего не трогает в .unity/.prefab.
    /// </summary>
    public sealed class SarcophagusOpeningScreen : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(SarcophagusOpeningScreen));
            host.AddComponent<SarcophagusOpeningScreen>();
            DontDestroyOnLoad(host);
        }

        private CampController _campController;
        private GridTraceInputController _input;
        private EvacuationSystem _wired;
        private SarcophagusOpeningFlow _flow;
        private GameObject _panel;
        private Text _title, _body, _buttonLabel;
        private int _returnedCoins;
        private bool _startupChecked;

        private void Start()
        {
            EnsureEventSystemExists();
            BuildPanel();
        }

        private void Update()
        {
            if (_campController == null) _campController = FindFirstObjectByType<CampController>();
            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
            if (_campController == null || _campController.Camp == null) return;

            if (_flow == null) _flow = new SarcophagusOpeningFlow(_campController.Camp);
            SyncEvacuation(_campController.Evacuation);

            // Неоткрытые с прошлой сессии: показываем один раз при старте.
            if (!_startupChecked)
            {
                _startupChecked = true;
                if (_campController.Camp.UnopenedSarcophagi.Count > 0) Show(0);
            }
        }

        private void OnDisable() => SyncEvacuation(null);

        private void SyncEvacuation(EvacuationSystem current)
        {
            if (ReferenceEquals(current, _wired)) return;
            if (_wired != null) _wired.Returned -= OnReturned;
            _wired = current;
            if (_wired != null) _wired.Returned += OnReturned;
        }

        private void OnReturned(ReturnConversionResult result) => Show(result.TotalCoins);

        private void Show(int returnedCoins)
        {
            if (_flow == null || _panel == null) return;
            _returnedCoins = returnedCoins;
            _flow.Begin();
            Refresh();
            _panel.SetActive(true);
        }

        private void Refresh()
        {
            switch (_flow.Phase)
            {
                case SarcophagusOpeningPhase.Closed:
                    _title.text = _returnedCoins > 0 ? $"Дома! +{_returnedCoins} Монет" : "Дома!";
                    _body.text = $"Саркофагов: {_flow.Remaining}\nЧто внутри — узнаешь сейчас";
                    _buttonLabel.text = "Открыть";
                    break;
                case SarcophagusOpeningPhase.Opened:
                    _title.text = "Саркофаг открыт";
                    _body.text = $"+{_flow.LastContents.Coins} Монет";
                    _buttonLabel.text = _flow.Remaining > 0 ? "Дальше" : "Готово";
                    break;
                default:
                    _title.text = _returnedCoins > 0 ? $"Дома! +{_returnedCoins} Монет" : "Дома!";
                    _body.text = "";
                    _buttonLabel.text = "Новая экспедиция";
                    break;
            }
        }

        private void HandleButton()
        {
            if (_flow.Phase == SarcophagusOpeningPhase.Finished)
            {
                _flow.Reset();
                _panel.SetActive(false);
                if (_input != null) _input.Restart();
                return;
            }

            _flow.Advance();
            Refresh();
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
            var canvasHost = new GameObject(nameof(SarcophagusOpeningScreen) + "_Canvas");
            canvasHost.transform.SetParent(transform, worldPositionStays: false);
            var canvas = canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            canvasHost.AddComponent<CanvasScaler>();
            canvasHost.AddComponent<GraphicRaycaster>();

            _panel = NewRect("OpeningPanel", canvasHost.transform, new Vector2(900f, 700f), Vector2.zero).gameObject;
            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.07f, 0.06f, 0.96f);

            _title = NewText("Title", 52, new Vector2(0f, 250f), 860f, 90f);
            _body = NewText("Body", 44, new Vector2(0f, 60f), 860f, 260f);
            _body.color = new Color(1f, 0.84f, 0f); // золото Саркофага, как плита в забеге

            var buttonRect = NewRect("Button", _panel.transform, new Vector2(520f, 120f), new Vector2(0f, -240f));
            var image = buttonRect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.2f, 0.18f, 1f);
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(HandleButton);
            var labelRect = NewRect("Label", buttonRect, Vector2.zero, Vector2.zero);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            _buttonLabel = StyleText(labelRect.gameObject.AddComponent<Text>(), 42);

            _panel.SetActive(false);
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return rect;
        }

        private Text NewText(string name, int size, Vector2 pos, float width, float height) =>
            StyleText(NewRect(name, _panel.transform, new Vector2(width, height), pos).gameObject.AddComponent<Text>(), size);

        private static Text StyleText(Text text, int size)
        {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }
    }
}
