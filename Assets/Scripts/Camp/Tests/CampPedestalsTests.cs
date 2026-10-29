using Burmalda.Artifacts;
using Burmalda.Currencies;
using NUnit.Framework;

namespace Burmalda.Camp.Tests
{
    public class CampPedestalsTests
    {
        [Test]
        public void Empty_PedestalsHaveNoItem_AndDarkSilhouette()
        {
            var progress = new CollectionProgress();
            var idol = CampPedestals.For(progress, CollectibleKind.Idol);
            var totem = CampPedestals.For(progress, CollectibleKind.Totem);

            Assert.IsNull(idol.Item);
            Assert.IsNull(totem.Item);
            Assert.AreEqual(0, idol.Lit);
            Assert.AreEqual(0f, idol.PityGlow);
        }

        [Test]
        public void EachPart_LightsAPiece_OfTheItemInProgress()
        {
            var progress = new CollectionProgress();
            progress.AddPart("idol-shield");

            var pedestal = CampPedestals.For(progress, CollectibleKind.Idol);

            Assert.AreEqual("idol-shield", pedestal.Item.Id);
            Assert.AreEqual(1, pedestal.Lit);
            Assert.AreEqual(4, pedestal.Total, "1/4 у Редкого");
            Assert.IsFalse(pedestal.Assembled);
            Assert.IsNull(CampPedestals.For(progress, CollectibleKind.Totem).Item, "постаменты независимы");
        }

        [Test]
        public void AssembledItem_StandsOnPedestal_WhenNothingInProgress()
        {
            var progress = new CollectionProgress();
            progress.SetParts("idol-rush", 4);
            progress.SetParts("idol-time", 5);

            var pedestal = CampPedestals.For(progress, CollectibleKind.Idol);

            Assert.IsTrue(pedestal.Assembled);
            Assert.AreEqual("idol-time", pedestal.Item.Id, "высшая редкость");
            Assert.AreEqual(pedestal.Total, pedestal.Lit);
        }

        [Test]
        public void ItemInProgress_ShowsInsteadOfAssembled()
        {
            var progress = new CollectionProgress();
            progress.SetParts("idol-rush", 4);
            progress.SetParts("idol-shield", 2);

            Assert.AreEqual("idol-shield", CampPedestals.For(progress, CollectibleKind.Idol).Item.Id);
        }

        [Test]
        public void PityGlow_GrowsWithAccumulatedMisses_AndIsCapped()
        {
            var progress = new CollectionProgress();
            progress.SetPartMisses(RelicRarity.Rare, 5);
            var low = CampPedestals.PityGlowOf(progress);
            progress.SetPartMisses(RelicRarity.Epic, 100);

            Assert.Greater(low, 0f);
            Assert.AreEqual(1f, CampPedestals.PityGlowOf(progress));
        }

        [Test]
        public void NearestGoal_IsStartedItemWithFewestPartsLeft_ElseFirstRare_ElseNull()
        {
            var progress = new CollectionProgress();
            Assert.AreEqual(RelicRarity.Rare, CampPedestals.NearestGoal(progress).Value.Item.Rarity);

            progress.SetParts("totem-amik", 1);
            progress.SetParts("idol-time", 4); // осталась 1 часть
            var goal = CampPedestals.NearestGoal(progress).Value;
            Assert.AreEqual("idol-time", goal.Item.Id);
            Assert.AreEqual(1, goal.PartsLeft);

            foreach (var item in CollectibleCatalog.All) progress.SetParts(item.Id, item.PartsRequired);
            Assert.IsNull(CampPedestals.NearestGoal(progress));
        }

        [Test]
        public void OpeningFlow_PartChoice_PausesInChoosingPhase_ThenResumes()
        {
            SarcophagusConfig.PartBasePercent = new[] { 100.0, 100.0, 100.0 };
            var oldChoice = SarcophagusConfig.PartChoicePercent;
            SarcophagusConfig.PartChoicePercent = 100;
            try
            {
                var camp = new Camp(new PersistentWallet(), new ArtifactPool(), new CollectionProgress { FirstPartGranted = true }, new System.Random(3));
                camp.AddUnopened(new[] { new Sarcophagus(SarcophagusRank.Junior) });
                var flow = new SarcophagusOpeningFlow(camp);
                flow.Begin();
                flow.Advance();

                Assert.AreEqual(SarcophagusOpeningPhase.ChoosingPart, flow.Phase);
                Assert.IsFalse(flow.ChoosePart("no-such"));
                var pick = flow.LastContents.Loot.PartChoices[0];
                Assert.IsTrue(flow.ChoosePart(pick.Id));
                Assert.AreEqual(SarcophagusOpeningPhase.Opened, flow.Phase);
                Assert.AreEqual(pick, flow.ChosenPart);
            }
            finally
            {
                SarcophagusConfig.PartBasePercent = new[] { 15.0, 4.0, 1.5 };
                SarcophagusConfig.PartChoicePercent = oldChoice;
            }
        }
    }
}
