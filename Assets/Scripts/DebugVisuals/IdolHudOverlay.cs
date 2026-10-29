using Burmalda.Artifacts;
using Burmalda.Bootstrap;
using Burmalda.Camp;
using UnityEngine;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// HUD Идола (PRD v10 §18.1): силуэт активного Идола и шкала заряда у левого края экрана, вне зоны плит. Полная шкала
    /// пульсирует; в режиме выбора цели — подсказка. Не принимает нажатий. Временный UI на примитивах.
    /// </summary>
    public sealed class IdolHudOverlay : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(IdolHudOverlay));
            host.AddComponent<IdolHudOverlay>();
            DontDestroyOnLoad(host);
        }

        private IdolController _idol;
        private CampController _camp;
        private RawImage _icon;
        private Image _barBack, _barFill;
        private Text _hint;
        private string _shownIdol;

        private void Start() => BuildCanvas();

        private void Update()
        {
            if (_idol == null) _idol = FindFirstObjectByType<IdolController>();
            if (_camp == null) _camp = FindFirstObjectByType<CampController>();
            if (_idol == null || _camp == null || _camp.Camp == null || _icon == null) return;

            var id = _camp.Camp.Progress.ActiveIdolId;
            var visible = id != null && _idol.Charge != null;
            _icon.gameObject.SetActive(visible);
            _barBack.gameObject.SetActive(visible);
            _hint.text = "";
            if (!visible) return;

            if (id != _shownIdol)
            {
                _shownIdol = id;
                var item = CollectibleCatalog.Get(id);
                _icon.texture = CampArtPainter.SilhouetteTexture(CollectibleKind.Idol, item.PartsRequired, item.PartsRequired, item.Rarity, 0f);
            }

            var fill = _idol.Charge.Fill;
            var rect = _barFill.rectTransform;
            rect.anchorMax = new Vector2(1f, fill);
            var pulse = _idol.Charge.IsFull ? 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f) : 0.55f;
            _barFill.color = new Color(1f, 0.82f, 0.25f, pulse);
            if (_idol.Run != null && _idol.Run.IsTargeting) _hint.text = "Выбери цель";
        }

        private void BuildCanvas()
        {
            var canvasHost = new GameObject(nameof(IdolHudOverlay) + "_Canvas");
            canvasHost.transform.SetParent(transform, worldPositionStays: false);
            var canvas = canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 89;
            var scaler = canvasHost.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            _icon = NewRect("IdolIcon", canvasHost.transform, new Vector2(100f, 100f), new Vector2(16f, 420f)).gameObject.AddComponent<RawImage>();
            _icon.raycastTarget = false;
            var barRect = NewRect("ChargeBar", canvasHost.transform, new Vector2(26f, 260f), new Vector2(54f, 140f));
            _barBack = barRect.gameObject.AddComponent<Image>();
            _barBack.color = new Color(0.05f, 0.05f, 0.06f, 0.75f);
            _barBack.raycastTarget = false;
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(barRect, false);
            _barFill = fill.GetComponent<Image>();
            _barFill.raycastTarget = false;
            var fillRect = _barFill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 0f);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);

            var hint = NewRect("Hint", canvasHost.transform, new Vector2(500f, 60f), Vector2.zero);
            hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 0f);
            hint.pivot = new Vector2(0.5f, 0f);
            hint.anchoredPosition = new Vector2(0f, 200f);
            _hint = hint.gameObject.AddComponent<Text>();
            _hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _hint.fontSize = 40;
            _hint.fontStyle = FontStyle.Bold;
            _hint.alignment = TextAnchor.MiddleCenter;
            _hint.color = new Color(1f, 0.85f, 0.3f);
            _hint.raycastTarget = false;
        }

        // Левый край, снизу вверх: якорь внизу-слева.
        private static RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return rect;
        }
    }
}
