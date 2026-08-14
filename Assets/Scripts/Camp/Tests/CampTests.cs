using Burmalda.Artifacts;
using Burmalda.Currencies;
using NUnit.Framework;

namespace Burmalda.Camp.Tests
{
    public class CampTests
    {
        private static Camp CreateCamp(out PersistentWallet coins, out PersistentWallet crystals, out ArtifactPool pool)
        {
            coins = new PersistentWallet();
            crystals = new PersistentWallet();
            pool = new ArtifactPool();
            return new Camp(coins, crystals, pool);
        }

        [Test]
        public void TryUpgradeIdolPassiveA_SufficientCoins_SpendsAndUpgrades()
        {
            var camp = CreateCamp(out var coins, out _, out _);
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
            var camp = CreateCamp(out var coins, out _, out _);
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
            var camp = CreateCamp(out var coins, out _, out _);
            coins.Deposit(100);
            var idol = new Idol("i1", "Идол");

            var result = camp.TryUpgradeIdolPassiveB(idol, 60);

            Assert.IsTrue(result);
            Assert.AreEqual(1, idol.PassiveBLevel);
        }

        [Test]
        public void TryUpgradeTotem_SufficientCoins_SpendsAndUpgrades()
        {
            var camp = CreateCamp(out var coins, out _, out _);
            coins.Deposit(100);
            var totem = new Totem("t1", "Тотем", TotemAbilityType.Dash);

            var result = camp.TryUpgradeTotem(totem, 60);

            Assert.IsTrue(result);
            Assert.AreEqual(1, totem.Level);
        }

        [Test]
        public void TryUnlockArtifact_SufficientCrystals_SpendsAndUnlocks()
        {
            var camp = CreateCamp(out _, out var crystals, out var pool);
            crystals.Deposit(500);

            var result = camp.TryUnlockArtifact("top-idol-1", 300);

            Assert.IsTrue(result);
            Assert.IsTrue(pool.IsUnlocked("top-idol-1"));
            Assert.AreEqual(200, crystals.Balance);
        }

        [Test]
        public void TryUnlockArtifact_InsufficientCrystals_ReturnsFalseAndDoesNotUnlock()
        {
            var camp = CreateCamp(out _, out var crystals, out var pool);
            crystals.Deposit(100);

            var result = camp.TryUnlockArtifact("top-idol-1", 300);

            Assert.IsFalse(result);
            Assert.IsFalse(pool.IsUnlocked("top-idol-1"));
        }

        [Test]
        public void TryUnlockArtifact_AlreadyUnlocked_ReturnsFalseAndDoesNotSpend()
        {
            var camp = CreateCamp(out _, out var crystals, out var pool);
            crystals.Deposit(500);
            pool.Unlock("top-idol-1");

            var result = camp.TryUnlockArtifact("top-idol-1", 300);

            Assert.IsFalse(result);
            Assert.AreEqual(500, crystals.Balance);
        }

        [Test]
        public void OpenRelic_UnlocksGrantedArtifactInPool()
        {
            var camp = CreateCamp(out _, out _, out var pool);
            var relic = new Relic("rel1", "Реликвия Босса");
            var newIdol = new Idol("i-new", "Новый Идол");

            camp.OpenRelic(relic, newIdol);

            Assert.IsTrue(pool.IsUnlocked("i-new"));
        }
    }
}
