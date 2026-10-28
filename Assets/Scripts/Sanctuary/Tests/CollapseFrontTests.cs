using System;
using NUnit.Framework;

namespace Burmalda.Sanctuary.Tests
{
    public class CollapseFrontTests
    {
        [Test]
        public void New_RowPositionEqualsStartRow()
        {
            Assert.AreEqual(4f, new CollapseFront(4, 1f).RowPosition);
        }

        [Test]
        public void Tick_AdvancesByRateTimesTime()
        {
            var front = new CollapseFront(0, 2f);

            front.Tick(1.5f);

            Assert.AreEqual(3f, front.RowPosition, 1e-5f);
        }

        [Test]
        public void Tick_WithSpeedMultiplier_AdvancesFaster()
        {
            var front = new CollapseFront(0, 2f);

            front.Tick(1f, 1.5f);

            Assert.AreEqual(3f, front.RowPosition, 1e-5f);
        }

        [Test]
        public void Tick_NonPositiveDelta_DoesNothing()
        {
            var front = new CollapseFront(0, 2f);

            front.Tick(0f);
            front.Tick(-1f);

            Assert.AreEqual(0f, front.RowPosition);
        }

        [Test]
        public void HasCollapsed_RowAtOrBehindFront_IsTrue()
        {
            var front = new CollapseFront(0, 1f);
            front.Tick(3f);

            Assert.IsTrue(front.HasCollapsed(3));
            Assert.IsTrue(front.HasCollapsed(1));
            Assert.IsFalse(front.HasCollapsed(4));
        }

        [Test]
        public void Constructor_NonPositiveRate_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CollapseFront(0, 0f));
        }
    }
}
