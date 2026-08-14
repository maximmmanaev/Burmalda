using NUnit.Framework;

namespace Burmalda.Altar.Tests
{
    public class ManaToBossIndicatorTests
    {
        [Test]
        public void ComputeDeficit_CurrentBelowRequired_ReturnsDifference()
        {
            Assert.AreEqual(2000, ManaToBossIndicator.ComputeDeficit(3200, 5200));
        }

        [Test]
        public void ComputeDeficit_CurrentEqualsRequired_ReturnsZero()
        {
            Assert.AreEqual(0, ManaToBossIndicator.ComputeDeficit(5200, 5200));
        }

        [Test]
        public void ComputeDeficit_CurrentExceedsRequired_ReturnsZeroNotNegative()
        {
            // PRD v7 §8.2 (Перелив энергии) — избыток не "долг", клампим к 0.
            Assert.AreEqual(0, ManaToBossIndicator.ComputeDeficit(6000, 5200));
        }
    }
}
