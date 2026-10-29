using Burmalda.Artifacts;
using Burmalda.Currencies;
using NUnit.Framework;

namespace Burmalda.Camp.Tests
{
    public class SarcophagusOpeningFlowTests
    {
        private static (Camp camp, PersistentWallet wallet) CreateCamp(int junior)
        {
            var wallet = new PersistentWallet();
            var camp = new Camp(wallet, new ArtifactPool());
            for (var i = 0; i < junior; i++) camp.AddUnopened(new[] { new Sarcophagus(SarcophagusRank.Junior) });
            return (camp, wallet);
        }

        [Test]
        public void Begin_WithNothingToOpen_GoesStraightToFinished()
        {
            var (camp, _) = CreateCamp(0);
            var flow = new SarcophagusOpeningFlow(camp);

            flow.Begin();

            Assert.AreEqual(SarcophagusOpeningPhase.Finished, flow.Phase);
        }

        [Test]
        public void Begin_WithSarcophagi_ShowsClosedWithoutOpeningAnything()
        {
            var (camp, wallet) = CreateCamp(2);
            var flow = new SarcophagusOpeningFlow(camp);

            flow.Begin();

            Assert.AreEqual(SarcophagusOpeningPhase.Closed, flow.Phase);
            Assert.AreEqual(2, camp.UnopenedSarcophagi.Count);
            Assert.AreEqual(0, wallet.Balance);
        }

        [Test]
        public void Advance_OpensOneByOne_ThenFinishes()
        {
            var (camp, wallet) = CreateCamp(2);
            var flow = new SarcophagusOpeningFlow(camp);
            flow.Begin();

            flow.Advance(); // открыть первый
            Assert.AreEqual(SarcophagusOpeningPhase.Opened, flow.Phase);
            Assert.AreEqual(SarcophagusConfig.CoinsFor(RelicRarity.Rare), flow.LastContents.Coins);
            Assert.AreEqual(1, camp.UnopenedSarcophagi.Count);

            flow.Advance(); // к следующему
            Assert.AreEqual(SarcophagusOpeningPhase.Closed, flow.Phase);

            flow.Advance(); // открыть второй
            flow.Advance(); // больше нечего
            Assert.AreEqual(SarcophagusOpeningPhase.Finished, flow.Phase);
            Assert.AreEqual(SarcophagusConfig.CoinsFor(RelicRarity.Rare) * 2, wallet.Balance);
        }

        [Test]
        public void Advance_WhenFinished_IsNoOp()
        {
            var (camp, wallet) = CreateCamp(0);
            var flow = new SarcophagusOpeningFlow(camp);
            flow.Begin();

            flow.Advance();

            Assert.AreEqual(SarcophagusOpeningPhase.Finished, flow.Phase);
            Assert.AreEqual(0, wallet.Balance);
        }

        [Test]
        public void Reset_ReturnsToIdle()
        {
            var (camp, _) = CreateCamp(1);
            var flow = new SarcophagusOpeningFlow(camp);
            flow.Begin();

            flow.Reset();

            Assert.AreEqual(SarcophagusOpeningPhase.Idle, flow.Phase);
        }
    }
}
