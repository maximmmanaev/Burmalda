using Burmalda.Currencies;
using Burmalda.FateRoll;
using Burmalda.RunLifecycle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Экран Броска Судьбы (PRD v10 §16, §31): два кубика, три сектора шансов с
    /// процентами (смерть / Алтарь / на месте), что на кону, кнопки «Бросить» и
    /// «Отказаться». Показывается по <see cref="RunState.FateRollOffered"/>; после
    /// броска коротко держит выпавшие кубики и исход. Временный UI на примитивах
    /// (как <see cref="RestartButton"/>), финальный арт — позже. Оплата — заглушка
    /// (<see cref="FateRollRules.FateRollPaymentStub"/>). Ничего не трогает в .unity/.prefab.
    /// </summary>
    public sealed class FateRollOverlay : MonoBehaviour
    {
        private const float ResultHoldSeconds = 1.6f;
        private static readonly Color DeathBarColor = new Color(0.85f, 0.15f, 0.15f);
        private static readonly Color AltarBarColor = new Color(0.95f, 0.65f, 0.1f);
        private static readonly Color InPlaceBarColor = new Color(0.1f, 0.8f, 0.55f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(FateRollOverlay));
            host.AddComponent<FateRollOverlay>();
            DontDestroyOnLoad(host);
        }

        private RunController _runController;
        private CurrencyController _currency;
        private RunState _wired;
        private GameObject _panel;
        private Text _title, _stake, _dice, _result, _deathLabel, _altarLabel, _inPlaceLabel;
        private RectTransform _deathBar, _altarBar, _inPlaceBar;
        private Button _rollButton, _declineButton;
        private float _hideAt = -1f;

        private void Start()
        {
            EnsureEventSystemExists();
            BuildPanel();
        }

        private void Update()
        {
            if (_runController == null) _runController = FindFirstObjectByType<RunController>();
            if (_currency == null) _currency = FindFirstObjectByType<CurrencyController>();
            SyncRunState(_runController != null ? _runController.RunState : null);

            if (_hideAt > 0f && Time.unscaledTime >= _hideAt)
            {
                _hideAt = -1f;
                if (_panel != null) _panel.SetActive(false);
            }
        }

        private void OnDisable() => SyncRunState(null);

        private void SyncRunState(RunState current)
        {
            if (ReferenceEquals(current, _wired)) return;
            if (_wired != null)
            {
                _wired.FateRollOffered -= HandleOffered;
                _wired.FateRolled -= HandleRolled;
                _wired.Died -= HandleDied;
            }

            _wired = current;
            if (_wired == null) return;
            _wired.FateRollOffered += HandleOffered;
            _wired.FateRolled += HandleRolled;
            _wired.Died += HandleDied;
        }

        private void HandleOffered(int rollIndex)
        {
            if (_panel == null) return;
            var chances = FateRollRules.Chances(rollIndex, _wired.FateModifier);
            _title.text = FateRollScreenText.Title(rollIndex);
            _deathLabel.text = FateRollScreenText.Death(chances);
            _altarLabel.text = FateRollScreenText.Altar(chances);
            _inPlaceLabel.text = FateRollScreenText.InPlace(chances);
            SetBar(_deathBar, 0f, chances.DeathPercent);
            SetBar(_altarBar, chances.DeathPercent, chances.AltarPercent);
            SetBar(_inPlaceBar, chances.DeathPercent + chances.AltarPercent, chances.InPlacePercent);

            var mana = _currency != null && _currency.RunManaCrystals != null ? _currency.RunManaCrystals.Total : 0;
            var keys = _currency != null && _currency.RunKeys != null ? _currency.RunKeys.Total : 0;
            _stake.text = FateRollScreenText.Stake(mana, keys, 0);
            _dice.text = "?   ?";
            _result.text = "";
            _rollButton.interactable = _declineButton.interactable = true;
            _hideAt = -1f;
            _panel.SetActive(true);
        }

        private void HandleRolled(FateRollResult result)
        {
            if (_panel == null) return;
            _dice.text = $"{result.Die1}   {result.Die2}";
            _result.text = FateRollOutcomeText.BuildMessage(result);
            _result.color = FateRollOutcomeText.ResolveColor(result.Outcome);
            _rollButton.interactable = _declineButton.interactable = false;
            _hideAt = Time.unscaledTime + ResultHoldSeconds;
        }

        private void HandleDied(string reason)
        {
            // Отказ от броска: экран скрывается сразу; после броска со смертью даём прочитать кубики.
            if (_panel != null && _hideAt < 0f) _panel.SetActive(false);
        }

        private void HandleRollClicked() => _wired?.RollFate();

        private void HandleDeclineClicked() => _wired?.DeclineFate();

        private static void SetBar(RectTransform bar, double fromPercent, double widthPercent)
        {
            bar.anchorMin = new Vector2((float)(fromPercent / 100.0), 0f);
            bar.anchorMax = new Vector2((float)((fromPercent + widthPercent) / 100.0), 1f);
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
            var canvasHost = new GameObject(nameof(FateRollOverlay) + "_Canvas");
            canvasHost.transform.SetParent(transform, worldPositionStays: false);
            var canvas = canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasHost.AddComponent<CanvasScaler>();
            canvasHost.AddComponent<GraphicRaycaster>();

            _panel = NewRect("FateRollPanel", canvasHost.transform, new Vector2(0.5f, 0.5f), new Vector2(900f, 780f), Vector2.zero).gameObject;
            _panel.AddComponent<Image>().color = new Color(0.08f, 0.07f, 0.06f, 0.94f);

            _title = NewText("Title", 56, new Vector2(0f, 320f), 800f, 80f);
            _stake = NewText("Stake", 32, new Vector2(0f, 240f), 800f, 50f);
            _dice = NewText("Dice", 140, new Vector2(0f, 90f), 800f, 180f);

            var barHost = NewRect("Chances", _panel.transform, new Vector2(0.5f, 0.5f), new Vector2(800f, 50f), new Vector2(0f, -70f));
            _deathBar = NewBar(barHost, DeathBarColor);
            _altarBar = NewBar(barHost, AltarBarColor);
            _inPlaceBar = NewBar(barHost, InPlaceBarColor);

            _deathLabel = NewText("DeathLabel", 30, new Vector2(-270f, -140f), 260f, 50f);
            _altarLabel = NewText("AltarLabel", 30, new Vector2(0f, -140f), 260f, 50f);
            _inPlaceLabel = NewText("InPlaceLabel", 30, new Vector2(270f, -140f), 260f, 50f);
            _result = NewText("Result", 36, new Vector2(0f, -210f), 860f, 70f);

            _rollButton = NewButton("Бросить", new Vector2(-220f, -310f), HandleRollClicked);
            _declineButton = NewButton("Отказаться", new Vector2(220f, -310f), HandleDeclineClicked);
            _panel.SetActive(false);
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return rect;
        }

        private static RectTransform NewBar(RectTransform parent, Color color)
        {
            var go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, worldPositionStays: false);
            go.GetComponent<Image>().color = color;
            var rect = (RectTransform)go.transform;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private Text NewText(string name, int size, Vector2 pos, float width, float height)
        {
            var rect = NewRect(name, _panel.transform, new Vector2(0.5f, 0.5f), new Vector2(width, height), pos);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }

        private Button NewButton(string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var rect = NewRect(label + "Button", _panel.transform, new Vector2(0.5f, 0.5f), new Vector2(380f, 100f), pos);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.2f, 0.18f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var textRect = NewRect("Label", rect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var text = textRect.gameObject.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 38;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return button;
        }
    }
}
