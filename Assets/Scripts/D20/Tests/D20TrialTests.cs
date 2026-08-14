using System;
using NUnit.Framework;

namespace Burmalda.D20.Tests
{
    public class D20TrialTests
    {
        [TestCase(15, D20Outcome.Fortune)]
        [TestCase(17, D20Outcome.Fortune)]
        [TestCase(20, D20Outcome.Fortune)]
        [TestCase(10, D20Outcome.Knockback)]
        [TestCase(12, D20Outcome.Knockback)]
        [TestCase(14, D20Outcome.Knockback)]
        [TestCase(1, D20Outcome.Death)]
        [TestCase(5, D20Outcome.Death)]
        [TestCase(9, D20Outcome.Death)]
        public void Roll_KnownValue_MapsToPrdOutcome(int rollValue, D20Outcome expected)
        {
            var trial = new D20Trial(() => rollValue);

            Assert.AreEqual(expected, trial.Roll());
        }

        [TestCase(0)]
        [TestCase(21)]
        [TestCase(-5)]
        public void Roll_OutOfDiceRange_Throws(int rollValue)
        {
            var trial = new D20Trial(() => rollValue);

            Assert.Throws<InvalidOperationException>(() => trial.Roll());
        }

        [Test]
        public void Constructor_NullRollSource_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new D20Trial(null));
        }
    }
}
