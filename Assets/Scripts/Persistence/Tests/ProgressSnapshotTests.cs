using Burmalda.Artifacts;
using Burmalda.Camp;
using Burmalda.Currencies;
using Burmalda.Progression;
using NUnit.Framework;
using CampModel = Burmalda.Camp.Camp;

namespace Burmalda.Persistence.Tests
{
    public class ProgressSnapshotTests
    {
        private sealed class Fixture
        {
            public PersistentWallet Coins = new PersistentWallet();
            public ArtifactCollection Collection = new ArtifactCollection();
            public ArtifactPool Pool = new ArtifactPool();
            public DepthRecord DepthRecord = new DepthRecord();
            public CampModel Camp;

            public Fixture() => Camp = new CampModel(Coins, Pool);

            public SaveData Capture() => ProgressSnapshot.Capture(Coins, Collection, Pool, DepthRecord, Camp);

            public void Apply(SaveData data) => ProgressSnapshot.Apply(data, Coins, Collection, Pool, DepthRecord, Camp);
        }

        [Test]
        public void Capture_ReflectsCurrentStateOfAllSources()
        {
            var f = new Fixture();
            f.Coins.Deposit(100);
            f.Collection.Record("a1");
            f.Pool.Unlock("a1");
            f.DepthRecord.ReportTier(2);

            var data = f.Capture();

            Assert.AreEqual(100, data.coinsBalance);
            CollectionAssert.Contains(data.collectionRecordedIds, "a1");
            CollectionAssert.Contains(data.poolUnlockedIds, "a1");
            Assert.AreEqual(2, data.depthRecordBestTier);
        }

        [Test]
        public void Apply_RestoresWalletBalance()
        {
            var f = new Fixture();
            var data = new SaveData { coinsBalance = 70 };

            f.Apply(data);

            Assert.AreEqual(70, f.Coins.Balance);
        }

        [Test]
        public void Apply_RestoresCollectionAndPool()
        {
            var f = new Fixture();
            var data = new SaveData();
            data.collectionRecordedIds.Add("c1");
            data.poolUnlockedIds.Add("p1");

            f.Apply(data);

            Assert.IsTrue(f.Collection.Contains("c1"));
            Assert.IsTrue(f.Pool.IsUnlocked("p1"));
        }

        [Test]
        public void Apply_RestoresDepthRecord()
        {
            var f = new Fixture();
            var data = new SaveData { depthRecordBestTier = 3 };

            f.Apply(data);

            Assert.AreEqual(3, f.DepthRecord.BestTier);
        }

        [Test]
        public void Apply_NullData_IsNoOp()
        {
            var f = new Fixture();
            f.Coins.Deposit(10);

            f.Apply(null);

            Assert.AreEqual(10, f.Coins.Balance);
        }

        [Test]
        public void CaptureThenApplyOnFreshObjects_RoundTripsState()
        {
            var source = new Fixture();
            source.Coins.Deposit(250);
            source.Collection.Record("x1");
            source.Collection.Record("x2");
            source.Pool.Unlock("x1");
            source.DepthRecord.ReportTier(3);

            var data = source.Capture();
            var target = new Fixture();
            target.Apply(data);

            Assert.AreEqual(250, target.Coins.Balance);
            Assert.IsTrue(target.Collection.Contains("x1"));
            Assert.IsTrue(target.Collection.Contains("x2"));
            Assert.IsTrue(target.Pool.IsUnlocked("x1"));
            Assert.AreEqual(3, target.DepthRecord.BestTier);
        }

        [Test]
        public void Capture_UnopenedSarcophagi_AreStoredByRank()
        {
            var f = new Fixture();
            f.Camp.AddUnopened(new[] { new Sarcophagus(SarcophagusRank.Senior), new Sarcophagus(SarcophagusRank.Junior) });

            var data = f.Capture();

            CollectionAssert.AreEqual(new[] { (int)SarcophagusRank.Junior, (int)SarcophagusRank.Senior }, data.unopenedSarcophagiRanks);
        }

        [Test]
        public void Apply_RestoresUnopenedSarcophagi_SoNothingIsLostOnReload()
        {
            var f = new Fixture();
            var data = new SaveData();
            data.unopenedSarcophagiRanks.Add((int)SarcophagusRank.Middle);
            data.unopenedSarcophagiRanks.Add((int)SarcophagusRank.Junior);

            f.Apply(data);

            Assert.AreEqual(2, f.Camp.UnopenedSarcophagi.Count);
            Assert.AreEqual(SarcophagusRank.Junior, f.Camp.UnopenedSarcophagi[0].Rank);
        }

        [Test]
        public void Apply_OldSaveWithoutSarcophagi_IsFine()
        {
            var f = new Fixture();

            f.Apply(new SaveData { coinsBalance = 5 });

            Assert.AreEqual(0, f.Camp.UnopenedSarcophagi.Count);
            Assert.AreEqual(5, f.Coins.Balance);
        }
    }
}
