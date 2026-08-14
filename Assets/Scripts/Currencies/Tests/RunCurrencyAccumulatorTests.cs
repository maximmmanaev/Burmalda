using NUnit.Framework;

namespace Burmalda.Currencies.Tests
{
    public class RunCurrencyAccumulatorTests
    {
        [Test]
        public void NewAccumulator_TotalIsZero()
        {
            var accumulator = new RunCurrencyAccumulator();

            Assert.AreEqual(0, accumulator.Total);
        }

        [Test]
        public void Add_PositiveAmount_IncreasesTotal()
        {
            var accumulator = new RunCurrencyAccumulator();

            accumulator.Add(5);
            accumulator.Add(3);

            Assert.AreEqual(8, accumulator.Total);
        }

        [Test]
        public void Add_ZeroOrNegativeAmount_IsNoOp()
        {
            var accumulator = new RunCurrencyAccumulator();
            accumulator.Add(5);

            accumulator.Add(0);
            accumulator.Add(-3);

            Assert.AreEqual(5, accumulator.Total);
        }

        [Test]
        public void Add_PositiveAmount_RaisesChangedWithNewTotal()
        {
            var accumulator = new RunCurrencyAccumulator();
            var seen = -1;
            accumulator.Changed += total => seen = total;

            accumulator.Add(7);

            Assert.AreEqual(7, seen);
        }

        [Test]
        public void Add_ZeroOrNegativeAmount_DoesNotRaiseChanged()
        {
            var accumulator = new RunCurrencyAccumulator();
            var fired = false;
            accumulator.Changed += _ => fired = true;

            accumulator.Add(0);
            accumulator.Add(-1);

            Assert.IsFalse(fired);
        }
    }
}
