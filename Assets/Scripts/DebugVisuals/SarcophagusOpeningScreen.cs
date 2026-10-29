using System.Linq;
using System.Collections.Generic;
using Burmalda.Artifacts;
using Burmalda.Camp;
using Burmalda.Movement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Экран Лагеря после эвакуации (PRD v10 §22.2–22.3): Саркофаги открываются по одному, от менее ценных к более —
    /// свечение редкости (синее / фиолетовое / золотое) → Монеты → новая Реликвия (на полку бога) или часть (удар света и
    /// звука, зажигается кусок силуэта; при выборе — экран «чья часть»). Внизу два постамента — Идол и Тотем: тёмный силуэт,
    /// каждая часть зажигает кусок (<c>1/4</c>), накопленная защита от невезения делает силуэт ярче [ГД]. В конце — одна
    /// ближайшая цель и «Новая экспедиция». Без рекламы между возвращением и первым открытием. Картинки и звук рисуются кодом.
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
        private RectTransform _stage, _choices;
        private RawImage _stageImage, _glowImage, _pedestalIdol, _pedestalTotem;
        private Image _flash;
        private Text _title, _body, _buttonLabel, _idolLabel, _totemLabel, _goalLabel;
        private AudioSource _audio;
        private int _returnedCoins;
        private int _returnedMana;
        private bool _startupChecked;
        private float _fxStart = -10f;
        private Color _fxColor = Color.white;
        private float _fxStrength;

        private void Start()
        {
            EnsureEventSystemExists();
            BuildPanel();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
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

            AnimateFx();
        }

        private void OnDisable() => SyncEvacuation(null);

        private void SyncEvacuation(EvacuationSystem current)
        {
            if (ReferenceEquals(current, _wired)) return;
            if (_wired != null) _wired.Returned -= OnReturned;
            _wired = current;
            if (_wired != null) _wired.Returned += OnReturned;
        }

        private void OnReturned(ReturnConversionResult result)
        {
            _returnedMana = result.Mana;
            _wasReturned = true;
            Show(result.TotalCoins);
        }

        private void Show(int returnedCoins)
        {
            if (!_wasReturned) _returnedMana = 0;
            if (_flow == null || _panel == null) return;
            _returnedCoins = returnedCoins;
            _flow.Begin();
            Refresh();
            _panel.SetActive(true);
            _wasReturned = false; // следующий показ без возврата (стартовая проверка) строку не наследует
        }

        private CollectionProgress Progress => _campController.Camp.Progress;

        // Эффект открытия: расходящееся свечение редкости, вспышка и тряска силы <strength>, звук.
        private void Fx(Color color, float strength)
        {
            _fxStart = Time.unscaledTime;
            _fxColor = color;
            _fxStrength = strength;
            if (_audio != null && strength > 0f) _audio.PlayOneShot(ProceduralSfx.Beat(strength), 0.8f);
        }

        private void AnimateFx()
        {
            if (_panel == null || !_panel.activeSelf) return;
            var t = Time.unscaledTime - _fxStart;
            var glow = Mathf.Clamp01(1f - t / 1.6f);
            _glowImage.color = new Color(_fxColor.r, _fxColor.g, _fxColor.b, glow * 0.85f * (_fxStrength > 0f ? 1f : 0f));
            _glowImage.rectTransform.localScale = Vector3.one * (1f + Mathf.Min(t, 1.6f) * 0.9f);
            _flash.color = new Color(1f, 1f, 1f, Mathf.Max(0f, 1f - t / 0.3f) * (_fxStrength >= 1f ? 0.85f : 0f)); // «удар света» — только при части
            var shake = _fxStrength >= 1f ? Mathf.Max(0f, 1f - t / 0.4f) * 14f : 0f;
            _stage.anchoredPosition = new Vector2(0f, 150f) + (shake > 0f ? Random.insideUnitCircle * shake : Vector2.zero);
        }

        private bool HasAssembled() => _campController != null && _campController.Camp != null
            && Burmalda.Artifacts.CollectibleCatalog.All.Any(i => Progress.IsAssembled(i));

        private void StartRun()
        {
            if (_input != null) _input.Restart();
        }

        private bool _wasReturned;

        // Одна строка конвертации: «Мана 3200 → 118 Монет» (PRD v10 §26.1). Нет возврата в этой сессии (неоткрытые с прошлой) — «Дома!».
        private string ReturnLine() => _returnedMana > 0 ? $"Мана {_returnedMana} → {_returnedCoins} Монет" : (_returnedCoins > 0 ? $"+{_returnedCoins} Монет" : "Дома!");

        private void Refresh()
        {
            foreach (Transform child in _choices) Destroy(child.gameObject);
            _stage.gameObject.SetActive(true);
            _goalLabel.text = "";
            _body.color = new Color(1f, 0.84f, 0f);
            RefreshPedestals();

            switch (_flow.Phase)
            {
                case SarcophagusOpeningPhase.Closed:
                    _title.text = ReturnLine();
                    _body.text = $"Саркофагов: {_flow.Remaining}";
                    _stageImage.texture = CampArtPainter.SarcophagusTexture(new Color(0.5f, 0.45f, 0.4f, 0f));
                    _buttonLabel.text = "Открыть";
                    break;

                case SarcophagusOpeningPhase.ChoosingPart:
                    RevealCommon();
                    _title.text = "Чья часть?";
                    _body.text = "";
                    _stage.gameObject.SetActive(false);
                    ShowChoices(_campController.Camp.PendingPartChoice);
                    _buttonLabel.text = "";
                    break;

                case SarcophagusOpeningPhase.Opened:
                    RevealOpened();
                    _buttonLabel.text = _flow.Remaining > 0 ? "Дальше" : "Готово";
                    break;

                default: // Finished
                    _title.text = ReturnLine();
                    _body.text = "";
                    _stage.gameObject.SetActive(false);
                    ShowGoal();
                    _buttonLabel.text = HasAssembled() ? "Лагерь" : "Новая экспедиция";
                    break;
            }

            _buttonLabel.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(_buttonLabel.text));
        }

        private void RevealCommon()
        {
            var loot = _flow.LastContents.Loot;
            var color = CampArtPainter.RarityGlow(loot.Rarity);
            var strength = loot.Kind == SarcophagusLootKind.Part ? 1f : loot.Kind == SarcophagusLootKind.Relic ? 0.7f : 0.35f;
            Fx(color, strength);
            _stageImage.texture = CampArtPainter.SarcophagusTexture(new Color(color.r, color.g, color.b, 1f));
        }

        private void RevealOpened()
        {
            var loot = _flow.LastContents.Loot;
            if (_flow.ChosenPart == null) RevealCommon(); // после выбора части свечение уже сыграно
            var color = CampArtPainter.RarityGlow(loot.Rarity);
            _title.text = "Саркофаг";
            _title.color = color;
            switch (loot.Kind)
            {
                case SarcophagusLootKind.Relic:
                    _stageImage.texture = RelicIconPainter.IconFor(loot.Relic);
                    _body.text = $"+{loot.Coins} Монет\nНовая Реликвия: {loot.Relic.Name}";
                    break;
                case SarcophagusLootKind.Part:
                    var item = loot.Part ?? _flow.ChosenPart;
                    var lit = Progress.PartsOf(item.Id);
                    _stageImage.texture = CampArtPainter.SilhouetteTexture(item.Kind, lit, item.PartsRequired, item.Rarity, 0f);
                    _body.text = $"+{loot.Coins} Монет\n{item.Name}: {lit}/{item.PartsRequired}";
                    break;
                default:
                    _body.text = $"+{loot.Coins} Монет";
                    break;
            }

            if (loot.Kind == SarcophagusLootKind.Part && _flow.ChosenPart != null) Fx(color, 1f); // выбранная часть — тот же удар
        }

        private void ShowChoices(IReadOnlyList<CollectibleItem> items)
        {
            var width = 260f;
            var x = -(items.Count - 1) * (width + 20f) / 2f;
            foreach (var item in items)
            {
                var captured = item;
                var rect = NewRect("Choice_" + item.Id, _choices, new Vector2(width, 330f), new Vector2(x, 0f));
                var frame = rect.gameObject.AddComponent<Image>();
                frame.color = CampArtPainter.RarityGlow(item.Rarity);
                var inner = NewRect("Inner", rect, new Vector2(width - 10f, 320f), Vector2.zero);
                inner.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.09f, 0.08f, 1f);
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = frame;
                button.onClick.AddListener(() => { _flow.ChoosePart(captured.Id); Refresh(); });
                var icon = NewRect("Icon", inner, new Vector2(200f, 200f), new Vector2(0f, 40f));
                var raw = icon.gameObject.AddComponent<RawImage>();
                raw.texture = CampArtPainter.SilhouetteTexture(item.Kind, Progress.PartsOf(item.Id), item.PartsRequired, item.Rarity, 0f);
                raw.raycastTarget = false;
                AddText(inner, $"{item.Name}  {Progress.PartsOf(item.Id)}/{item.PartsRequired}", 30, new Vector2(0f, -120f), width - 20f, 50f, Color.white);
                x += width + 20f;
            }
        }

        private void RefreshPedestals()
        {
            var idol = CampPedestals.For(Progress, CollectibleKind.Idol);
            var totem = CampPedestals.For(Progress, CollectibleKind.Totem);
            SetPedestal(_pedestalIdol, _idolLabel, idol);
            SetPedestal(_pedestalTotem, _totemLabel, totem);
        }

        private static void SetPedestal(RawImage image, Text label, PedestalState state)
        {
            var rarity = state.Item != null ? state.Item.Rarity : RelicRarity.Rare;
            image.texture = CampArtPainter.SilhouetteTexture(state.Kind, state.Lit, state.Total, rarity, state.PityGlow, state.Level > 0 ? state.Level : 1);
            label.text = state.Item != null ? $"{state.Lit}/{state.Total}" : "";
        }

        private void ShowGoal()
        {
            var goal = CampPedestals.NearestGoal(Progress);
            if (!goal.HasValue) return;
            var item = goal.Value.Item;
            _stage.gameObject.SetActive(true);
            _stageImage.texture = CampArtPainter.SilhouetteTexture(item.Kind, Progress.PartsOf(item.Id), item.PartsRequired, item.Rarity, 0f);
            _goalLabel.text = $"Ближайшая цель: {item.Name} — ещё {goal.Value.PartsLeft}";
        }

        private void HandleButton()
        {
            if (_flow.Phase == SarcophagusOpeningPhase.Finished)
            {
                _flow.Reset();
                _panel.SetActive(false);
                if (HasAssembled()) CampScreen.Open(_campController, StartRun); // собранные Идол/Тотем: выбрать и прокачать
                else StartRun();
                return;
            }

            _title.color = Color.white;
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
            var scaler = canvasHost.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasHost.AddComponent<GraphicRaycaster>();

            _panel = NewRect("OpeningPanel", canvasHost.transform, Vector2.zero, Vector2.zero).gameObject;
            var panelRect = (RectTransform)_panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            _panel.AddComponent<Image>().color = new Color(0.06f, 0.055f, 0.05f, 0.97f);

            _glowImage = NewRect("Glow", _panel.transform, new Vector2(520f, 520f), new Vector2(0f, 150f)).gameObject.AddComponent<RawImage>();
            _glowImage.texture = CampArtPainter.GlowTexture();
            _glowImage.raycastTarget = false;

            _stage = NewRect("Stage", _panel.transform, new Vector2(380f, 380f), new Vector2(0f, 150f));
            _stageImage = _stage.gameObject.AddComponent<RawImage>();
            _stageImage.raycastTarget = false;

            _title = AddText(_panel.transform, "", 56, new Vector2(0f, 560f), 980f, 90f, Color.white);
            _body = AddText(_panel.transform, "", 44, new Vector2(0f, -120f), 980f, 150f, new Color(1f, 0.84f, 0f));
            _goalLabel = AddText(_panel.transform, "", 40, new Vector2(0f, -120f), 980f, 80f, new Color(0.9f, 0.85f, 0.7f));
            _choices = NewRect("Choices", _panel.transform, Vector2.zero, new Vector2(0f, 200f));

            // Постаменты у входа в гробницу: Идол и Тотем.
            _pedestalIdol = NewRect("PedestalIdol", _panel.transform, new Vector2(230f, 230f), new Vector2(-220f, -420f)).gameObject.AddComponent<RawImage>();
            _pedestalTotem = NewRect("PedestalTotem", _panel.transform, new Vector2(230f, 230f), new Vector2(220f, -420f)).gameObject.AddComponent<RawImage>();
            _pedestalIdol.raycastTarget = _pedestalTotem.raycastTarget = false;
            _idolLabel = AddText(_panel.transform, "", 36, new Vector2(-220f, -560f), 260f, 50f, Color.white);
            _totemLabel = AddText(_panel.transform, "", 36, new Vector2(220f, -560f), 260f, 50f, Color.white);

            var buttonRect = NewRect("Button", _panel.transform, new Vector2(560f, 120f), new Vector2(0f, -760f));
            var image = buttonRect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.2f, 0.18f, 1f);
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(HandleButton);
            _buttonLabel = AddText(buttonRect, "", 42, Vector2.zero, 560f, 120f, Color.white);

            _flash = NewRect("Flash", _panel.transform, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            var flashRect = _flash.rectTransform;
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = flashRect.offsetMax = Vector2.zero;
            _flash.color = new Color(1f, 1f, 1f, 0f);
            _flash.raycastTarget = false;

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

        private static Text AddText(Transform parent, string text, int size, Vector2 pos, float width, float height, Color color)
        {
            var label = NewRect("Text", parent, new Vector2(width, height), pos).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            return label;
        }
    }
}
