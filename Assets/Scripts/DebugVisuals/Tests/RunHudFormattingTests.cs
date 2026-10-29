using System.Collections.Generic;
using Burmalda.Artifacts;
using NUnit.Framework;

namespace Burmalda.DebugVisuals.Tests
{
    public class RunHudFormattingTests
    {
        private static RelicDefinition Of(string id) => RelicCatalog.Get(id);

        [Test]
        public void FormatRelicNames_NoMatches_ReturnsDash()
        {
            var items = new List<RelicDefinition> { Of("relic-leshy-amulet") };

            Assert.AreEqual("—", RunHudFormatting.FormatRelicNames(items, ArtifactCategory.Talisman));
        }

        [Test]
        public void FormatRelicNames_SingleMatch_ReturnsName()
        {
            var items = new List<RelicDefinition> { Of("relic-leshy-amulet") };

            Assert.AreEqual("Берестяной оберег", RunHudFormatting.FormatRelicNames(items, ArtifactCategory.Amulet));
        }

        [Test]
        public void FormatRelicNames_MultipleMatches_InAcquisitionOrder_FiltersByCategory()
        {
            var items = new List<RelicDefinition> { Of("relic-leshy-amulet"), Of("relic-mokosh-talisman"), Of("relic-perun-amulet") };

            Assert.AreEqual("Берестяной оберег, Перунов знак", RunHudFormatting.FormatRelicNames(items, ArtifactCategory.Amulet));
        }
    }
}
