using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class TotemTests
    {
        [Test]
        public void NewTotem_HasGivenAbilityAndZeroLevel()
        {
            var totem = new Totem("t1", "Тестовый Тотем", TotemAbilityType.Dash);

            Assert.AreEqual(TotemAbilityType.Dash, totem.ActiveAbility);
            Assert.AreEqual(0, totem.Level);
            Assert.AreEqual(ArtifactCategory.Totem, totem.Category);
        }

        [Test]
        public void UpgradeLevel_IncreasesLevel()
        {
            var totem = new Totem("t1", "Тестовый Тотем", TotemAbilityType.Breach);

            totem.UpgradeLevel();
            totem.UpgradeLevel();

            Assert.AreEqual(2, totem.Level);
        }
    }
}
