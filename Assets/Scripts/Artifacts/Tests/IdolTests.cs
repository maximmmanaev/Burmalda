using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class IdolTests
    {
        [Test]
        public void NewIdol_BothPassivesStartAtZero()
        {
            var idol = new Idol("i1", "Тестовый Идол");

            Assert.AreEqual(0, idol.PassiveALevel);
            Assert.AreEqual(0, idol.PassiveBLevel);
            Assert.AreEqual(ArtifactCategory.Idol, idol.Category);
        }

        [Test]
        public void UpgradePassiveA_IncreasesOnlyPassiveALevel()
        {
            var idol = new Idol("i1", "Тестовый Идол");

            idol.UpgradePassiveA();
            idol.UpgradePassiveA();

            Assert.AreEqual(2, idol.PassiveALevel);
            Assert.AreEqual(0, idol.PassiveBLevel);
        }

        [Test]
        public void UpgradePassiveB_IncreasesOnlyPassiveBLevel()
        {
            var idol = new Idol("i1", "Тестовый Идол");

            idol.UpgradePassiveB();

            Assert.AreEqual(0, idol.PassiveALevel);
            Assert.AreEqual(1, idol.PassiveBLevel);
        }
    }
}
