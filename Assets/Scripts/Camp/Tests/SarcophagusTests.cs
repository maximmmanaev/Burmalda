using System.Collections.Generic;
using System.Linq;
using Burmalda.Artifacts;
using Burmalda.Currencies;
using NUnit.Framework;

namespace Burmalda.Camp.Tests
{
    public class SarcophagusTests
    {
        [Test]
        public void Open_Junior_GivesCoinsByRank()
        {
            var contents = new Sarcophagus(SarcophagusRank.Junior).Open();

            Assert.AreEqual(Sarcophagus.CoinsForRank(SarcophagusRank.Junior), contents.Coins);
            Assert.Greater(contents.Coins, 0);
        }

        [Test]
        public void CoinsForRank_GrowsWithRank()
        {
            Assert.Less(Sarcophagus.CoinsForRank(SarcophagusRank.Junior), Sarcophagus.CoinsForRank(SarcophagusRank.Middle));
            Assert.Less(Sarcophagus.CoinsForRank(SarcophagusRank.Middle), Sarcophagus.CoinsForRank(SarcophagusRank.Senior));
        }

        [Test]
        public void RunSarcophagi_AddAndClear_TrackCountAndRaiseChanged()
        {
            var carried = new RunSarcophagi();
            var changes = 0;
            carried.Changed += () => changes++;

            carried.Add(new Sarcophagus(SarcophagusRank.Junior));
            carried.Add(new Sarcophagus(SarcophagusRank.Junior));
            Assert.AreEqual(2, carried.Count);

            carried.Clear();

            Assert.AreEqual(0, carried.Count);
            Assert.AreEqual(3, changes);
        }

        [Test]
        public void RunSarcophagi_ClearWhenEmpty_DoesNotRaiseChanged()
        {
            var carried = new RunSarcophagi();
            var raised = false;
            carried.Changed += () => raised = true;

            carried.Clear();

            Assert.IsFalse(raised);
        }

        private static Camp CreateCamp(out PersistentWallet wallet)
        {
            wallet = new PersistentWallet();
            return new Camp(wallet, new ArtifactPool());
        }

        [Test]
        public void Camp_AddUnopened_KeepsDimmerFirst_BrighterLast()
        {
            var camp = CreateCamp(out _);

            camp.AddUnopened(new[]
            {
                new Sarcophagus(SarcophagusRank.Senior),
                new Sarcophagus(SarcophagusRank.Junior),
                new Sarcophagus(SarcophagusRank.Middle)
            });

            CollectionAssert.AreEqual(
                new[] { SarcophagusRank.Junior, SarcophagusRank.Middle, SarcophagusRank.Senior },
                camp.UnopenedSarcophagi.Select(s => s.Rank));
        }

        [Test]
        public void Camp_OpenNext_DepositsCoinsAndRemovesOneByOne()
        {
            var camp = CreateCamp(out var wallet);
            camp.AddUnopened(new[] { new Sarcophagus(SarcophagusRank.Junior), new Sarcophagus(SarcophagusRank.Junior) });
            var opened = new List<SarcophagusContents>();
            camp.SarcophagusOpened += opened.Add;

            var first = camp.OpenNext();

            Assert.IsNotNull(first);
            Assert.AreEqual(1, camp.UnopenedSarcophagi.Count);
            Assert.AreEqual(first.Value.Coins, wallet.Balance);
            Assert.AreEqual(1, opened.Count);

            camp.OpenNext();

            Assert.AreEqual(0, camp.UnopenedSarcophagi.Count);
            Assert.AreEqual(first.Value.Coins * 2, wallet.Balance);
        }

        [Test]
        public void Camp_OpenNext_WhenNothingToOpen_ReturnsNull()
        {
            var camp = CreateCamp(out var wallet);

            Assert.IsNull(camp.OpenNext());
            Assert.AreEqual(0, wallet.Balance);
        }
    }
}
