using System;
using UnityEngine;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals.HudDesign
{
    /// <summary>
    /// Переиспользуемые UI-примитивы дизайн-системы HUD (issue "Собрать
    /// Unity UI для HUD забега"), см. <c>docs/design/burmalda-hud-design-spec.md</c>
    /// раздел «Компоненты». Не .prefab-ассеты — строятся кодом во время
    /// выполнения, тот же принцип, что и у остальных UI-классов проекта
    /// (<see cref="CameraAnchorDebugPanel"/>/<see cref="RestartButton"/>):
    /// .unity/.prefab трогать автономно нельзя
    /// (docs/rules/forbidden-actions.md), а Unity сама умеет строить любую
    /// UI-иерархию из чистого C# без единого сохранённого ассета — вызывающая
    /// сторона просто зовёт эти статические методы как фабрику.
    ///
    /// Скруглённые углы/градиенты/тени рисуются процедурными текстурами
    /// (без единого .png) — тот же принцип "без арт-ассетов", что у
    /// <see cref="PickupFeedback"/>.
    /// </summary>
    public static class HudUiPrimitives
    {
        private const int TextureSize = 64;
        private static Font _sharedFont;

        /// <summary>
        /// Встроенный шрифт Unity — Rubik/Inter/JetBrains Mono из дизайна
        /// недоступны локально (не установлены на машине, в проекте нет
        /// .ttf/.otf) — см. отчёт задачи. Единая точка замены на будущее,
        /// если шрифты появятся в проекте.
        /// </summary>
        public static Font SharedFont => _sharedFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, worldPositionStays: false);
            return (RectTransform)host.transform;
        }

        public static Text CreateLabel(Transform parent, string name, string content, int fontSize, Color32 color, bool bold, TextAnchor alignment)
        {
            var rect = CreateRect(parent, name);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = SharedFont;
            text.fontSize = fontSize;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = color;
            text.alignment = alignment;
            text.text = content;
            return text;
        }

        // === Скруглённые прямоугольники (карточки/кнопки/слоты) ===

        /// <summary>Заливка одним цветом + опциональная рамка, скруглённые углы, 9-slice текстура.</summary>
        public static Image CreateRoundedPanel(Transform parent, string name, Color32 fill, Color32 borderColor, int borderWidth, int cornerRadius)
        {
            var rect = CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.type = Image.Type.Sliced;
            image.sprite = BuildRoundedRectSprite(cornerRadius, borderWidth, borderColor, (_, _) => fill);
            image.color = Color.white; // цвет уже в текстуре — Image.color не тонирует повторно
            return image;
        }

        /// <summary>Вертикальный градиент (верх→низ) без рамки, скруглённые углы — золотая/опасная кнопка.</summary>
        public static Image CreateRoundedGradientPanel(Transform parent, string name, Color32 top, Color32 bottom, int cornerRadius)
        {
            var rect = CreateRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.type = Image.Type.Sliced;
            image.sprite = BuildRoundedRectSprite(cornerRadius, 0, default, (_, v) => Color32.Lerp(bottom, top, v));
            image.color = Color.white;
            return image;
        }

        /// <summary>Пунктирная рамка без заливки — пустой слот иконки (issue: "пустые слоты НЕ скрывать").</summary>
        public static void AddDashedBorder(RectTransform target, Color32 dashColor, int cornerRadius)
        {
            var image = target.gameObject.AddComponent<Image>();
            image.type = Image.Type.Sliced;
            image.sprite = BuildDashedRoundedRectSprite(cornerRadius, dashColor);
            image.color = Color.white;
        }

        // === Карточка предмета (issue: "рамка = цвет тира редкости") ===

        public static Image CreateItemCard(Transform parent, string name, Color32 rarityBorderColor)
        {
            return CreateRoundedPanel(parent, name, BurmaldaHudPalette.SurfaceCard, rarityBorderColor, borderWidth: 2, cornerRadius: 16);
        }

        // === Кнопки ===

        public static Button CreatePrimaryButton(Transform parent, string label, Action onClick)
        {
            return CreateButton(parent, "PrimaryButton", label, BurmaldaHudPalette.GoldButtonTop, BurmaldaHudPalette.GoldButtonBottom,
                BurmaldaHudPalette.GoldButtonShadow, BurmaldaHudPalette.GoldButtonText, onClick);
        }

        public static Button CreateDangerButton(Transform parent, string label, Action onClick)
        {
            return CreateButton(parent, "DangerButton", label, BurmaldaHudPalette.DangerButtonTop, BurmaldaHudPalette.DangerButtonBottom,
                BurmaldaHudPalette.GoldButtonShadow, BurmaldaHudPalette.TextPrimary, onClick);
        }

        public static Button CreateSecondaryButton(Transform parent, string label, Action onClick)
        {
            var rect = CreateRect(parent, "SecondaryButton");
            var panel = CreateRoundedPanel(rect, "Background", BurmaldaHudPalette.SurfaceCard, BurmaldaHudPalette.StoneBorder, borderWidth: 2, cornerRadius: 12);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = panel;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            AddCenteredLabel(rect, label, BurmaldaHudPalette.TextPrimary);
            return button;
        }

        private static Button CreateButton(Transform parent, string name, string label, Color32 top, Color32 bottom, Color32 shadow, Color32 textColor, Action onClick)
        {
            var rect = CreateRect(parent, name);

            // Hard-shadow (issue: "эффект нажимной кнопки") — та же скруглённая форма, сплошной цвет, смещена вниз, БЕЗ блюра.
            var shadowPanel = CreateRoundedPanel(rect, "Shadow", shadow, shadow, borderWidth: 0, cornerRadius: 12);
            var shadowRect = (RectTransform)shadowPanel.transform;
            shadowRect.anchorMin = Vector2.zero;
            shadowRect.anchorMax = Vector2.one;
            shadowRect.offsetMin = new Vector2(0f, -6f);
            shadowRect.offsetMax = new Vector2(0f, -6f);

            var gradient = CreateRoundedGradientPanel(rect, "Gradient", top, bottom, cornerRadius: 12);
            var gradientRect = (RectTransform)gradient.transform;
            gradientRect.anchorMin = Vector2.zero;
            gradientRect.anchorMax = Vector2.one;
            gradientRect.offsetMin = Vector2.zero;
            gradientRect.offsetMax = Vector2.zero;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = gradient;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            AddCenteredLabel(gradientRect, label, textColor);
            return button;
        }

        private static void AddCenteredLabel(Transform parent, string label, Color32 color)
        {
            var text = CreateLabel(parent, "Label", label, 24, color, bold: true, TextAnchor.MiddleCenter);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // === Счётчик валюты (иконка + число + подпись) ===

        public enum CurrencyIconShape
        {
            Diamond,
            Circle
        }

        public readonly struct CurrencyCounter
        {
            public readonly Text ValueText;
            public CurrencyCounter(Text valueText) => ValueText = valueText;
        }

        /// <summary>Ромб (Мана) или кружок (Монета/Ключ) + крупное число (Rubik 800 в дизайне — здесь bold встроенного шрифта, см. SharedFont).</summary>
        public static CurrencyCounter CreateCurrencyCounter(Transform parent, string name, Color32 iconColor, CurrencyIconShape shape)
        {
            var root = CreateRect(parent, name);
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var iconRect = CreateRect(root, "Icon");
            var iconImage = iconRect.gameObject.AddComponent<Image>();
            iconImage.sprite = shape == CurrencyIconShape.Circle ? BuildCircleSprite(iconColor) : BuildDiamondSprite(iconColor);
            iconImage.color = Color.white;
            var iconLayout = iconRect.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 28f;
            iconLayout.preferredHeight = 28f;

            var valueText = CreateLabel(root, "Value", "0", 28, BurmaldaHudPalette.TextPrimary, bold: true, TextAnchor.MiddleLeft);
            var valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
            valueLayout.preferredWidth = 90f;

            return new CurrencyCounter(valueText);
        }

        // === Золотая «таблетка» множителя с пульсацией ===

        public static (RectTransform root, Text valueText) CreateMultiplierPill(Transform parent, string name)
        {
            var panel = CreateRoundedPanel(parent, name, BurmaldaHudPalette.GoldAccent, BurmaldaHudPalette.GoldAccent, borderWidth: 0, cornerRadius: 999);
            var rect = (RectTransform)panel.transform;
            var text = CreateLabel(rect, "Value", "×1.0", 22, BurmaldaHudPalette.GoldButtonText, bold: true, TextAnchor.MiddleCenter);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            rect.gameObject.AddComponent<HudPulseAnimation>();
            return (rect, text);
        }

        // === Слот иконки артефакта (заполненный/пустой) ===

        public static RectTransform CreateFilledIconSlot(Transform parent, string name, string twoLetterCode)
        {
            var panel = CreateRoundedPanel(parent, name, BurmaldaHudPalette.SurfaceCard, BurmaldaHudPalette.GoldAccent, borderWidth: 2, cornerRadius: 12);
            var rect = (RectTransform)panel.transform;
            AddCenteredLabel(rect, twoLetterCode, BurmaldaHudPalette.TextPrimary);
            return rect;
        }

        public static RectTransform CreateEmptyIconSlot(Transform parent, string name)
        {
            var rect = CreateRect(parent, name);
            AddDashedBorder(rect, BurmaldaHudPalette.EmptySlotDash, cornerRadius: 12);
            return rect;
        }

        // === Процедурные текстуры ===

        private static Sprite BuildRoundedRectSprite(int cornerRadius, int borderWidth, Color32 borderColor, Func<int, float, Color32> colorAt)
        {
            // Клампим радиус до половины текстуры — больше даёт вырожденный
            // результат в IsInsideRoundedRect (кажется квадратом без
            // скругления вместо капсулы/пилюли). Вызывающая сторона просит
            // "максимально скруглить" через большие значения (999 — пилюля
            // множителя) — клампим здесь один раз, а не считаем на каждом call site.
            cornerRadius = Mathf.Min(cornerRadius, TextureSize / 2);
            var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[TextureSize * TextureSize];
            for (var y = 0; y < TextureSize; y++)
            {
                var v = y / (float)(TextureSize - 1);
                for (var x = 0; x < TextureSize; x++)
                {
                    var inside = IsInsideRoundedRect(x, y, TextureSize, TextureSize, cornerRadius);
                    if (!inside) { pixels[y * TextureSize + x] = new Color32(0, 0, 0, 0); continue; }

                    var isBorder = borderWidth > 0 && !IsInsideRoundedRect(x, y, TextureSize, TextureSize, cornerRadius, borderWidth);
                    pixels[y * TextureSize + x] = isBorder ? borderColor : colorAt(x, v);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            var border = Mathf.Max(cornerRadius, borderWidth);
            return Sprite.Create(tex, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        // Пунктир — рамка из коротких сегментов вдоль периметра скруглённого прямоугольника (issue: "пустой — пунктирная рамка").
        private static Sprite BuildDashedRoundedRectSprite(int cornerRadius, Color32 dashColor)
        {
            cornerRadius = Mathf.Min(cornerRadius, TextureSize / 2); // см. BuildRoundedRectSprite — тот же вырожденный случай
            const int borderWidth = 3;
            const int dashPeriod = 10; // px текстуры между серединами штрихов
            const int dashLength = 5;

            var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            var pixels = new Color32[TextureSize * TextureSize];
            for (var y = 0; y < TextureSize; y++)
            for (var x = 0; x < TextureSize; x++)
            {
                var onOuter = IsInsideRoundedRect(x, y, TextureSize, TextureSize, cornerRadius);
                var onInner = IsInsideRoundedRect(x, y, TextureSize, TextureSize, cornerRadius, borderWidth);
                var isRing = onOuter && !onInner;
                var isDash = ((x + y) % dashPeriod) < dashLength; // диагональный пунктир — простой, без тригонометрии по периметру
                pixels[y * TextureSize + x] = isRing && isDash ? dashColor : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            var border = cornerRadius;
            return Sprite.Create(tex, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        private static bool IsInsideRoundedRect(int x, int y, int width, int height, int cornerRadius, int inset = 0)
        {
            var minX = inset;
            var minY = inset;
            var maxX = width - 1 - inset;
            var maxY = height - 1 - inset;
            if (x < minX || x > maxX || y < minY || y > maxY) return false;

            var r = Mathf.Max(cornerRadius - inset, 0);
            // Ближайший угол — проверяем расстояние только там, где текстурная координата реально в угловой зоне.
            float cx = x < minX + r ? minX + r : x > maxX - r ? maxX - r : x;
            float cy = y < minY + r ? minY + r : y > maxY - r ? maxY - r : y;
            var dx = x - cx;
            var dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        private static Sprite BuildCircleSprite(Color32 fill)
        {
            var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[TextureSize * TextureSize];
            var center = (TextureSize - 1) / 2f;
            var radius = TextureSize / 2f - 1f;
            for (var y = 0; y < TextureSize; y++)
            for (var x = 0; x < TextureSize; x++)
            {
                var dx = x - center;
                var dy = y - center;
                pixels[y * TextureSize + x] = dx * dx + dy * dy <= radius * radius ? fill : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f));
        }

        private static Sprite BuildDiamondSprite(Color32 fill)
        {
            var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[TextureSize * TextureSize];
            var center = (TextureSize - 1) / 2f;
            var half = TextureSize / 2f - 1f;
            for (var y = 0; y < TextureSize; y++)
            for (var x = 0; x < TextureSize; x++)
            {
                var manhattan = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                pixels[y * TextureSize + x] = manhattan <= half ? fill : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f));
        }
    }
}
