using Burmalda.Artifacts;
using Burmalda.Currencies;
using NUnit.Framework;

namespace Burmalda.Altar.Tests
{
    public class RitualTests
    {
        private static float ConstantRandom(float value) => value;

        [Test]
        public void Constructor_NoUnlockedAmuletsOrTalismans_PrimaryChestIsNull()
        {
            var ritual = new Ritual(new ArtifactPool(), () => ConstantRandom(0f));

            Assert.IsNull(ritual.PrimaryChest);
            Assert.IsNotNull(ritual.RuneChestOffer);
        }

        [Test]
        public void Constructor_UnlockedAmulet_OffersAmuletChest()
        {
            var pool = new ArtifactPool();
            pool.Unlock(ArtifactCatalog.Amulets[0].Id);

            var ritual = new Ritual(pool, () => ConstantRandom(0f));

            Assert.IsInstanceOf<AmuletChest>(ritual.PrimaryChest);
        }

        [Test]
        public void TryReroll_InsufficientKeys_ReturnsFalseAndDoesNotChangeOffers()
        {
            var ritual = new Ritual(new ArtifactPool(), () => ConstantRandom(0f));
            var keys = new RunCurrencyAccumulator();
            var runeBefore = ritual.RuneChestOffer;

            var result = ritual.TryReroll(keys);

            Assert.IsFalse(result);
            Assert.AreSame(runeBefore, ritual.RuneChestOffer);
        }

        [Test]
        public void TryReroll_SufficientKeys_SpendsKeysAndIncreasesNextRerollCost()
        {
            var ritual = new Ritual(new ArtifactPool(), () => ConstantRandom(0f));
            var keys = new RunCurrencyAccumulator();
            keys.Add(1000);
            var firstCost = ritual.NextRerollCost;

            var result = ritual.TryReroll(keys);

            Assert.IsTrue(result);
            Assert.AreEqual(1000 - firstCost, keys.Total);
            Assert.Greater(ritual.NextRerollCost, firstCost);
        }

        [Test]
        public void NextRerollCost_WithFreeRerolls_IsZeroUntilExhausted()
        {
            // PRD v7 §20, Знамение «Слепой Спуск»: "+1 бесплатный реролл на каждом Алтаре".
            var ritual = new Ritual(new ArtifactPool(), () => ConstantRandom(0f), freeRerolls: 1);

            Assert.AreEqual(0, ritual.NextRerollCost);
        }

        [Test]
        public void TryReroll_WithFreeRerolls_DoesNotSpendKeysUntilExhausted()
        {
            var ritual = new Ritual(new ArtifactPool(), () => ConstantRandom(0f), freeRerolls: 1);
            var keys = new RunCurrencyAccumulator(); // 0 Ключей — платный реролл сейчас же провалился бы

            var firstResult = ritual.TryReroll(keys); // бесплатный
            var costAfterFree = ritual.NextRerollCost;
            var secondResult = ritual.TryReroll(keys); // уже не бесплатный, Ключей нет — провал

            Assert.IsTrue(firstResult);
            Assert.AreEqual(0, keys.Total);
            Assert.Greater(costAfterFree, 0);
            Assert.IsFalse(secondResult);
        }

