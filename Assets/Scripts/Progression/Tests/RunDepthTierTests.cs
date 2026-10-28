using NUnit.Framework;

namespace Burmalda.Progression.Tests
{
    public class RunDepthTierTests
    {
        [Test]
        public void NewTier_CurrentTierIsZero()
        {
            var tier = new RunDepthTier();

            Assert.AreEqual(0, tier.CurrentTier);
        }

        [Test]
        public void RecordSanctuaryCleared_IncreasesCurrentTierByOne()
        {
            var tier = new RunDepthTier();

            tier.RecordSanctuaryCleared();

            Assert.AreEqual(1, tier.CurrentTier);
        }

        [Test]
        public void RecordSanctuaryCleared_MultipleTimes_AccumulatesTier()
        {
            var tier = new RunDepthTier();

            tier.RecordSanctuaryCleared();
            tier.RecordSanctuaryCleared();
            tier.RecordSanctuaryCleared();

            Assert.AreEqual(3, tier.CurrentTier);
        }

        [Test]
        public void RecordSanctuaryCleared_RaisesAdvancedWithNewTier()
        {
            var tier = new RunDepthTier();
            var seen = -1;
            tier.Advanced += t => seen = t;

            tier.RecordSanctuaryCleared();

            Assert.AreEqual(1, seen);
        }
    }
}
