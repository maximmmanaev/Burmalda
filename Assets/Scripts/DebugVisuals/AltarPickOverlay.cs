using System.Collections.Generic;
using Burmalda.Altar;
using Burmalda.Artifacts;
using Burmalda.Movement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Экран Алтаря «выбор 1 из 2» (PRD v10 §13.2): две карточки (три — с бонусом тайника за Воротами или Талисманом
    /// «Ключ подземных врат»), у каждой — тип, название, рамка редкости и короткий эффект. Нажатие на карточку берёт
    /// Реликвию. Кнопки закрытия или пропуска нет. Если слоты типа заполнены — второй шаг: выбор заменяемого предмета.
    /// Пока экран открыт, забег стоит (ввод заблокирован, время остановлено) — решение не стоит распада. Также
    /// показывает маркер бонуса тайника. Временный UI на примитивах (как <see cref="FateRollOverlay"/>); ничего не
    /// трогает в .unity/.prefab.
    /// </summary>
    public sealed class AltarPickOverlay : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.05f, 0.96f);
        private static readonly Color CardColor = new Color(0.16f, 0.14f, 0.12f, 1f);
        private const float CardWidth = 300f;
        private const float CardHeight = 440f;
        private const float CardSpacing = 30f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(AltarPickOverlay));
            host.AddComponent<AltarPickOverlay>();
            DontDestroyOnLoad(host);
        }

        private AltarController _altar;
        private GridTraceInputController _input;
        private AltarPickSession _session;
        private GameObject _panel;
        private RectTransform _content;
        private Text _vaultMarker;
        private float _savedTimeScale = 1f;
        private bool _frozen;

        private void Start()
        {
            EnsureEventSystemExists();
            BuildCanvas();
        }

        private void Update()
        {
            if (_altar == null)
            {
                _altar = FindFirstObjectByType<AltarController>();
                if (_altar != null) _altar.PickOffered += HandlePickOffered;
            }

            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
            if (_vaultMarker != null) _vaultMarker.text = _altar != null ? AltarPickText.VaultMarker(_altar.PendingVaultOptions) : "";
        }

        private void OnDestroy()
        {
            if (_altar != null) _altar.PickOffered -= HandlePickOffered;
            Unfreeze();
        }

        private void HandlePickOffered(AltarPickSession session)
        {
            _session = session;
            session.Completed += _ => Close();
            Freeze();
            Render();
            _panel.SetActive(true);
        }

        private void Close()
        {
            _session = null;
            if (_panel != null) _panel.SetActive(false);
            Unfreeze();
        }

        private void Freeze()
        {
            if (_frozen) return;
            _frozen = true;
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (_input != null) _input.MarkDead(); // ввод стоит, пока игрок выбирает (как при Броске Судьбы)
        }

        private void Unfreeze()
        {
            if (!_frozen) return;
            _frozen = false;
            Time.timeScale = _savedTimeScale;
            if (_input != null) _input.ResumeAfterFate();
        }

        private void Render()
        {
            foreach (Transform child in _content) Destroy(child.gameObject);
            if (_session == null) return;

            if (_session.State == AltarPickState.Choosing)
            {
                AddLabel(AltarPickText.Title(_session.Options.Count), 52, new Vector2(0f, 360f), 900f, 80f);
                LayoutCards(_session.Options, relic => _session.Pick(relic.Id), () => Render(), 0f);
            }
            else if (_session.State == AltarPickState.ChoosingReplacement)
            {
                AddLabel(AltarPickText.ReplaceTitle(_session.Pending.Category), 40, new Vector2(0f, 400f), 1000f, 70f);
                AddCard(_session.Pending, new Vector2(0f, 150f), 0.72f, null);
                AddLabel("заменяет", 30, new Vector2(0f, -45f), 400f, 40f);
                LayoutCards(_session.ReplaceCandidates, relic => _session.Replace(relic.Id), () => Render(), -300f, 0.8f);
                AddButton("Назад", new Vector2(0f, -560f), () => { _session.BackToCards(); Render(); });
            }
        }

        private void LayoutCards(IReadOnlyList<RelicDefinition> relics, System.Func<RelicDefinition, bool> onTap, System.Action afterTap, float y, float scale = 1f)
        {
            var total = relics.Count * CardWidth * scale + (relics.Count - 1) * CardSpacing;
            var x = -total / 2f + CardWidth * scale / 2f;
            foreach (var relic in relics)
            {
                var captured = relic;
                AddCard(captured, new Vector2(x, y), scale, () => { onTap(captured); afterTap(); });
                x += CardWidth * scale + CardSpacing;
            }
        }

        private void AddCard(RelicDefinition relic, Vector2 position, float scale, UnityEngine.Events.UnityAction onClick)
        {
            var frame = NewRect("Card_" + relic.Id, _content, new Vector2(CardWidth * scale + 12f, CardHeight * scale + 12f), position);
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.color = AltarPickText.RarityColor(relic.Rarity);
            var inner = NewRect("Inner", frame, new Vector2(CardWidth * scale, CardHeight * scale), Vector2.zero);
            var innerImage = inner.gameObject.AddComponent<Image>();
            innerImage.color = CardColor;

            if (onClick != null)
            {
                var button = frame.gameObject.AddComponent<Button>();
                button.targetGraphic = frameImage;
                button.onClick.AddListener(onClick);
            }

            AddText(inner, AltarPickText.KindGlyph(relic.Category), Mathf.RoundToInt(120 * scale), new Vector2(0f, 110f * scale), CardWidth * scale, 150f * scale, AltarPickText.RarityColor(relic.Rarity));
            AddText(inner, AltarPickText.KindName(relic.Category), Mathf.RoundToInt(26 * scale), new Vector2(0f, 20f * scale), CardWidth * scale, 34f * scale, new Color(0.75f, 0.72f, 0.68f));
            AddText(inner, relic.Name, Mathf.RoundToInt(34 * scale), new Vector2(0f, -45f * scale), CardWidth * scale - 20f, 90f * scale, Color.white);
            AddText(inner, relic.Effect, Mathf.RoundToInt(22 * scale), new Vector2(0f, -150f * scale), CardWidth * scale - 24f, 150f * scale, new Color(0.85f, 0.82f, 0.78f), FontStyle.Normal);
        }

        private void AddLabel(string text, int size, Vector2 position, float width, float height) =>
            AddText(_content, text, size, position, width, height, Color.white);

        private void AddButton(string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var rect = NewRect(label + "Button", _content, new Vector2(380f, 100f), position);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.2f, 0.18f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            AddText(rect, label, 38, Vector2.zero, 380f, 100f, Color.white);
        }

        private void BuildCanvas()
        {
            var canvasHost = new GameObject(nameof(AltarPickOverlay) + "_Canvas");
            canvasHost.transform.SetParent(transform, worldPositionStays: false);
            var canvas = canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 105;
            var scaler = canvasHost.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasHost.AddComponent<GraphicRaycaster>();

            // Маркер бонуса тайника — всегда виден в забеге (PRD v10 §10), правый верх под Ярусом.
            var marker = NewRect("VaultMarker", canvasHost.transform, new Vector2(520f, 40f), Vector2.zero);
            marker.anchorMin = marker.anchorMax = new Vector2(1f, 1f);
            marker.pivot = new Vector2(1f, 1f);
            marker.anchoredPosition = new Vector2(-24f, -230f);
            _vaultMarker = marker.gameObject.AddComponent<Text>();
            _vaultMarker.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _vaultMarker.fontSize = 26;
            _vaultMarker.alignment = TextAnchor.MiddleRight;
            _vaultMarker.color = new Color(1f, 0.82f, 0.25f);
            _vaultMarker.raycastTarget = false;

            _panel = new GameObject("AltarPickPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(canvasHost.transform, worldPositionStays: false);
            var panelRect = (RectTransform)_panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = PanelColor; // перекрывает весь экран и перехватывает нажатия
            _content = NewRect("Content", panelRect, Vector2.zero, Vector2.zero);
            _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.SetActive(false);
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static void AddText(Transform parent, string text, int size, Vector2 position, float width, float height, Color color, FontStyle style = FontStyle.Bold)
        {
            var rect = NewRect("Text", parent, new Vector2(width, height), position);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
        }

        private static void EnsureEventSystemExists()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var host = new GameObject(nameof(EventSystem));
            DontDestroyOnLoad(host);
            host.AddComponent<EventSystem>();
            host.AddComponent<InputSystemUIInputModule>();
        }
    }
}