        [Test]
        public void Constructor_NegativeFreeRerolls_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Ritual(new ArtifactPool(), () => ConstantRandom(0f), freeRerolls: -1));
        }

        [Test]
        public void TryPurchasePrimary_NullPrimaryChest_ReturnsFalse()
        {
            var ritual = new Ritual(new ArtifactPool(), () => ConstantRandom(0f));
            var keys = new RunCurrencyAccumulator();
            keys.Add(1000);
            var loadout = new RunArtifactLoadout(new ArtifactCollection());

            Assert.IsFalse(ritual.TryPurchasePrimary(keys, loadout));
        }

        [Test]
        public void TryPurchasePrimary_InsufficientKeys_ReturnsFalse()
        {
            var pool = new ArtifactPool();
            pool.Unlock(ArtifactCatalog.Amulets[0].Id);
            var ritual = new Ritual(pool, () => ConstantRandom(0f));
            var keys = new RunCurrencyAccumulator();
            var loadout = new RunArtifactLoadout(new ArtifactCollection());

            Assert.IsFalse(ritual.TryPurchasePrimary(keys, loadout));
            Assert.IsEmpty(loadout.Acquired);
        }

        [Test]
        public void TryPurchasePrimary_SufficientKeys_SpendsAndAddsToLoadout()
        {
            var pool = new ArtifactPool();
            pool.Unlock(ArtifactCatalog.Amulets[0].Id);
            var ritual = new Ritual(pool, () => ConstantRandom(0f));
            var keys = new RunCurrencyAccumulator();
            keys.Add(1000);
            var loadout = new RunArtifactLoadout(new ArtifactCollection());
            var cost = ritual.PrimaryChest.Cost;

            var result = ritual.TryPurchasePrimary(keys, loadout);

            Assert.IsTrue(result);
            Assert.AreEqual(1, loadout.Acquired.Count);
            Assert.AreEqual(1000 - cost, keys.Total);
        }

        [Test]
        public void TryPurchasePrimary_AfterPurchase_SlotBecomesNullUntilReroll()
        {
            var pool = new ArtifactPool();
            pool.Unlock(ArtifactCatalog.Amulets[0].Id);
            var ritual = new Ritual(pool, () => ConstantRandom(0f));
            var keys = new RunCurrencyAccumulator();
            keys.Add(1000);
            var loadout = new RunArtifactLoadout(new ArtifactCollection());

            ritual.TryPurchasePrimary(keys, loadout);

            Assert.IsNull(ritual.PrimaryChest);
            Assert.IsFalse(ritual.TryPurchasePrimary(keys, loadout), "повторная покупка того же слота без реролла невозможна");
        }

        [Test]
        public void TryPurchaseRuneChest_InsufficientKeys_ReturnsNull()
        {
            var ritual = new Ritual(new ArtifactPool(), () => ConstantRandom(0f));
            var keys = new RunCurrencyAccumulator();

            Assert.IsNull(ritual.TryPurchaseRuneChest(keys));
        }

        [Test]
        public void TryPurchaseRuneChest_SufficientKeys_ReturnsRuneAndSpendsKeys()
        {
            var ritual = new Ritual(new ArtifactPool(), () => ConstantRandom(0f));
            var keys = new RunCurrencyAccumulator();
            keys.Add(1000);
            var cost = ritual.RuneChestOffer.Cost;

            var rune = ritual.TryPurchaseRuneChest(keys);

            Assert.IsNotNull(rune);
            Assert.AreEqual(1000 - cost, keys.Total);
        }

        [Test]
        public void TrySell_ArtifactInLoadout_RemovesAndRefundsFewerKeysThanCost()
        {
            var loadout = new RunArtifactLoadout(new ArtifactCollection());
            var talisman = ArtifactCatalog.Talismans[0];
            loadout.Acquire(talisman);
            var keys = new RunCurrencyAccumulator();
            var ritual = new Ritual(new ArtifactPool(), () => ConstantRandom(0f));

            var sold = ritual.TrySell(talisman, loadout, keys);

            Assert.IsTrue(sold);
            Assert.IsEmpty(loadout.Acquired);
            Assert.Greater(keys.Total, 0);
            Assert.Less(keys.Total, Ritual.TalismanOrAmuletChestCost, "продажа должна быть дешевле цены покупки");
        }

        [Test]
        public void TrySell_ArtifactNotInLoadout_ReturnsFalse()
        {
            var loadout = new RunArtifactLoadout(new ArtifactCollection());
            var keys = new RunCurrencyAccumulator();
            var ritual = new Ritual(new ArtifactPool(), () => ConstantRandom(0f));

            Assert.IsFalse(ritual.TrySell(ArtifactCatalog.Talismans[0], loadout, keys));
        }

        [Test]
        public void PreviewResonanceLossOnSell_SellingBreaksResonance_ReturnsLostResonance()
        {
            var loadout = new RunArtifactLoadout(new ArtifactCollection());
            var keyTalismanA = new Talisman("kt1", "К1", "э", new[] { ArtifactTag.Keys });
            var keyTalismanB = new Talisman("kt2", "К2", "э", new[] { ArtifactTag.Keys });
            loadout.Acquire(keyTalismanA);
            loadout.Acquire(keyTalismanB);
            var ritual = new Ritual(new ArtifactPool(), () => ConstantRandom(0f));

            var lost = ritual.PreviewResonanceLossOnSell(keyTalismanB, loadout);

            CollectionAssert.Contains(lost, ResonanceType.KeyBond);
        }

        [Test]
        public void PreviewResonanceLossOnSell_SellingUnrelatedArtifact_ReturnsEmpty()
        {
            var loadout = new RunArtifactLoadout(new ArtifactCollection());
            var keyTalismanA = new Talisman("kt1", "К1", "э", new[] { ArtifactTag.Keys });
            var keyTalismanB = new Talisman("kt2", "К2", "э", new[] { ArtifactTag.Keys });
            var manaTalisman = new Talisman("mt1", "М1", "э", new[] { ArtifactTag.Mana });
            loadout.Acquire(keyTalismanA);
            loadout.Acquire(keyTalismanB);
            loadout.Acquire(manaTalisman);
            var ritual = new Ritual(new ArtifactPool(), () => ConstantRandom(0f));

            var lost = ritual.PreviewResonanceLossOnSell(manaTalisman, loadout);

            Assert.IsEmpty(lost);
        }

        [Test]
        public void PurchasedRune_CanBeAppliedToIdolPassiveA()
        {
            // Issue #21: "Выбор руны, постоянно улучшающей 1 из 2 пассивов
            // Идола или активную способность Тотема" — сам выбор цели
            // (PassiveA/PassiveB/Totem) — решение игрока/UI, Ritual только
            // выдаёт Руну; применение — уже существующие методы Idol/Totem.
            var ritual = new Ritual(new ArtifactPool(), () => ConstantRandom(0f));
            var keys = new RunCurrencyAccumulator();
            keys.Add(1000);
            var idol = new Idol("i1", "Идол");

            var rune = ritual.TryPurchaseRuneChest(keys);
            idol.UpgradePassiveA();

            Assert.IsNotNull(rune);
            Assert.AreEqual(1, idol.PassiveALevel);
        }

        [Test]
        public void PreviewResonanceGainOnPurchase_CompletesResonance_ReturnsGainedResonance()
        {
            var loadout = new RunArtifactLoadout(new ArtifactCollection());
            loadout.Acquire(new Talisman("kt1", "К1", "э", new[] { ArtifactTag.Keys }));
            var candidate = new Talisman("kt2", "К2", "э", new[] { ArtifactTag.Keys });
            var ritual = new Ritual(new ArtifactPool(), () => ConstantRandom(0f));

            var gained = ritual.PreviewResonanceGainOnPurchase(candidate, loadout);

            CollectionAssert.Contains(gained, ResonanceType.KeyBond);
        }

        [Test]
        public void PreviewResonanceGainOnPurchase_UnrelatedCandidate_ReturnsEmpty()
        {
            var loadout = new RunArtifactLoadout(new ArtifactCollection());
            var candidate = new Talisman("mt1", "М1", "э", new[] { ArtifactTag.Mana });
            var ritual = new Ritual(new ArtifactPool(), () => ConstantRandom(0f));

            var gained = ritual.PreviewResonanceGainOnPurchase(candidate, loadout);

            Assert.IsEmpty(gained);
        }
    }
}
