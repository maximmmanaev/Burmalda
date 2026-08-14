using System;
using Burmalda.Artifacts;
using NUnit.Framework;

namespace Burmalda.Altar.Tests
{
    public class ChestTests
    {
        [Test]
        public void RuneChest_ExposesTypeCostAndContent()
        {
            var rune = new Rune("r1", "Руна Силы", "сила");
            var chest = new RuneChest(50, rune);

            Assert.AreEqual(ChestType.Rune, chest.Type);
            Assert.AreEqual(50, chest.Cost);
            Assert.AreSame(rune, chest.Content);
        }

        [Test]
        public void TalismanChest_ExposesTypeCostAndContent()
        {
            var talisman = new Talisman("t1", "Т", "э", new[] { ArtifactTag.Mana });
            var chest = new TalismanChest(80, talisman);

            Assert.AreEqual(ChestType.Talisman, chest.Type);
            Assert.AreSame(talisman, chest.Content);
        }

        [Test]
        public void AmuletChest_ExposesTypeCostAndContent()
        {
            var amulet = new Amulet("a1", "А", "э", new[] { ArtifactTag.Defense });
            var chest = new AmuletChest(80, amulet);

            Assert.AreEqual(ChestType.Amulet, chest.Type);
            Assert.AreSame(amulet, chest.Content);
        }

        [Test]
        public void RelicChest_ExposesTypeCostAndContent()
        {
            var relic = new Relic("rel1", "Р");
            var chest = new RelicChest(500, relic);

            Assert.AreEqual(ChestType.Relic, chest.Type);
            Assert.AreSame(relic, chest.Content);
        }

        [Test]
        public void RuneChest_NullContent_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RuneChest(50, null));
        }
    }
}
