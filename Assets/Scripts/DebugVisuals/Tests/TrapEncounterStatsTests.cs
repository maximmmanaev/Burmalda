using NUnit.Framework;

namespace Burmalda.DebugVisuals.Tests
{
    public class TrapEncounterStatsTests
    {
        [TearDown]
        public void ResetAfterTest() => TrapEncounterStats.Reset();

        [Test]
        public void Reset_ZeroesBothCounters()
        {
            TrapEncounterStats.RecordTrapTrigger();
            TrapEncounterStats.RecordRow(5);

            TrapEncounterStats.Reset();

            Assert.AreEqual(0, TrapEncounterStats.TrapsTriggered);
            Assert.AreEqual(0, TrapEncounterStats.RowsTraversed);
        }

        [Test]
        public void RecordTrapTrigger_IncrementsCounter()
        {
            TrapEncounterStats.Reset();

            TrapEncounterStats.RecordTrapTrigger();
            TrapEncounterStats.RecordTrapTrigger();

            Assert.AreEqual(2, TrapEncounterStats.TrapsTriggered);
        }

        [Test]
        public void RecordRow_TracksFurthestRowReached_IgnoresBackwardMoves()
        {
            // Knockback/распад могут откатить CurrentPosition назад —
            // RowsTraversed это знаменатель "как далеко реально продвинулся
            // забег", не текущая позиция, откат назад не должен его уменьшать.
            TrapEncounterStats.Reset();

            TrapEncounterStats.RecordRow(3);
            TrapEncounterStats.RecordRow(7);
            TrapEncounterStats.RecordRow(2); // откат назад (напр. Knockback к Алтарю)

            Assert.AreEqual(7, TrapEncounterStats.RowsTraversed);
        }
    }
}
