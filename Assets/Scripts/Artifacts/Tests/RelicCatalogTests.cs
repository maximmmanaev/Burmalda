using System.Linq;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class RelicCatalogTests
    {
        [Test]
        public void Catalog_Has30Relics_UniqueIds()
        {
            Assert.AreEqual(30, RelicCatalog.All.Count);
            Assert.AreEqual(30, RelicCatalog.All.Select(r => r.Id).Distinct().Count());
        }

        [Test]
        public void EveryGod_HasOneAmuletTalismanRune_AndOneOfEachRarity()
        {
            foreach (var god in System.Enum.GetValues(typeof(God)).Cast<God>())
            {
                var relics = RelicCatalog.All.Where(r => r.God == god).ToList();
                Assert.AreEqual(3, relics.Count, god.ToString());
                CollectionAssert.AreEquivalent(
                    new[] { ArtifactCategory.Amulet, ArtifactCategory.Talisman, ArtifactCategory.Rune },
                    relics.Select(r => r.Category).ToList(), god.ToString());
                CollectionAssert.AreEquivalent(
                    new[] { RelicRarity.Rare, RelicRarity.Epic, RelicRarity.Legendary },
                    relics.Select(r => r.Rarity).ToList(), god.ToString());
            }
        }

        [Test]
        public void MythologyPools_Are12Slavic_9Sumerian_9Ojibwe()
        {
            Assert.AreEqual(12, RelicCatalog.All.Count(r => r.Mythology == Mythology.Slavic));
            Assert.AreEqual(9, RelicCatalog.All.Count(r => r.Mythology == Mythology.Sumerian));
            Assert.AreEqual(9, RelicCatalog.All.Count(r => r.Mythology == Mythology.Ojibwe));
        }

        [TestCase(1, God.Leshy)]
        [TestCase(3, God.Perun)]
        [TestCase(4, God.Humbaba)]
        [TestCase(7, God.Memegwesi)]
        [TestCase(10, God.Chernobog)]
        public void GodOfYarus_FollowsWorldTable(int yarus, God god)
        {
            Assert.AreEqual(god, RelicCatalog.GodOfYarus(yarus));
        }

        [Test]
        public void StarterRelics_AreThreeRare_AmuletTalismanRune()
        {
            var starters = RelicCatalog.StarterIds.Select(RelicCatalog.Get).ToList();
            Assert.AreEqual(3, starters.Count);
            Assert.IsTrue(starters.All(r => r.Rarity == RelicRarity.Rare));
            CollectionAssert.AreEquivalent(
                new[] { ArtifactCategory.Amulet, ArtifactCategory.Talisman, ArtifactCategory.Rune },
                starters.Select(r => r.Category).ToList());
            Assert.AreEqual(God.Leshy, starters.Single(r => r.Category == ArtifactCategory.Amulet).God);
            Assert.AreEqual(God.Mokosh, starters.Single(r => r.Category == ArtifactCategory.Talisman).God);
            Assert.AreEqual(God.Perun, starters.Single(r => r.Category == ArtifactCategory.Rune).God);
        }

        [Test]
        public void OnlyAmulets_ProtectFromThreats_AndEveryAmuletDoes()
        {
            foreach (var r in RelicCatalog.All)
                Assert.AreEqual(r.Category == ArtifactCategory.Amulet, r.Protects.Count > 0, r.Id);
        }

        [Test]
        public void ThirdOptionTalisman_IsEreshkigalRare()
        {
            var t = RelicCatalog.Get(RelicCatalog.ThirdOptionTalismanId);
            Assert.AreEqual(God.Ereshkigal, t.God);
            Assert.AreEqual(ArtifactCategory.Talisman, t.Category);
            Assert.AreEqual(RelicRarity.Rare, t.Rarity);
        }

        [Test]
        public void NoKeysAnywhereInRelicDescriptions()
        {
            Assert.IsFalse(RelicCatalog.All.Any(r => r.Effect.Contains("Ключ ") || r.Effect.Contains("Ключей") || r.Effect.EndsWith("Ключ")));
        }
    }
}
