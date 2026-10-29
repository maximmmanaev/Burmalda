using System.Linq;
using Burmalda.Artifacts;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.DebugVisuals.Tests
{
    public class CampArtPainterTests
    {
        private static float Coverage(Color32[] px) => px.Count(c => c.a > 128) / (float)px.Length;

        [Test]
        public void Sarcophagus_HasShape_TransparentCorners_AndGlowChangesPicture()
        {
            var dark = CampArtPainter.PaintSarcophagus(new Color(0.5f, 0.45f, 0.4f, 0f));
            var gold = CampArtPainter.PaintSarcophagus(new Color(1f, 0.8f, 0.2f, 1f));

            Assert.AreEqual(0, dark[0].a);
            Assert.That(Coverage(dark), Is.InRange(0.2f, 0.7f));
            CollectionAssert.AreNotEqual(dark, gold);
        }

        [TestCase(CollectibleKind.Idol)]
        [TestCase(CollectibleKind.Totem)]
        public void Silhouette_EachLitPiece_ChangesThePicture_AndIsDeterministic(CollectibleKind kind)
        {
            var color = AltarPickText.RareColor;
            var none = CampArtPainter.PaintSilhouette(kind, 0, 4, color, 0f);
            var one = CampArtPainter.PaintSilhouette(kind, 1, 4, color, 0f);
            var two = CampArtPainter.PaintSilhouette(kind, 2, 4, color, 0f);

            Assert.That(Coverage(none), Is.InRange(0.15f, 0.8f));
            CollectionAssert.AreNotEqual(none, one);
            CollectionAssert.AreNotEqual(one, two);
            CollectionAssert.AreEqual(one, CampArtPainter.PaintSilhouette(kind, 1, 4, color, 0f));
        }

        [Test]
        public void Silhouette_PityGlow_MakesDarkPartBrighter()
        {
            var calm = CampArtPainter.PaintSilhouette(CollectibleKind.Idol, 0, 4, AltarPickText.RareColor, 0f);
            var glowing = CampArtPainter.PaintSilhouette(CollectibleKind.Idol, 0, 4, AltarPickText.RareColor, 1f);

            Assert.Greater(glowing.Where(c => c.a > 200).Average(c => c.r + c.g + c.b), calm.Where(c => c.a > 200).Average(c => c.r + c.g + c.b));
        }

        [Test]
        public void RarityGlow_IsBlueEpicPurpleGold()
        {
            Assert.AreEqual(AltarPickText.RareColor, CampArtPainter.RarityGlow(RelicRarity.Rare));
            Assert.AreEqual(AltarPickText.EpicColor, CampArtPainter.RarityGlow(RelicRarity.Epic));
            Assert.AreEqual(AltarPickText.LegendaryColor, CampArtPainter.RarityGlow(RelicRarity.Legendary));
        }

        [Test]
        public void Beat_IsAudibleClip_LouderLongerWithStrength()
        {
            var soft = ProceduralSfx.Beat(0.35f);
            var strong = ProceduralSfx.Beat(1f);

            Assert.Greater(soft.length, 0.1f);
            Assert.Greater(strong.length, soft.length);
        }
    }
}
