using NUnit.Framework;

namespace Burmalda.Boss.Tests
{
    public class FirstBossVictoryTrackerTests
    {
        [Test]
        public void NewTracker_HasNotWonBefore()
        {
            var tracker = new FirstBossVictoryTracker();

            Assert.IsFalse(tracker.HasWonBefore);
        }

        [Test]
        public void RecordVictory_SetsHasWonBeforeAndFiresFirstVictory()
        {
            var tracker = new FirstBossVictoryTracker();
            var fireCount = 0;
            tracker.FirstVictory += () => fireCount++;

            tracker.RecordVictory();

            Assert.IsTrue(tracker.HasWonBefore);
            Assert.AreEqual(1, fireCount);
        }

        [Test]
        public void RecordVictory_CalledTwice_FiresFirstVictoryOnlyOnce()
        {
            var tracker = new FirstBossVictoryTracker();
            var fireCount = 0;
            tracker.FirstVictory += () => fireCount++;

            tracker.RecordVictory();
            tracker.RecordVictory();

            Assert.AreEqual(1, fireCount);
        }
    }
}
