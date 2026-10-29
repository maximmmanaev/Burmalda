using Burmalda.Core;
using System;
using System.Collections.Generic;
using Burmalda.Artifacts;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>Символ эффекта в центре иконки Реликвии (пиктограмма «что», PRD v10 §20.1: описания визуальные, без текста).</summary>
    public enum RelicGlyph
    {
        Block, Blades, Burst, Flame, Jaws, Eye, Diamond, Cross, Bolt, Wave, Gate, Moon, Spindle, Ring, Dots,
    }

    /// <summary>
    /// Рисует иконки Реликвий и фон карточки кодом, без внешних картинок: форма по типу (Амулет — щит, Талисман —
    /// восьмиконечная звезда, Руна — двойное кольцо), символ эффекта (<see cref="RelicGlyph"/>) и цвет бога.
    /// Всё считается по знаковым расстояниям (SDF) со сглаженным краем — один и тот же результат на любом устройстве.
    /// Временный арт до финальных иллюстраций; ничего не трогает в .unity/.prefab.
    /// </summary>
    public static class RelicIconPainter
    {
        public const int IconSize = 128;

        private static readonly Dictionary<string, RelicGlyph> Glyphs = new Dictionary<string, RelicGlyph>
        {
            ["relic-leshy-amulet"] = RelicGlyph.Block, ["relic-leshy-talisman"] = RelicGlyph.Cross, ["relic-leshy-rune"] = RelicGlyph.Moon,
            ["relic-mokosh-amulet"] = RelicGlyph.Ring, ["relic-mokosh-talisman"] = RelicGlyph.Spindle, ["relic-mokosh-rune"] = RelicGlyph.Eye,
            ["relic-perun-amulet"] = RelicGlyph.Flame, ["relic-perun-talisman"] = RelicGlyph.Bolt, ["relic-perun-rune"] = RelicGlyph.Block,
            ["relic-humbaba-amulet"] = RelicGlyph.Burst, ["relic-humbaba-talisman"] = RelicGlyph.Diamond, ["relic-humbaba-rune"] = RelicGlyph.Wave,
            ["relic-ereshkigal-amulet"] = RelicGlyph.Blades, ["relic-ereshkigal-talisman"] = RelicGlyph.Dots, ["relic-ereshkigal-rune"] = RelicGlyph.Gate,
            ["relic-enlil-amulet"] = RelicGlyph.Blades, ["relic-enlil-talisman"] = RelicGlyph.Cross, ["relic-enlil-rune"] = RelicGlyph.Wave,
            ["relic-memegwesi-amulet"] = RelicGlyph.Block, ["relic-memegwesi-talisman"] = RelicGlyph.Wave, ["relic-memegwesi-rune"] = RelicGlyph.Bolt,
            ["relic-mishibijiu-amulet"] = RelicGlyph.Jaws, ["relic-mishibijiu-talisman"] = RelicGlyph.Diamond, ["relic-mishibijiu-rune"] = RelicGlyph.Moon,
            ["relic-animikii-amulet"] = RelicGlyph.Blades, ["relic-animikii-talisman"] = RelicGlyph.Eye, ["relic-animikii-rune"] = RelicGlyph.Bolt,
            ["relic-chernobog-amulet"] = RelicGlyph.Jaws, ["relic-chernobog-talisman"] = RelicGlyph.Diamond, ["relic-chernobog-rune"] = RelicGlyph.Moon,
        };

        public static RelicGlyph GlyphOf(RelicDefinition relic) => Glyphs.TryGetValue(relic.Id, out var g) ? g : RelicGlyph.Ring;

        /// <summary>Основной цвет бога: у каждого мира свой тон (славянский — лес и сталь, шумерский — глина и закат, оджибве — вода и гроза).</summary>
        public static Color GodColor(God god) => god switch
        {
            God.Leshy => new Color(0.38f, 0.72f, 0.40f),
            God.Mokosh => new Color(0.86f, 0.52f, 0.72f),
            God.Perun => new Color(0.45f, 0.65f, 0.95f),
            God.Chernobog => new Color(0.62f, 0.30f, 0.62f),
            God.Humbaba => new Color(0.78f, 0.58f, 0.34f),
            God.Ereshkigal => new Color(0.86f, 0.28f, 0.30f),
            God.Enlil => new Color(0.55f, 0.82f, 0.92f),
            God.Memegwesi => new Color(0.30f, 0.78f, 0.74f),
            God.Mishibijiu => new Color(0.88f, 0.52f, 0.28f),
            _ => new Color(0.92f, 0.82f, 0.32f),
        };

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>Иконка Реликвии (кэшируется; текстуры живут до выхода из игры).</summary>
        public static Texture2D IconFor(RelicDefinition relic)
        {
            if (Cache.TryGetValue(relic.Id, out var cached) && cached != null) return cached;
            var texture = ToTexture(Paint(relic), IconSize, IconSize);
            Cache[relic.Id] = texture;
            return texture;
        }

        /// <summary>Пиксели иконки (RGBA, строки снизу вверх) — чистая функция от Реликвии, без Unity-объектов.</summary>
        public static Color32[] Paint(RelicDefinition relic, int size = IconSize)
        {
            var god = GodColor(relic.God);
            var rarity = AltarPickText.RarityColor(relic.Rarity);
            var glyph = GlyphOf(relic);
            var body = Color.Lerp(new Color(0.10f, 0.09f, 0.08f), god, 0.28f);
            var light = Color.Lerp(god, Color.white, 0.55f);
            var pixels = new Color32[size * size];
            var aa = 2f / size;

            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                    var color = new Color(0f, 0f, 0f, 0f);

                    var shape = Emblem(relic.Category, p);
                    color = Over(color, body, Coverage(shape, aa));
                    color = Over(color, rarity, Coverage(Math.Abs(shape + 0.035f) - 0.03f, aa));
                    color = Over(color, god, Coverage(Math.Abs(shape + 0.15f) - 0.012f, aa) * 0.55f);
                    color = Over(color, light, Coverage(GlyphDistance(glyph, relic.Category == ArtifactCategory.Amulet ? p + new Vector2(0f, -0.08f) : p), aa));
                    pixels[y * size + x] = color;
                }

            return pixels;
        }

        // ---- Афиша (PRD v10 §13.3): знак бога и типы угроз — символами, без текста ----

        public static RelicGlyph ThreatGlyph(ThreatType threat) => threat switch
        {
            ThreatType.Blunt => RelicGlyph.Block,
            ThreatType.Slashing => RelicGlyph.Blades,
            ThreatType.Explosion => RelicGlyph.Burst,
            ThreatType.Fire => RelicGlyph.Flame,
            _ => RelicGlyph.Jaws,
        };

        public static Color ThreatColor(ThreatType threat) => threat switch
        {
            ThreatType.Blunt => new Color(0.75f, 0.62f, 0.45f),
            ThreatType.Slashing => new Color(0.78f, 0.82f, 0.90f),
            ThreatType.Explosion => new Color(0.98f, 0.62f, 0.25f),
            ThreatType.Fire => new Color(1.00f, 0.42f, 0.22f),
            _ => new Color(0.70f, 0.74f, 0.62f),
        };

        /// <summary>Значок типа угрозы: тёмная плашка с цветной рамкой и символом.</summary>
        public static Color32[] PaintThreat(ThreatType threat, int size = 96)
        {
            var color = ThreatColor(threat);
            var glyph = ThreatGlyph(threat);
            var pixels = new Color32[size * size];
            var aa = 2f / size;
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                    var plate = RoundedBox(p, 0.86f, 0.18f);
                    var c = new Color(0f, 0f, 0f, 0f);
                    c = Over(c, new Color(0.10f, 0.09f, 0.08f), Coverage(plate, aa));
                    c = Over(c, color, Coverage(Mathf.Abs(plate + 0.04f) - 0.04f, aa));
                    c = Over(c, Color.Lerp(color, Color.white, 0.35f), Coverage(GlyphDistance(glyph, p * 1.25f) / 1.25f, aa));
                    pixels[y * size + x] = c;
                }

            return pixels;
        }

        /// <summary>
        /// Знак бога (заглушка до арта): контур мира — круг (славянский), ромб (шумерский), треугольник (оджибве) — в цвете бога,
        /// внутри точки ранга (1–3).
        /// </summary>
        public static Color32[] PaintGodSign(YarusGod shrine, int size = 96)
        {
            var color = GodColor(shrine.God);
            var pixels = new Color32[size * size];
            var aa = 2f / size;
            var pips = (int)shrine.Rank + 1;
            var triangle = new[] { new Vector2(0f, 0.86f), new Vector2(0.86f, -0.66f), new Vector2(-0.86f, -0.66f) };
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                    var shape = shrine.Mythology switch
                    {
                        Mythology.Slavic => Circle(p, 0.84f),
                        Mythology.Sumerian => Box(Rotate(p, 45f), 0.62f),
                        _ => Polygon(p, triangle),
                    };
                    var c = new Color(0f, 0f, 0f, 0f);
                    c = Over(c, Color.Lerp(new Color(0.10f, 0.09f, 0.08f), color, 0.35f), Coverage(shape, aa));
                    c = Over(c, color, Coverage(Mathf.Abs(shape + 0.05f) - 0.05f, aa));
                    var py = shrine.Mythology == Mythology.Ojibwe ? -0.22f : -0.02f;
                    for (var i = 0; i < pips; i++)
                        c = Over(c, Color.Lerp(color, Color.white, 0.6f), Coverage(Circle(p - new Vector2((i - (pips - 1) / 2f) * 0.3f, py), 0.1f), aa));
                    pixels[y * size + x] = c;
                }

            return pixels;
        }

        private static readonly Dictionary<ThreatType, Texture2D> ThreatCache = new Dictionary<ThreatType, Texture2D>();
        private static readonly Dictionary<God, Texture2D> GodCache = new Dictionary<God, Texture2D>();

        public static Texture2D ThreatTexture(ThreatType threat)
        {
            if (ThreatCache.TryGetValue(threat, out var t) && t != null) return t;
            return ThreatCache[threat] = ToTexture(PaintThreat(threat), 96, 96);
        }

        public static Texture2D GodSignTexture(YarusGod shrine)
        {
            if (GodCache.TryGetValue(shrine.God, out var t) && t != null) return t;
            return GodCache[shrine.God] = ToTexture(PaintGodSign(shrine), 96, 96);
        }

        /// <summary>Фон карточки: тёмный вертикальный градиент с отсветом цвета редкости, внутренняя рамка и угловые метки.</summary>
        public static Color32[] PaintCardBackground(Color rarity, int width = 150, int height = 220)
        {
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var u = (x + 0.5f) / width;
                    var v = (y + 0.5f) / height;
                    var color = Color.Lerp(new Color(0.20f, 0.17f, 0.15f), new Color(0.09f, 0.08f, 0.07f), v);
                    var glow = Mathf.Exp(-Mathf.Pow((v - 0.78f) * 3.2f, 2f)) * 0.22f; // отсвет за иконкой
                    color = Color.Lerp(color, rarity, glow);
                    var edge = Mathf.Min(Mathf.Min(u, 1f - u) * width, Mathf.Min(v, 1f - v) * height); // расстояние до края, px
                    if (Mathf.Abs(edge - 6f) < 0.8f) color = Color.Lerp(color, rarity, 0.55f); // внутренняя рамка
                    var corner = Mathf.Min(Mathf.Min(x, width - 1 - x), 14) + Mathf.Min(Mathf.Min(y, height - 1 - y), 14);
                    if (corner < 7) color = Color.Lerp(color, rarity, 0.85f); // угловые метки
                    pixels[y * width + x] = color;
                }

            return pixels;
        }

        public static Texture2D ToTexture(Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        // ---- Формы ----

        private static float Emblem(ArtifactCategory category, Vector2 p) => category switch
        {
            ArtifactCategory.Amulet => Polygon(p, new[]
            {
                new Vector2(-0.78f, 0.78f), new Vector2(0.78f, 0.78f), new Vector2(0.78f, 0.02f), new Vector2(0f, -0.92f), new Vector2(-0.78f, 0.02f),
            }),
            ArtifactCategory.Talisman => Mathf.Min(Box(p, 0.58f), Box(Rotate(p, 45f), 0.58f)),
            _ => Mathf.Abs(p.magnitude - 0.78f) - 0.12f, // Руна: толстое кольцо, внутри — пустота под символ
        };

        private static float GlyphDistance(RelicGlyph glyph, Vector2 p)
        {
            switch (glyph)
            {
                case RelicGlyph.Block:
                    return Mathf.Min(Mathf.Abs(Box(p, 0.30f)) - 0.07f, Segment(p, new Vector2(-0.30f, 0f), new Vector2(0.30f, 0f)) - 0.05f);
                case RelicGlyph.Blades:
                    return Mathf.Min(Mathf.Min(Segment(p, new Vector2(-0.42f, -0.42f), new Vector2(0.42f, 0.42f)) - 0.07f, Segment(p, new Vector2(-0.42f, 0.42f), new Vector2(0.42f, -0.42f)) - 0.07f),
                        Mathf.Min(Segment(p, new Vector2(-0.30f, -0.12f), new Vector2(-0.12f, -0.30f)) - 0.05f, Segment(p, new Vector2(0.30f, -0.12f), new Vector2(0.12f, -0.30f)) - 0.05f));
                case RelicGlyph.Burst:
                {
                    var d = p.magnitude - 0.17f;
                    for (var i = 0; i < 4; i++)
                    {
                        var dir = new Vector2(Mathf.Cos(i * Mathf.PI / 4f), Mathf.Sin(i * Mathf.PI / 4f)) * 0.5f;
                        d = Mathf.Min(d, Segment(p, -dir, dir) - 0.045f);
                    }

                    return d;
                }
                case RelicGlyph.Flame:
                {
                    var outer = Mathf.Min(Circle(p - new Vector2(0f, -0.16f), 0.28f), Polygon(p, new[]
                    {
                        new Vector2(0.03f, 0.60f), new Vector2(0.12f, 0.34f), new Vector2(0.30f, 0.10f), new Vector2(0.27f, -0.12f),
                        new Vector2(-0.27f, -0.12f), new Vector2(-0.30f, 0.04f), new Vector2(-0.14f, 0.22f), new Vector2(-0.10f, 0.42f),
                    }));
                    return Mathf.Max(outer, -Circle(p - new Vector2(0.02f, -0.2f), 0.11f)); // ядро-вырез
                }
                case RelicGlyph.Jaws:
                    return Mathf.Min(
                        Polygon(p, new[] { new Vector2(-0.5f, 0.45f), new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.28f), new Vector2(0.25f, 0.0f), new Vector2(0f, 0.28f), new Vector2(-0.25f, 0.0f), new Vector2(-0.5f, 0.28f) }),
                        Polygon(p, new[] { new Vector2(-0.5f, -0.45f), new Vector2(0.5f, -0.45f), new Vector2(0.5f, -0.28f), new Vector2(0.25f, 0.0f), new Vector2(0f, -0.28f), new Vector2(-0.25f, 0.0f), new Vector2(-0.5f, -0.28f) }));
                case RelicGlyph.Eye:
                {
                    var lens = Mathf.Max(Circle(p - new Vector2(0f, 0.55f), 0.7f), Circle(p + new Vector2(0f, 0.55f), 0.7f));
                    return Mathf.Min(Mathf.Abs(lens) - 0.05f, Circle(p, 0.15f));
                }
                case RelicGlyph.Diamond:
                    return Polygon(p, new[] { new Vector2(0f, 0.52f), new Vector2(0.36f, 0f), new Vector2(0f, -0.52f), new Vector2(-0.36f, 0f) });
                case RelicGlyph.Cross:
                    return Mathf.Min(Segment(p, new Vector2(-0.34f, -0.34f), new Vector2(0.34f, 0.34f)) - 0.09f, Segment(p, new Vector2(-0.34f, 0.34f), new Vector2(0.34f, -0.34f)) - 0.09f);
                case RelicGlyph.Bolt:
                    return Polygon(p, new[] { new Vector2(0.12f, 0.56f), new Vector2(-0.28f, -0.04f), new Vector2(-0.02f, -0.04f), new Vector2(-0.14f, -0.56f), new Vector2(0.28f, 0.1f), new Vector2(0.02f, 0.1f) });
                case RelicGlyph.Wave:
                {
                    var d = 10f;
                    for (var row = -1; row <= 1; row++)
                        for (var i = 0; i < 10; i++)
                        {
                            var x0 = -0.45f + i * 0.1f;
                            var x1 = x0 + 0.1f;
                            d = Mathf.Min(d, Segment(p, new Vector2(x0, row * 0.22f + 0.07f * Mathf.Sin(x0 * 14f)), new Vector2(x1, row * 0.22f + 0.07f * Mathf.Sin(x1 * 14f))) - 0.035f);
                        }

                    return d;
                }
                case RelicGlyph.Gate:
                {
                    var arch = p.y > 0.05f ? Mathf.Abs(Vector2.Distance(p, new Vector2(0f, 0.05f)) - 0.34f) - 0.06f : 10f;
                    return Mathf.Min(arch, Mathf.Min(Segment(p, new Vector2(-0.34f, -0.45f), new Vector2(-0.34f, 0.05f)) - 0.06f, Segment(p, new Vector2(0.34f, -0.45f), new Vector2(0.34f, 0.05f)) - 0.06f));
                }
                case RelicGlyph.Moon:
                    return Mathf.Max(Circle(p, 0.44f), -Circle(p - new Vector2(0.2f, 0.1f), 0.38f));
                case RelicGlyph.Spindle:
                    return Mathf.Min(Polygon(p, new[] { new Vector2(0f, 0.58f), new Vector2(0.13f, 0f), new Vector2(0f, -0.58f), new Vector2(-0.13f, 0f) }), Mathf.Abs(Circle(p, 0.3f)) - 0.035f);
                case RelicGlyph.Dots:
                    return Mathf.Min(Circle(p - new Vector2(0f, 0.3f), 0.13f), Mathf.Min(Circle(p - new Vector2(-0.27f, -0.2f), 0.13f), Circle(p - new Vector2(0.27f, -0.2f), 0.13f)));
                default:
                    return Mathf.Min(Mathf.Abs(Circle(p, 0.4f)) - 0.06f, Circle(p, 0.14f));
            }
        }

        // ---- Примитивы (знаковое расстояние: < 0 внутри) ----

        private static float Circle(Vector2 p, float r) => p.magnitude - r;

        private static float RoundedBox(Vector2 p, float half, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - new Vector2(half - radius, half - radius);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        private static float Box(Vector2 p, float half) => Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half;

        private static Vector2 Rotate(Vector2 p, float degrees)
        {
            var a = degrees * Mathf.Deg2Rad;
            return new Vector2(p.x * Mathf.Cos(a) - p.y * Mathf.Sin(a), p.x * Mathf.Sin(a) + p.y * Mathf.Cos(a));
        }

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var pa = p - a;
            var ba = b - a;
            var h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        private static float Polygon(Vector2 p, Vector2[] v)
        {
            var d = Vector2.Dot(p - v[0], p - v[0]);
            var sign = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i];
                var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                var c0 = p.y >= v[i].y;
                var c1 = p.y < v[j].y;
                var c2 = e.x * w.y > e.y * w.x;
                if ((c0 && c1 && c2) || (!c0 && !c1 && !c2)) sign = -sign;
            }

            return sign * Mathf.Sqrt(d);
        }

        private static float Coverage(float signedDistance, float aa) => Mathf.Clamp01(0.5f - signedDistance / aa);

        private static Color Over(Color under, Color over, float coverage)
        {
            var a = coverage * over.a;
            if (a <= 0f) return under;
            var outA = a + under.a * (1f - a);
            var rgb = (new Vector3(over.r, over.g, over.b) * a + new Vector3(under.r, under.g, under.b) * under.a * (1f - a)) / outA;
            return new Color(rgb.x, rgb.y, rgb.z, outA);
        }
    }
}
