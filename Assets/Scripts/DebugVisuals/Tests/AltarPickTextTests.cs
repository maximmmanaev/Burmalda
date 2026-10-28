using Burmalda.Artifacts;
using NUnit.Framework;

namespace Burmalda.DebugVisuals.Tests
{
    public class AltarPickTextTests
    {
        [Test]
        public void RarityColors_AreDistinct()
        {
            Assert.AreNotEqual(AltarPickText.RarityColor(RelicRarity.Rare), AltarPickText.RarityColor(RelicRarity.Epic));
            Assert.AreNotEqual(AltarPickText.RarityColor(RelicRarity.Epic), AltarPickText.RarityColor(RelicRarity.Legendary));
        }

        [Test]
        public void Title_SaysTwoOrThree()
        {
            StringAssert.Contains("двух", AltarPickText.Title(2));
            StringAssert.Contains("трёх", AltarPickText.Title(3));
        }

        [Test]
        public void VaultMarker_VisibleOnlyWhenBonusPending()
        {
            Assert.AreEqual("", AltarPickText.VaultMarker(0));
            StringAssert.Contains("+1", AltarPickText.VaultMarker(1));
        }
    }
}
