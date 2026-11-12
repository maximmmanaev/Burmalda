using Burmalda.Artifacts;
using Burmalda.Currencies;
using NUnit.Framework;

namespace Burmalda.Camp.Tests
{
    public class CampUpgradesTests
    {
        private static (Camp camp, PersistentWallet wallet) Make(int coins, params string[] assembled)
        {
            var wallet = new PersistentWallet();
            if (coins > 0) wallet.Deposit(coins);
            var progress = new CollectionProgress();
            foreach (var id in assembled) progress.SetParts(id, CollectibleCatalog.Get(id).PartsRequired);
            return (new Camp(wallet, new ArtifactPool(), progress), wallet);
        }

        [Test]
        public void AssembledItem_StartsAtLevel1()
        {
            var (camp, _) = Make(0, "idol-rush");
            Assert.AreEqual(1, camp.Progress.LevelOf("idol-rush"));
            Assert.AreEqual(0, camp.Progress.LevelOf("idol-shield"), "не собран");
        }

        [Test]
        public void Upgrade_L1ToL2ToL3_SpendsPrices_ThenStopsAtMax()
        {
            var item = CollectibleCatalog.Get("idol-rush");
            var p1 = CampUpgrades.PriceFor(item, 1);
            var p2 = CampUpgrades.PriceFor(item, 2);
            var (camp, wallet) = Make(p1 + p2 + 5, "idol-rush");

            Assert.IsTrue(camp.TryUpgrade("idol-rush"));
            Assert.AreEqual(2, camp.Progress.LevelOf("idol-rush"));
            Assert.AreEqual(p2 + 5, wallet.Balance);

            Assert.IsTrue(camp.TryUpgrade("idol-rush"));
            Assert.AreEqual(3, camp.Progress.LevelOf("idol-rush"));
            Assert.AreEqual(5, wallet.Balance);

            Assert.IsFalse(camp.TryUpgrade("idol-rush"), "Яндекс — 3 уровня");
            Assert.AreEqual(0, CampUpgrades.PriceFor(item, 3));
        }

        [Test]
        public void Upgrade_NotEnoughCoins_ChangesNothing()
        {
            var (camp, wallet) = Make(10, "totem-amik");

            Assert.IsFalse(camp.TryUpgrade("totem-amik"));
            Assert.AreEqual(1, camp.Progress.LevelOf("totem-amik"));
            Assert.AreEqual(10, wallet.Balance);
        }

        [Test]
        public void Upgrade_NotAssembled_Fails()
        {
            var (camp, wallet) = Make(100000);

            Assert.IsFalse(camp.TryUpgrade("idol-time"));
            Assert.IsFalse(camp.TryUpgrade("no-such"));
            Assert.AreEqual(100000, wallet.Balance);
        }

        [Test]
        public void Prices_GrowWithLevelAndRarity()
        {
            var rare = CollectibleCatalog.Get("idol-rush");
            var epic = CollectibleCatalog.Get("idol-time");
            var legendary = CollectibleCatalog.Get("idol-passage");

            Assert.Less(CampUpgrades.PriceFor(rare, 1), CampUpgrades.PriceFor(rare, 2));
            Assert.Less(CampUpgrades.PriceFor(rare, 1), CampUpgrades.PriceFor(epic, 1));
            Assert.Less(CampUpgrades.PriceFor(epic, 1), CampUpgrades.PriceFor(legendary, 1));
        }

        [Test]
        public void Equip_OnlyAssembled_PicksActiveIdolAndTotem()
        {
            var (camp, _) = Make(0, "idol-rush", "idol-shield", "totem-maang");

            Assert.IsTrue(camp.Equip("idol-shield"));
            Assert.AreEqual("idol-shield", camp.Progress.ActiveIdolId);
            Assert.IsFalse(camp.Equip("idol-time"), "не собран");
            Assert.AreEqual("idol-shield", camp.Progress.ActiveIdolId);
        }

        [Test]
        public void Upgraded_AbilityReceivesNewLevel()
        {
            var (camp, _) = Make(100000, "idol-rush");
            camp.TryUpgrade("idol-rush");

            Assert.AreEqual(2, camp.Progress.LevelOf("idol-rush"), "IdolRunSystem берёт уровень из прогресса");
        }
    }
}
