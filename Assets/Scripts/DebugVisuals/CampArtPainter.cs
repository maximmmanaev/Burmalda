using System.Collections.Generic;
using Burmalda.Artifacts;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Рисует кодом картинки экрана открытия Саркофагов и постаментов (PRD v10 §22.2–22.3): Саркофаг, силуэты Идола и
    /// Тотема с зажигающимися кусками (<c>1/4</c>), радиальное свечение редкости. Временный арт до финальных иллюстраций.
    /// </summary>
    public static class CampArtPainter
    {
        public const int Size = 192;

        public static Color RarityGlow(RelicRarity rarity) => AltarPickText.RarityColor(rarity); // синий / фиолетовый / золотой

        // ---- Саркофаг ----

        /// <summary>Саркофаг: каменный гроб с крестом-знаком. <paramref name="glow"/> — цвет свечения (a = сила) из трещины и знака.</summary>
        public static Color32[] PaintSarcophagus(Color glow, int size = Size)
        {
            var pixels = new Color32[size * size];
            var aa = 2f / size;
            var shape = new[] { new Vector2(0f, 0.94f), new Vector2(0.44f, 0.56f), new Vector2(0.34f, -0.92f), new Vector2(-0.34f, -0.92f), new Vector2(-0.44f, 0.56f) };
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                    var d = Polygon(p, shape);
                    var c = new Color(0, 0, 0, 0);
                    c = Over(c, Color.Lerp(new Color(0.20f, 0.18f, 0.16f), new Color(0.12f, 0.11f, 0.10f), (p.y + 1f) / 2f), Cov(d, aa));
                    c = Over(c, new Color(0.42f, 0.38f, 0.33f), Cov(Mathf.Abs(d + 0.04f) - 0.025f, aa));
                    c = Over(c, new Color(0.30f, 0.27f, 0.24f), Cov(Mathf.Abs(d + 0.17f) - 0.012f, aa));
                    // знак: крест
                    var cross = Mathf.Min(Seg(p, new Vector2(0f, 0.38f), new Vector2(0f, -0.38f)) - 0.05f, Seg(p, new Vector2(-0.2f, 0.12f), new Vector2(0.2f, 0.12f)) - 0.05f);
                    c = Over(c, Color.Lerp(new Color(0.40f, 0.36f, 0.31f), new Color(glow.r, glow.g, glow.b, 1f), glow.a), Cov(cross, aa));
                    pixels[y * size + x] = c;
                }

            return pixels;
        }

        // ---- Силуэты предметов ----

        /// <summary>
        /// Силуэт Идола (статуэтка) или Тотема (столб) с <paramref name="total"/> кусками по кругу; зажжено <paramref name="lit"/>
        /// (цвет редкости). Тёмная часть горит ярче при накопленной защите (<paramref name="pityGlow"/> 0..1, [ГД]).
        /// </summary>
        public static Color32[] PaintSilhouette(CollectibleKind kind, int lit, int total, Color rarity, float pityGlow, int size = Size)
        {
            var pixels = new Color32[size * size];
            var aa = 2f / size;
            var dark = Color.Lerp(new Color(0.11f, 0.11f, 0.13f), new Color(0.38f, 0.38f, 0.46f), Mathf.Clamp01(pityGlow));
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                    var d = kind == CollectibleKind.Idol ? IdolShape(p) : TotemShape(p);
                    var c = new Color(0, 0, 0, 0);
                    var color = dark;
                    if (total > 0)
                    {
                        var turn = Mathf.Repeat(Mathf.Atan2(p.x, p.y), Mathf.PI * 2f) / (Mathf.PI * 2f); // от верха по часовой
                        var slice = Mathf.Min((int)(turn * total), total - 1);
                        var edge = Mathf.Abs(turn * total - Mathf.Round(turn * total));
                        if (slice < lit) color = Color.Lerp(rarity, Color.white, 0.12f);
                        if (edge < 0.012f && p.magnitude > 0.02f) color = Color.Lerp(color, new Color(0.04f, 0.04f, 0.05f), 0.85f); // щели между кусками
                    }

                    c = Over(c, color, Cov(d, aa));
                    c = Over(c, Color.Lerp(color, Color.white, 0.25f), Cov(Mathf.Abs(d + 0.025f) - 0.015f, aa) * 0.6f);
                    pixels[y * size + x] = c;
                }

            return pixels;
        }

        private static float IdolShape(Vector2 p) =>
            Mathf.Min(Mathf.Min(Circle(p - new Vector2(0f, 0.56f), 0.27f),
                Polygon(p, new[] { new Vector2(-0.34f, 0.22f), new Vector2(0.34f, 0.22f), new Vector2(0.56f, -0.62f), new Vector2(-0.56f, -0.62f) })),
                RoundRect(p - new Vector2(0f, -0.76f), 0.66f, 0.13f, 0.05f));

        private static float TotemShape(Vector2 p) =>
            Mathf.Min(Mathf.Min(RoundRect(p - new Vector2(0f, 0.64f), 0.34f, 0.2f, 0.07f), RoundRect(p - new Vector2(0f, 0.14f), 0.44f, 0.2f, 0.07f)),
                Mathf.Min(RoundRect(p - new Vector2(0f, -0.36f), 0.52f, 0.2f, 0.07f), RoundRect(p - new Vector2(0f, -0.8f), 0.66f, 0.1f, 0.04f)));

        // ---- Свечение ----

        /// <summary>Радиальное свечение (мягкий круг) — белое, красится цветом редкости в Image.color.</summary>
        public static Color32[] PaintGlow(int size = 128)
        {
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var r = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f).magnitude;
                    var a = Mathf.Clamp01(1f - r);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
                }

            return pixels;
        }

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Texture(string key, System.Func<Color32[]> paint, int size = Size)
        {
            if (Cache.TryGetValue(key, out var t) && t != null) return t;
            return Cache[key] = RelicIconPainter.ToTexture(paint(), size, size);
        }

        public static Texture2D SarcophagusTexture(Color glow) =>
            Texture($"sarc-{glow.r:F2}-{glow.g:F2}-{glow.b:F2}-{glow.a:F2}", () => PaintSarcophagus(glow));

        public static Texture2D SilhouetteTexture(CollectibleKind kind, int lit, int total, RelicRarity rarity, float pityGlow) =>
            Texture($"sil-{kind}-{lit}-{total}-{rarity}-{Mathf.RoundToInt(pityGlow * 10)}", () => PaintSilhouette(kind, lit, total, RarityGlow(rarity), pityGlow));

        public static Texture2D GlowTexture() => Texture("glow", () => PaintGlow(128), 128);

        // ---- Примитивы ----

        private static float Circle(Vector2 p, float r) => p.magnitude - r;

        private static float RoundRect(Vector2 p, float halfW, float halfH, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x) - (halfW - r), Mathf.Abs(p.y) - (halfH - r));
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        private static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            var pa = p - a;
            var ba = b - a;
            return (pa - ba * Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba))).magnitude;
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

        private static float Cov(float signedDistance, float aa) => Mathf.Clamp01(0.5f - signedDistance / aa);

        private static Color Over(Color under, Color over, float coverage)
        {
            var a = coverage * over.a;
            if (a <= 0f) return under;
            var outA = a + under.a * (1f - a);
            var rgb = (new Vector3(over.r, over.g, over.b) * a + new Vector3(under.r, under.g, under.b) * under.a * (1f - a)) / outA;
            return new Color(rgb.x, rgb.y, rgb.z, outA);
        }
    }

    /// <summary>Звук «удара» открытия, синтезируется кодом (без файлов): падающий тон с шумом. Сила 0..1 — громче и ниже.</summary>
    public static class ProceduralSfx
    {
        private static readonly Dictionary<int, AudioClip> Cache = new Dictionary<int, AudioClip>();

        public static AudioClip Beat(float strength)
        {
            var key = Mathf.RoundToInt(Mathf.Clamp01(strength) * 4f);
            if (Cache.TryGetValue(key, out var clip) && clip != null) return clip;
            const int rate = 22050;
            var seconds = 0.25f + key * 0.12f;
            var samples = new float[(int)(rate * seconds)];
            var rng = new System.Random(key + 7);
            var f = 330f - key * 45f;
            var phase = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)samples.Length;
                phase += 2f * Mathf.PI * Mathf.Lerp(f, f * 0.4f, t) / rate;
                var env = Mathf.Exp(-5f * t);
                var noise = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-30f * t) * 0.5f;
                samples[i] = (Mathf.Sin(phase) * 0.7f + noise) * env * (0.35f + 0.1f * key);
            }

            clip = AudioClip.Create("beat" + key, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return Cache[key] = clip;
        }
    }
}
