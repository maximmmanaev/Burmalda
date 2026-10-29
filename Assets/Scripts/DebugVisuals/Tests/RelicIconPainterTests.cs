using System.Linq;
using Burmalda.Artifacts;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.DebugVisuals.Tests
{
    public class RelicIconPainterTests
    {
        private static float Coverage(Color32[] px) => px.Count(c => c.a > 128) / (float)px.Length;

        [Test]
        public void EveryRelic_HasIcon_WithShapeAndTransparentCorners()
        {
            foreach (var relic in RelicCatalog.All)
            {
                var px = RelicIconPainter.Paint(relic);
                Assert.AreEqual(RelicIconPainter.IconSize * RelicIconPainter.IconSize, px.Length, relic.Id);
                Assert.AreEqual(0, px[0].a, $"{relic.Id}: угол прозрачен");
                Assert.AreEqual(0, px[px.Length - 1].a, $"{relic.Id}: угол прозрачен");
                var cover = Coverage(px);
                Assert.That(cover, Is.InRange(0.12f, 0.85f), $"{relic.Id}: иконка не пустая и не залита целиком ({cover:P0})");
            }
        }

        [Test]
        public void EveryRelic_HasDistinctPicture()
        {
            var signatures = RelicCatalog.All.Select(r => string.Join(",", RelicIconPainter.Paint(r).Where((_, i) => i % 7 == 0).Select(c => c.r ^ c.g ^ c.b ^ c.a))).Distinct().Count();
            Assert.AreEqual(30, signatures);
        }

        [Test]
        public void Painting_IsDeterministic()
        {
            var relic = RelicCatalog.Get("relic-perun-amulet");
            CollectionAssert.AreEqual(RelicIconPainter.Paint(relic), RelicIconPainter.Paint(relic));
        }

        [Test]
        public void EveryGlyph_IsUsedByAtLeastOneRelic_AndGodsHaveDistinctColors()
        {
            var used = RelicCatalog.All.Select(RelicIconPainter.GlyphOf).Distinct().Count();
            Assert.AreEqual(System.Enum.GetValues(typeof(RelicGlyph)).Length, used);
            var colors = System.Enum.GetValues(typeof(God)).Cast<God>().Select(RelicIconPainter.GodColor).Distinct().Count();
            Assert.AreEqual(10, colors);
        }

        [Test]
        public void AmuletThreatGlyphs_MatchProtection()
        {
            Assert.AreEqual(RelicGlyph.Flame, RelicIconPainter.GlyphOf(RelicCatalog.Get("relic-perun-amulet")));
            Assert.AreEqual(RelicGlyph.Blades, RelicIconPainter.GlyphOf(RelicCatalog.Get("relic-ereshkigal-amulet")));
            Assert.AreEqual(RelicGlyph.Jaws, RelicIconPainter.GlyphOf(RelicCatalog.Get("relic-chernobog-amulet")));
        }

        [Test]
        public void CardBackground_IsOpaque_AndTintedByRarity()
        {
            var rare = RelicIconPainter.PaintCardBackground(AltarPickText.RareColor);
            var legendary = RelicIconPainter.PaintCardBackground(AltarPickText.LegendaryColor);
            Assert.IsTrue(rare.All(c => c.a == 255));
            CollectionAssert.AreNotEqual(rare, legendary);
        }
    }
}
