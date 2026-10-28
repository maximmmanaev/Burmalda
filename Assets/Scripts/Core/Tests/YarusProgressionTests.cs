using NUnit.Framework;

namespace Burmalda.Core.Tests
{
    public class YarusProgressionTests
    {
        [TestCase(1, 1), TestCase(3, 1), TestCase(4, 2), TestCase(6, 2), TestCase(7, 3), TestCase(9, 3), TestCase(10, 4)]
        public void DifficultyBand_ChangesOnYaruses4_7_10(int yarus, int band) =>
            Assert.AreEqual(band, YarusProgression.DifficultyBand(yarus));

        [TestCase(1, 90), TestCase(3, 90), TestCase(4, 110), TestCase(6, 110), TestCase(7, 130), TestCase(9, 130), TestCase(10, 140)]
        public void TunnelRows_ByBand(int yarus, int rows) =>
            Assert.AreEqual(rows, YarusProgression.TunnelRows(yarus));

        [Test]
        public void DecayLifetimeMultiplier_FallsByBand()
        {
            Assert.AreEqual(0.8f, YarusProgression.DecayLifetimeMultiplier(1), 1e-5f);
            Assert.AreEqual(0.7f, YarusProgression.DecayLifetimeMultiplier(4), 1e-5f);
            Assert.AreEqual(0.6f, YarusProgression.DecayLifetimeMultiplier(7), 1e-5f);
            Assert.AreEqual(0.55f, YarusProgression.DecayLifetimeMultiplier(10), 1e-5f);
        }
    }
}
