using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class RunRelicBuildTests
    {
        private static RelicDefinition Of(string id) => RelicCatalog.Get(id);

        [Test]
        public void SlotsFollowPrd_2Amulets_2Talismans_RuneSlotsConfig()
        {
            Assert.AreEqual(2, RunRelicBuild.SlotsFor(ArtifactCategory.Amulet));
            Assert.AreEqual(2, RunRelicBuild.SlotsFor(ArtifactCategory.Talisman));
            Assert.AreEqual(2, RunRelicBuild.SlotsFor(ArtifactCategory.Rune), "старт 2 (§13.4)");
        }

        [Test]
        public void ThirdAmulet_DoesNotFit_ButReplacementWorks()
        {
            var build = new RunRelicBuild();
            Assert.IsTrue(build.TryAdd(Of("relic-leshy-amulet")));
            Assert.IsTrue(build.TryAdd(Of("relic-perun-amulet")));
            Assert.IsFalse(build.HasFreeSlot(ArtifactCategory.Amulet));
            Assert.IsFalse(build.TryAdd(Of("relic-humbaba-amulet")), "пятый предмет — только вытеснением");

            Assert.IsTrue(build.TryReplace("relic-leshy-amulet", Of("relic-humbaba-amulet")));
            Assert.IsTrue(build.Contains("relic-humbaba-amulet"));
            Assert.IsFalse(build.Contains("relic-leshy-amulet"));
        }

        [Test]
        public void Duplicate_IsRejected()
        {
            var build = new RunRelicBuild();
            Assert.IsTrue(build.TryAdd(Of("relic-leshy-amulet")));
            Assert.IsFalse(build.TryAdd(Of("relic-leshy-amulet")));
        }

        [Test]
        public void Replace_WithOtherCategoryOrUnknownDrop_IsRejected()
        {
            var build = new RunRelicBuild();
            build.TryAdd(Of("relic-leshy-amulet"));
            Assert.IsFalse(build.TryReplace("relic-leshy-amulet", Of("relic-mokosh-talisman")));
            Assert.IsFalse(build.TryReplace("relic-nope", Of("relic-perun-amulet")));
        }

        [Test]
        public void ProtectsAgainstAny_ChecksAmuletThreats()
        {
            var build = new RunRelicBuild();
            build.TryAdd(Of("relic-leshy-amulet")); // Blunt
            Assert.IsTrue(build.ProtectsAgainstAny(new[] { ThreatType.Blunt, ThreatType.Fire }));
            Assert.IsFalse(build.ProtectsAgainstAny(new[] { ThreatType.Slashing }));
        }
    }
}
