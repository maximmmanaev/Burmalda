using NUnit.Framework;

namespace Burmalda.Core.Tests
{
    public class MultiplierCurveTests
    {
        // Значения буквально перенесены из legacy/burmolda_demo.html, MCURVE
        // (PRD 4.3) — тест фиксирует границы каждой "полки" кривой.
        [TestCase(0, 1)]
        [TestCase(9, 1)]
        [TestCase(10, 2)]
        [TestCase(18, 2)]
        [TestCase(19, 4)]
        [TestCase(26, 4)]
        [TestCase(27, 7)]
        [TestCase(33, 7)]
        [TestCase(34, 12)]
        [TestCase(38, 12)]
        [TestCase(39, 20)]
        public void GetMultiplier_KnownEffectiveLength_MatchesLegacyCurve(int effectiveLength, int expected)
        {
            Assert.AreEqual(expected, MultiplierCurve.GetMultiplier(effectiveLength));
        }

        [Test]
        public void GetMultiplier_AtCurveLength_ReturnsMaxMultiplier()
        {
            Assert.AreEqual(MultiplierCurve.MaxMultiplier, MultiplierCurve.GetMultiplier(40));
        }

        [Test]
        public void GetMultiplier_WellBeyondCurveLength_ReturnsMaxMultiplier()
        {
            Assert.AreEqual(MultiplierCurve.MaxMultiplier, MultiplierCurve.GetMultiplier(1000));
        }

        [Test]
        public void GetMultiplier_NegativeEffectiveLength_ClampsToZero()
        {
            Assert.AreEqual(MultiplierCurve.GetMultiplier(0), MultiplierCurve.GetMultiplier(-5));
        }
    }
}
