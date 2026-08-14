using NUnit.Framework;

namespace Burmalda.Progression.Tests
{
    public class DepthRecordTests
    {
        [Test]
        public void NewRecord_BestTierIsZero()
        {
            var record = new DepthRecord();

            Assert.AreEqual(0, record.BestTier);
        }

        [Test]
        public void ReportTier_HigherThanCurrentBest_UpdatesBestTier()
        {
            var record = new DepthRecord();

            record.ReportTier(2);

            Assert.AreEqual(2, record.BestTier);
        }

        [Test]
        public void ReportTier_LowerOrEqualToCurrentBest_DoesNotDecreaseBestTier()
        {
            var record = new DepthRecord();
            record.ReportTier(3);

            record.ReportTier(1);

            Assert.AreEqual(3, record.BestTier);
        }

        [Test]
        public void ReportTier_NewBest_RaisesRecordBroken()
        {
            var record = new DepthRecord();
            var seen = -1;
            record.RecordBroken += tier => seen = tier;

            record.ReportTier(2);

            Assert.AreEqual(2, seen);
        }

        [Test]
        public void ReportTier_NotBeatingRecord_DoesNotRaiseRecordBroken()
        {
            var record = new DepthRecord();
            record.ReportTier(3);
            var fired = false;
            record.RecordBroken += _ => fired = true;

            record.ReportTier(2);

            Assert.IsFalse(fired);
        }
    }
}
