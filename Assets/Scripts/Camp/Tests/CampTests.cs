using Burmalda.Artifacts;
using Burmalda.Currencies;
using NUnit.Framework;

namespace Burmalda.Camp.Tests
{
    public class CampTests
    {
        private static Camp CreateCamp(out PersistentWallet coins, out ArtifactPool pool)
        {
            coins = new PersistentWallet();
            pool = new ArtifactPool();
            return new Camp(coins, pool);
        }

        [Test]
        public void TryUpgradeIdolPassiveA_SufficientCoins_SpendsAndUpgrades()
        {
            var camp = CreateCamp(out var coins, out _);
            coins.Deposit(100);
            var idol = new Idol("i1", "Идол");

            var result = camp.TryUpgradeIdolPassiveA(idol, 60);

            Assert.IsTrue(result);
            Assert.AreEqual(1, idol.PassiveALevel);
            Assert.AreEqual(40, coins.Balance);
        }

        [Test]
        public void TryUpgradeIdolPassiveA_InsufficientCoins_ReturnsFalseAndDoesNotUpgrade()
        {
            var camp = CreateCamp(out var coins, out _);
            coins.Deposit(10);
            var idol = new Idol("i1", "Идол");

            var result = camp.TryUpgradeIdolPassiveA(idol, 60);

            Assert.IsFalse(result);
            Assert.AreEqual(0, idol.PassiveALevel);
            Assert.AreEqual(10, coins.Balance);
        }

        [Test]
        public void TryUpgradeIdolPassiveB_SufficientCoins_SpendsAndUpgrades()
        {
            var camp = CreateCamp(out var coins, out _);
            coins.Deposit(100);
            var idol = new Idol("i1", "Идол");

            var result = camp.TryUpgradeIdolPassiveB(idol, 60);

            Assert.IsTrue(result);
            Assert.AreEqual(1, idol.PassiveBLevel);
        }

        [Test]
        public void TryUpgradeTotem_SufficientCoins_SpendsAndUpgrades()
        {
            var camp = CreateCamp(out var coins, out _);
            coins.Deposit(100);
            var totem = new Totem("t1", "Тотем", TotemAbilityType.Dash);

            var result = camp.TryUpgradeTotem(totem, 60);

            Assert.IsTrue(result);
            Assert.AreEqual(1, totem.Level);
        }

        [Test]
        public void OpenRelic_UnlocksGrantedArtifactInPool()
        {
            var camp = CreateCamp(out _, out var pool);
            var relic = new Relic("rel1", "Реликвия Босса");
            var newIdol = new Idol("i-new", "Новый Идол");

            camp.OpenRelic(relic, newIdol);

            Assert.IsTrue(pool.IsUnlocked("i-new"));
        }
    }
}
