using Burmalda.Artifacts;
using Burmalda.Boss;
using Burmalda.Currencies;
using Burmalda.Progression;
using NUnit.Framework;

namespace Burmalda.Persistence.Tests
{
    public class ProgressSnapshotTests
    {
        private sealed class Fixture
        {
            public PersistentWallet Coins = new PersistentWallet();
            public PersistentWallet Crystals = new PersistentWallet();
            public ArtifactCollection Collection = new ArtifactCollection();
            public ArtifactPool Pool = new ArtifactPool();
            public DepthRecord DepthRecord = new DepthRecord();
            public FirstBossVictoryTracker Tracker = new FirstBossVictoryTracker();

            public SaveData Capture() => ProgressSnapshot.Capture(Coins, Crystals, Collection, Pool, DepthRecord, Tracker);

            public void Apply(SaveData data) => ProgressSnapshot.Apply(data, Coins, Crystals, Collection, Pool, DepthRecord, Tracker);
        }

        [Test]
        public void Capture_ReflectsCurrentStateOfAllSources()
        {
            var f = new Fixture();
            f.Coins.Deposit(100);
            f.Crystals.Deposit(50);
            f.Collection.Record("a1");
            f.Pool.Unlock("a1");
            f.DepthRecord.ReportTier(2);
            f.Tracker.RecordVictory();

            var data = f.Capture();

            Assert.AreEqual(100, data.coinsBalance);
            Assert.AreEqual(50, data.crystalsBalance);
            CollectionAssert.Contains(data.collectionRecordedIds, "a1");
            CollectionAssert.Contains(data.poolUnlockedIds, "a1");
            Assert.AreEqual(2, data.depthRecordBestTier);
            Assert.IsTrue(data.hasWonBossBefore);
        }

        [Test]
        public void Apply_RestoresWalletBalances()
        {
            var f = new Fixture();
            var data = new SaveData { coinsBalance = 70, crystalsBalance = 30 };

            f.Apply(data);

            Assert.AreEqual(70, f.Coins.Balance);
            Assert.AreEqual(30, f.Crystals.Balance);
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
        public void Apply_RestoresDepthRecordAndVictoryTracker()
        {
            var f = new Fixture();
            var data = new SaveData { depthRecordBestTier = 3, hasWonBossBefore = true };

            f.Apply(data);

            Assert.AreEqual(3, f.DepthRecord.BestTier);
            Assert.IsTrue(f.Tracker.HasWonBefore);
        }

        [Test]
        public void Apply_HasWonBossBeforeFalse_DoesNotRecordVictory()
        {
            var f = new Fixture();
            var data = new SaveData { hasWonBossBefore = false };

            f.Apply(data);

            Assert.IsFalse(f.Tracker.HasWonBefore);
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
            source.Crystals.Deposit(40);
            source.Collection.Record("x1");
            source.Collection.Record("x2");
            source.Pool.Unlock("x1");
            source.DepthRecord.ReportTier(3);
            source.Tracker.RecordVictory();

            var data = source.Capture();
            var target = new Fixture();
            target.Apply(data);

            Assert.AreEqual(250, target.Coins.Balance);
            Assert.AreEqual(40, target.Crystals.Balance);
            Assert.IsTrue(target.Collection.Contains("x1"));
            Assert.IsTrue(target.Collection.Contains("x2"));
            Assert.IsTrue(target.Pool.IsUnlocked("x1"));
            Assert.AreEqual(3, target.DepthRecord.BestTier);
            Assert.IsTrue(target.Tracker.HasWonBefore);
        }
    }
}
