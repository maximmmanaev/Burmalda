using System.Linq;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class GodTableTests
    {
        [Test]
        public void Table_Has10Gods_OnePerYarus_AllGodsDistinct()
        {
            Assert.AreEqual(10, GodTable.All.Count);
            CollectionAssert.AreEqual(Enumerable.Range(1, 10).ToList(), GodTable.All.Select(g => g.Yarus).ToList());
            Assert.AreEqual(10, GodTable.All.Select(g => g.God).Distinct().Count());
        }

        [TestCase(1, Mythology.Slavic, GodRank.Junior, God.Leshy)]
        [TestCase(2, Mythology.Slavic, GodRank.Middle, God.Mokosh)]
        [TestCase(3, Mythology.Slavic, GodRank.Senior, God.Perun)]
        [TestCase(4, Mythology.Sumerian, GodRank.Junior, God.Humbaba)]
        [TestCase(5, Mythology.Sumerian, GodRank.Middle, God.Ereshkigal)]
        [TestCase(6, Mythology.Sumerian, GodRank.Senior, God.Enlil)]
        [TestCase(7, Mythology.Ojibwe, GodRank.Junior, God.Memegwesi)]
        [TestCase(8, Mythology.Ojibwe, GodRank.Middle, God.Mishibijiu)]
        [TestCase(9, Mythology.Ojibwe, GodRank.Senior, God.Animikii)]
        [TestCase(10, Mythology.Slavic, GodRank.Senior, God.Chernobog)]
        public void Row_MatchesPrdSection52(int yarus, Mythology mythology, GodRank rank, God god)
        {
            var row = GodTable.ForYarus(yarus);
            Assert.AreEqual(mythology, row.Mythology);
            Assert.AreEqual(rank, row.Rank);
            Assert.AreEqual(god, row.God);
        }

        [Test]
        public void OnlyYarus1_HasOwnLayout_OthersArePlaceholders()
        {
            Assert.IsTrue(GodTable.ForYarus(1).HasOwnLayout);
            Assert.IsTrue(GodTable.All.Skip(1).All(g => !g.HasOwnLayout), "раскладки Ярусов 2–10 — заглушка (Леший)");
        }

        [Test]
        public void RelicMythologyPools_AreDerivedFromTable()
        {
            foreach (var row in GodTable.All)
            {
                Assert.AreEqual(row.Mythology, RelicCatalog.MythologyOf(row.God));
                Assert.AreEqual(row.God, RelicCatalog.GodOfYarus(row.Yarus));
            }
        }

        [Test]
        public void OutOfRange_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => GodTable.ForYarus(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => GodTable.ForYarus(11));
        }
    }
}
