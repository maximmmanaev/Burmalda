using System;
using NUnit.Framework;

namespace Burmalda.Boss.Tests
{
    public class BossTests
    {
        [Test]
        public void Resolve_ManaBelowThreshold_ReturnsDefeat()
        {
            var boss = new Boss(5000);

            var outcome = boss.Resolve(4000);

            Assert.IsFalse(outcome.IsVictory);
            Assert.AreEqual(4000, outcome.AccumulatedMana);
            Assert.AreEqual(5000, outcome.RequiredEnergy);
        }

        [Test]
        public void Resolve_ManaEqualsThreshold_ReturnsVictory()
        {
            var boss = new Boss(5000);

            var outcome = boss.Resolve(5000);

            Assert.IsTrue(outcome.IsVictory);
        }

        [Test]
        public void Resolve_ManaBelowBoostThreshold_NoBoostedRareRelicChance()
        {
            var boss = new Boss(5000);

            var outcome = boss.Resolve(7000); // < 5000 * 1.5 = 7500

            Assert.IsTrue(outcome.IsVictory);
            Assert.IsFalse(outcome.HasBoostedRareRelicChance);
        }

        [Test]
        public void Resolve_ManaAtBoostThreshold_HasBoostedRareRelicChance()
        {
            var boss = new Boss(5000);

            var outcome = boss.Resolve(7500); // == 5000 * 1.5

            Assert.IsTrue(outcome.HasBoostedRareRelicChance);
        }

        [Test]
        public void Constructor_NonPositiveRequiredEnergy_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Boss(0));
        }
    }
}
