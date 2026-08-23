using System.Collections.Generic;
using Burmalda.Artifacts;
using NUnit.Framework;

namespace Burmalda.DebugVisuals.Tests
{
    public class RunHudFormattingTests
    {
        [Test]
        public void FormatArtifactIds_NoMatches_ReturnsDash()
        {
            var acquired = new List<Artifact>
            {
                new Amulet("amulet-a", "A", "desc", new[] { ArtifactTag.Defense }),
            };

            var result = RunHudFormatting.FormatArtifactIds(acquired, ArtifactCategory.Talisman);

            Assert.AreEqual("—", result);
        }

        [Test]
        public void FormatArtifactIds_SingleMatch_ReturnsItsId()
        {
            var acquired = new List<Artifact>
            {
                new Amulet("amulet-trap-immunity", "Иммунитет", "desc", new[] { ArtifactTag.Defense }),
            };

            var result = RunHudFormatting.FormatArtifactIds(acquired, ArtifactCategory.Amulet);

            Assert.AreEqual("amulet-trap-immunity", result);
        }

        [Test]
        public void FormatArtifactIds_MultipleMatches_JoinedInAcquisitionOrder()
        {
            var acquired = new List<Artifact>
            {
                new Talisman("talisman-double-mana", "A", "desc", new[] { ArtifactTag.Mana }),
                new Talisman("talisman-double-keys", "B", "desc", new[] { ArtifactTag.Keys }),
            };

            var result = RunHudFormatting.FormatArtifactIds(acquired, ArtifactCategory.Talisman);

            Assert.AreEqual("talisman-double-mana, talisman-double-keys", result);
        }

        [Test]
        public void FormatArtifactIds_IgnoresOtherCategories()
        {
            var acquired = new List<Artifact>
            {
                new Amulet("amulet-a", "A", "desc", new[] { ArtifactTag.Defense }),
                new Talisman("talisman-b", "B", "desc", new[] { ArtifactTag.Mana }),
                new Amulet("amulet-c", "C", "desc", new[] { ArtifactTag.Defense }),
            };

            var result = RunHudFormatting.FormatArtifactIds(acquired, ArtifactCategory.Amulet);

            Assert.AreEqual("amulet-a, amulet-c", result);
        }

        [Test]
        public void FormatArtifactIds_EmptyList_ReturnsDash()
        {
            var result = RunHudFormatting.FormatArtifactIds(new List<Artifact>(), ArtifactCategory.Amulet);

            Assert.AreEqual("—", result);
        }
    }
}
