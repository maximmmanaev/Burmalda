using System.Collections.Generic;
using Burmalda.Altar;
using Burmalda.Artifacts;
using Burmalda.Bootstrap;
using Burmalda.Currencies;
using Burmalda.Generation;
using Burmalda.Movement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// HUD Реликвий в забеге (PRD v10 §20.4, §12.2): иконки Рун у правого края экрана, вне зоны плит — тап по иконке
    /// активирует Руну (разовая способность; использованная гаснет, Морок и Ночь Чернобога показывают остаток времени), и
    /// серия чистого прохода — значок слева внизу и «подсветка пути» (золотая кромка у левого края растёт с серией).
    /// Временный UI на примитивах; ничего не трогает в .unity/.prefab.
    /// </summary>
    public sealed class RuneHudOverlay : MonoBehaviour
    {
        private const float IconSize = 120f;
        private const float EdgeMargin = 16f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(RuneHudOverlay));
            host.AddComponent<RuneHudOverlay>();
            DontDestroyOnLoad(host);
        }

        private sealed class Slot
        {
            public RelicDefinition Relic;
            public RawImage Image;
            public Text Timer;
        }

        private AltarController _altar;
        private CurrencyController _currency;
        private GridTraceInputController _input;
        private SegmentGenerationController _segments;
        private RectTransform _runeColumn;
        private Image _streakBadge, _pathGlow;
        private Text _streakText;
        private readonly List<Slot> _slots = new List<Slot>();
        private RunRelicBuild _shownBuild;
        private int _shownRuneCount = -1;
        private RuneActions _actions;
        private object _actionsFor;

        private void Start()
        {
            EnsureEventSystemExists();
            BuildCanvas();
        }

        private void Update()
        {
            if (_altar == null) _altar = FindFirstObjectByType<AltarController>();
            if (_currency == null) _currency = FindFirstObjectByType<CurrencyController>();
            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
            if (_segments == null) _segments = FindFirstObjectByType<SegmentGenerationController>();
            if (_altar == null || _altar.Effects == null || _runeColumn == null) return;

            var runes = _altar.Build.OfType(ArtifactCategory.Rune);
            if (!ReferenceEquals(_shownBuild, _altar.Build) || runes.Count != _shownRuneCount) RebuildSlots(runes);
            RefreshSlots();
            RefreshStreak();
        }

        private void RebuildSlots(IReadOnlyList<RelicDefinition> runes)
        {
            foreach (Transform child in _runeColumn) Destroy(child.gameObject);
            _slots.Clear();
            _shownBuild = _altar.Build;
            _shownRuneCount = runes.Count;
            for (var i = 0; i < runes.Count; i++)
            {
                var relic = runes[i];
                var rect = NewRect("Rune_" + relic.Id, _runeColumn, new Vector2(IconSize, IconSize), Vector2.zero);
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-EdgeMargin, 320f + i * (IconSize + 14f));
                var image = rect.gameObject.AddComponent<RawImage>();
                image.texture = RelicIconPainter.IconFor(relic);
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                var captured = relic;
                button.onClick.AddListener(() => Activate(captured));
                var timer = AddText(rect, "", 40, Vector2.zero, IconSize, IconSize, Color.white);
                _slots.Add(new Slot { Relic = relic, Image = image, Timer = timer });
            }
        }

        private void Activate(RelicDefinition relic)
        {
            if (_input == null || _input.Grid == null || _input.Trail == null) return;
            if (_actions == null || !ReferenceEquals(_actionsFor, _altar.Effects) || !ReferenceEquals(_actionsForTrail, _input.Trail))
            {
                _actions = new RuneActions(_altar.Effects, _input.Grid, _input.Trail, row => _segments?.EnsureCoveredThrough(row));
                _actionsFor = _altar.Effects;
                _actionsForTrail = _input.Trail;
            }

            _actions.Activate(relic.Id);
        }

        private object _actionsForTrail;

        private void RefreshSlots()
        {
            foreach (var slot in _slots)
            {
                var usable = _altar.Effects.CanUseRune(slot.Relic.Id);
                slot.Image.color = usable ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                var remaining = slot.Relic.Id == RelicEffectState.MorokRune ? _altar.Effects.MorokRemaining
                    : slot.Relic.Id == RelicEffectState.NightRune && _altar.Effects.IsNightActive ? RelicEffectState.NightSeconds : 0f;
                slot.Timer.text = slot.Relic.Id == RelicEffectState.MorokRune && _altar.Effects.IsMorokActive ? Mathf.CeilToInt(remaining).ToString()
                    : slot.Relic.Id == RelicEffectState.NightRune && _altar.Effects.IsNightActive ? "●" : "";
            }
        }

        private void RefreshStreak()
        {
            var streak = _currency != null ? _currency.Streak : null;
            var count = streak != null ? streak.Streak : 0;
            var glow = streak != null ? streak.Glow : 0f;
            _streakBadge.gameObject.SetActive(count >= 2);
            _streakText.text = count >= 2 ? $"Серия {count}" : "";
            _streakBadge.color = new Color(0.25f + 0.6f * glow, 0.2f + 0.45f * glow, 0.05f, 0.55f + 0.35f * glow);
            _pathGlow.color = new Color(1f, 0.82f, 0.25f, glow * 0.55f); // подсветка пути растёт с серией
        }

        private void BuildCanvas()
        {
            var canvasHost = new GameObject(nameof(RuneHudOverlay) + "_Canvas");
            canvasHost.transform.SetParent(transform, worldPositionStays: false);
            var canvas = canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = canvasHost.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasHost.AddComponent<GraphicRaycaster>();

            _pathGlow = NewRect("PathGlow", canvasHost.transform, new Vector2(18f, 0f), Vector2.zero).gameObject.AddComponent<Image>();
            var glowRect = _pathGlow.rectTransform;
            glowRect.anchorMin = new Vector2(0f, 0f);
            glowRect.anchorMax = new Vector2(0f, 1f);
            glowRect.pivot = new Vector2(0f, 0.5f);
            glowRect.sizeDelta = new Vector2(18f, 0f);
            glowRect.anchoredPosition = Vector2.zero;
            _pathGlow.raycastTarget = false;
            _pathGlow.color = new Color(1f, 0.82f, 0.25f, 0f);

            _runeColumn = NewRect("Runes", canvasHost.transform, Vector2.zero, Vector2.zero);
            _runeColumn.anchorMin = Vector2.zero;
            _runeColumn.anchorMax = Vector2.one;
            _runeColumn.offsetMin = _runeColumn.offsetMax = Vector2.zero;

            var badge = NewRect("StreakBadge", canvasHost.transform, new Vector2(260f, 70f), Vector2.zero);
            badge.anchorMin = badge.anchorMax = new Vector2(0f, 0f);
            badge.pivot = new Vector2(0f, 0f);
            badge.anchoredPosition = new Vector2(EdgeMargin + 20f, 320f);
            _streakBadge = badge.gameObject.AddComponent<Image>();
            _streakBadge.raycastTarget = false;
            _streakText = AddText(badge, "", 34, Vector2.zero, 260f, 70f, Color.white);
            _streakBadge.gameObject.SetActive(false);
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
