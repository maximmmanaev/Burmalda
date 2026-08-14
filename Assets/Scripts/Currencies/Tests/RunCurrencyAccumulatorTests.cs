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

        [Test]
        public void Spend_SufficientTotal_DecreasesTotalAndReturnsTrue()
        {
            var accumulator = new RunCurrencyAccumulator();
            accumulator.Add(10);

            var spent = accumulator.Spend(6);

            Assert.IsTrue(spent);
            Assert.AreEqual(4, accumulator.Total);
        }

        [Test]
        public void Spend_InsufficientTotal_ReturnsFalseAndLeavesTotalUnchanged()
        {
            var accumulator = new RunCurrencyAccumulator();
            accumulator.Add(5);

            var spent = accumulator.Spend(6);

            Assert.IsFalse(spent);
            Assert.AreEqual(5, accumulator.Total);
        }

        [Test]
        public void Spend_ZeroOrNegativeAmount_ReturnsFalseAndLeavesTotalUnchanged()
        {
            var accumulator = new RunCurrencyAccumulator();
            accumulator.Add(5);

            Assert.IsFalse(accumulator.Spend(0));
            Assert.IsFalse(accumulator.Spend(-1));
            Assert.AreEqual(5, accumulator.Total);
        }

        [Test]
        public void Spend_Successful_RaisesChangedWithNewTotal()
        {
            var accumulator = new RunCurrencyAccumulator();
            accumulator.Add(10);
            var seen = -1;
            accumulator.Changed += total => seen = total;

            accumulator.Spend(4);

            Assert.AreEqual(6, seen);
        }

        [Test]
        public void Spend_Failed_DoesNotRaiseChanged()
        {
            var accumulator = new RunCurrencyAccumulator();
            accumulator.Add(5);
            var fired = false;
            accumulator.Changed += _ => fired = true;

            accumulator.Spend(100);

            Assert.IsFalse(fired);
        }

        [Test]
        public void RevertToCheckpoint_WithoutAnyCheckpoint_RevertsToZero()
        {
            // Старт забега — тоже чекпоинт (0), пока Checkpoint() не вызван явно.
            var accumulator = new RunCurrencyAccumulator();
            accumulator.Add(50);

            accumulator.RevertToCheckpoint();

            Assert.AreEqual(0, accumulator.Total);
        }

        [Test]
        public void Checkpoint_ThenAddMore_RevertToCheckpoint_RestoresCheckpointedValue()
        {
            var accumulator = new RunCurrencyAccumulator();
            accumulator.Add(50);
            accumulator.Checkpoint();
            accumulator.Add(30); // после чекпоинта — то, что "в пути"

            accumulator.RevertToCheckpoint();

            Assert.AreEqual(50, accumulator.Total);
        }

        [Test]
        public void RevertToCheckpoint_AfterSpendingBelowCheckpoint_RestoresCheckpointedValue()
        {
            var accumulator = new RunCurrencyAccumulator();
            accumulator.Add(50);
            accumulator.Checkpoint();
            accumulator.Spend(20);

            accumulator.RevertToCheckpoint();

            Assert.AreEqual(50, accumulator.Total);
        }

        [Test]
        public void RevertToCheckpoint_TotalAlreadyEqualsCheckpoint_DoesNotRaiseChanged()
        {
            var accumulator = new RunCurrencyAccumulator();
            accumulator.Add(50);
            accumulator.Checkpoint();
            var fired = false;
            accumulator.Changed += _ => fired = true;

            accumulator.RevertToCheckpoint();

            Assert.IsFalse(fired);
        }

        [Test]
        public void RevertToCheckpoint_TotalDiffersFromCheckpoint_RaisesChanged()
        {
            var accumulator = new RunCurrencyAccumulator();
            accumulator.Add(50);
            accumulator.Checkpoint();
            accumulator.Add(10);
            var seen = -1;
            accumulator.Changed += total => seen = total;

            accumulator.RevertToCheckpoint();

            Assert.AreEqual(50, seen);
        }

        [Test]
        public void Checkpoint_CalledAgainAfterMoreProgress_MovesCheckpointForward()
        {
            var accumulator = new RunCurrencyAccumulator();
            accumulator.Add(50);
            accumulator.Checkpoint();
            accumulator.Add(30);
            accumulator.Checkpoint(); // новый чекпоинт на Алтаре — 80

            accumulator.Add(10);
            accumulator.RevertToCheckpoint();

            Assert.AreEqual(80, accumulator.Total);
        }
    }
}
