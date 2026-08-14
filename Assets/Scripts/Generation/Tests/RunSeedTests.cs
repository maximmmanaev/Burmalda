using System;
using NUnit.Framework;

namespace Burmalda.Generation.Tests
{
    public class RunSeedTests
    {
        [Test]
        public void Value_ReturnsConstructorSeed()
        {
            var seed = new RunSeed(12345);
            Assert.AreEqual(12345, seed.Value);
        }

        [Test]
        public void NextFloat01_SameSeed_ProducesIdenticalSequence()
        {
            var a = new RunSeed(42);
            var b = new RunSeed(42);

            for (var i = 0; i < 20; i++)
                Assert.AreEqual(a.NextFloat01(), b.NextFloat01(), $"расхождение на вызове {i}");
        }

        [Test]
        public void NextFloat01_DifferentSeeds_ProduceDifferentFirstValue()
        {
            var a = new RunSeed(1);
            var b = new RunSeed(2);

            Assert.AreNotEqual(a.NextFloat01(), b.NextFloat01());
        }

        [Test]
        public void NextFloat01_AlwaysWithinZeroToOneRange()
        {
            var seed = new RunSeed(7);
            for (var i = 0; i < 1000; i++)
            {
                var value = seed.NextFloat01();
                Assert.GreaterOrEqual(value, 0f);
                Assert.Less(value, 1f);
            }
        }

        [Test]
        public void NextInt_AlwaysWithinRequestedRange()
        {
            var seed = new RunSeed(99);
            for (var i = 0; i < 1000; i++)
            {
                var value = seed.NextInt(3, 8);
                Assert.GreaterOrEqual(value, 3);
                Assert.Less(value, 8);
            }
        }

        [Test]
        public void NextInt_SameSeed_ProducesIdenticalSequence()
        {
            var a = new RunSeed(555);
            var b = new RunSeed(555);

            for (var i = 0; i < 20; i++)
                Assert.AreEqual(a.NextInt(0, 100), b.NextInt(0, 100));
        }

        [Test]
        public void ComputeTrialSeed_SameDateAndTier_ProducesSameValue()
        {
            var date = new DateTime(2027, 3, 15);
            Assert.AreEqual(RunSeed.ComputeTrialSeed(date, 2), RunSeed.ComputeTrialSeed(date, 2));
        }

        [Test]
        public void ComputeTrialSeed_DifferentTier_ProducesDifferentValue()
        {
            var date = new DateTime(2027, 3, 15);
            Assert.AreNotEqual(RunSeed.ComputeTrialSeed(date, 1), RunSeed.ComputeTrialSeed(date, 2));
        }

        [Test]
        public void ComputeTrialSeed_DifferentDate_ProducesDifferentValue()
        {
            Assert.AreNotEqual(
                RunSeed.ComputeTrialSeed(new DateTime(2027, 3, 15), 1),
                RunSeed.ComputeTrialSeed(new DateTime(2027, 3, 16), 1));
        }

        [Test]
        public void ComputeTrialSeed_IgnoresTimeOfDayComponent()
        {
            Assert.AreEqual(
                RunSeed.ComputeTrialSeed(new DateTime(2027, 3, 15, 0, 0, 0), 1),
                RunSeed.ComputeTrialSeed(new DateTime(2027, 3, 15, 23, 59, 59), 1));
        }
    }
}
