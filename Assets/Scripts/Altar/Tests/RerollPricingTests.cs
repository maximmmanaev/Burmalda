using System;
using NUnit.Framework;

namespace Burmalda.Altar.Tests
{
    public class RerollPricingTests
    {
        [Test]
        public void CostForRerollNumber_First_EqualsBaseCost()
        {
            Assert.AreEqual(RerollPricing.BaseCost, RerollPricing.CostForRerollNumber(1));
        }

        [Test]
        public void CostForRerollNumber_Second_IsBaseCostTimesEscalation()
        {
            var expected = (int)Math.Round(RerollPricing.BaseCost * RerollPricing.EscalationFactor);
            Assert.AreEqual(expected, RerollPricing.CostForRerollNumber(2));
        }

        [Test]
        public void CostForRerollNumber_IncreasesMonotonically()
        {
            var previous = 0;
            for (var n = 1; n <= 5; n++)
            {
                var cost = RerollPricing.CostForRerollNumber(n);
                Assert.Greater(cost, previous, $"реролл {n} должен быть дороже предыдущего");
                previous = cost;
            }
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void CostForRerollNumber_LessThanOne_Throws(int rerollNumber)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RerollPricing.CostForRerollNumber(rerollNumber));
        }
    }
}
