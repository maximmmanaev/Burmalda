using System.Linq;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class ArtifactCatalogTests
    {
        [Test]
        public void Amulets_HasAtLeastTheTwoIssue17Examples()
        {
            Assert.GreaterOrEqual(ArtifactCatalog.Amulets.Count, 2);
        }

        [Test]
        public void Talismans_HasAtLeastTheThreeIssue17Examples()
        {
            Assert.GreaterOrEqual(ArtifactCatalog.Talismans.Count, 3);
        }

        [Test]
        public void AllIds_AreUnique()
        {
            var ids = ArtifactCatalog.Amulets.Select(a => a.Id)
                .Concat(ArtifactCatalog.Talismans.Select(t => t.Id))
                .ToList();

            Assert.AreEqual(ids.Distinct().Count(), ids.Count);
        }

        [Test]
        public void AllEntries_HaveAtLeastOneTag()
        {
            foreach (var amulet in ArtifactCatalog.Amulets)
                Assert.IsNotEmpty(amulet.Tags, $"{amulet.Id} без тега");
            foreach (var talisman in ArtifactCatalog.Talismans)
                Assert.IsNotEmpty(talisman.Tags, $"{talisman.Id} без тега");
        }
    }
}
