using System.Linq;
using Burmalda.Artifacts;
using Burmalda.Camp;
using Burmalda.Currencies;
using Burmalda.Progression;
using NUnit.Framework;
using UnityEngine;
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

        // ---- v10-4: редкость/бог Саркофагов, части, защита от невезения, флаг первой части ----

        [Test]
        public void RoundTrip_SarcophagusRarityAndGod_Parts_Pity_FirstPartFlag()
        {
            var f = new Fixture();
            f.Camp.AddUnopened(new[] { new Sarcophagus(SarcophagusRank.Senior, RelicRarity.Legendary, God.Enlil), new Sarcophagus(SarcophagusRank.Junior, RelicRarity.Epic, God.Leshy) });
            f.Camp.Progress.SetParts("idol-shield", 3);
            f.Camp.Progress.SetPartMisses(RelicRarity.Epic, 5);
            f.Camp.Progress.SetRelicMisses(RelicRarity.Legendary, 9);
            f.Camp.Progress.FirstPartGranted = true;

            var restored = new Fixture();
            restored.Apply(SaveFormat.Deserialize(SaveFormat.Serialize(f.Capture(), 1)).Data);

            CollectionAssert.AreEqual(
                new[] { (RelicRarity.Epic, God.Leshy), (RelicRarity.Legendary, God.Enlil) },
                restored.Camp.UnopenedSarcophagi.Select(x => (x.Rarity, x.God)));
            Assert.AreEqual(3, restored.Camp.Progress.PartsOf("idol-shield"));
            Assert.AreEqual(5, restored.Camp.Progress.PartMisses(RelicRarity.Epic));
            Assert.AreEqual(9, restored.Camp.Progress.RelicMisses(RelicRarity.Legendary));
            Assert.IsTrue(restored.Camp.Progress.FirstPartGranted);
        }

        [Test]
        public void OldSave_WithoutNewFields_LoadsWithDefaults_NothingLost()
        {
            var f = new Fixture();
            var data = new SaveData { coinsBalance = 5 };
            data.unopenedSarcophagiRanks.Add((int)SarcophagusRank.Middle);
            data.unopenedSarcophagiRarities = null; // версии < 3 не знали этих полей
            data.partItemIds = null;

            f.Apply(data);

            Assert.AreEqual(1, f.Camp.UnopenedSarcophagi.Count);
            Assert.AreEqual(RelicRarity.Rare, f.Camp.UnopenedSarcophagi[0].Rarity);
            Assert.AreEqual(God.Leshy, f.Camp.UnopenedSarcophagi[0].God);
            Assert.IsFalse(f.Camp.Progress.FirstPartGranted);
        }

        [Test]
        public void Version2Save_ParsesIntoCurrentFormat()
        {
            var payload = "{\"coinsBalance\":12,\"unopenedSarcophagiRanks\":[1]}";
            var text = JsonUtility.ToJson(new SaveEnvelope { version = 2, savedAt = 3, payload = payload });

            var r = SaveFormat.Deserialize(text);

            Assert.AreEqual(SaveReadStatus.Ok, r.Status);
            Assert.AreEqual(12, r.Data.coinsBalance);
            Assert.IsNotNull(r.Data.partItemIds);
            Assert.IsNotNull(r.Data.unopenedSarcophagiRarities);
            Assert.GreaterOrEqual(SaveFormat.CurrentVersion, 3);
        }

        [Test]
        public void Apply_ClampsPartsToRequired_IgnoresUnknownItems()
        {
            var f = new Fixture();
            var data = new SaveData();
            data.partItemIds.AddRange(new[] { "idol-rush", "no-such-item" });
            data.partItemCounts.AddRange(new[] { 99, 3 });

            f.Apply(data);

            Assert.AreEqual(4, f.Camp.Progress.PartsOf("idol-rush"), "Редкий — максимум 4 части");
            Assert.AreEqual(0, f.Camp.Progress.PartsOf("no-such-item"));
        }

        [Test]
        public void RoundTrip_ActiveIdolAndTotem()
        {
            var f = new Fixture();
            f.Camp.Progress.SetParts("idol-time", 5);
            f.Camp.Progress.SetParts("totem-maang", 4);
            f.Camp.Progress.TrySelect("idol-time");
            f.Camp.Progress.TrySelect("totem-maang");

            var restored = new Fixture();
            restored.Apply(SaveFormat.Deserialize(SaveFormat.Serialize(f.Capture(), 1)).Data);

            Assert.AreEqual("idol-time", restored.Camp.Progress.ActiveIdolId);
            Assert.AreEqual("totem-maang", restored.Camp.Progress.ActiveTotemId);
        }

        [Test]
        public void RoundTrip_ItemLevels_AndVersion4()
        {
            var f = new Fixture();
            f.Camp.Progress.SetParts("idol-rush", 4);
            f.Camp.Progress.SetLevel("idol-rush", 3);

            var restored = new Fixture();
            restored.Apply(SaveFormat.Deserialize(SaveFormat.Serialize(f.Capture(), 1)).Data);

            Assert.AreEqual(3, restored.Camp.Progress.LevelOf("idol-rush"));
            Assert.AreEqual(4, SaveFormat.CurrentVersion);
        }

        [Test]
        public void Version3Save_LoadsWithAssembledItemsAtLevel1_AndEmptySlotsAutoFilled()
        {
            var payload = "{\"coinsBalance\":9,\"partItemIds\":[\"idol-shield\"],\"partItemCounts\":[4]}";
            var text = JsonUtility.ToJson(new SaveEnvelope { version = 3, savedAt = 1, payload = payload });
            var f = new Fixture();

            f.Apply(SaveFormat.Deserialize(text).Data);

            Assert.AreEqual(1, f.Camp.Progress.LevelOf("idol-shield"));
            f.Camp.Progress.AutoEquipIfEmpty();
            Assert.AreEqual("idol-shield", f.Camp.Progress.ActiveIdolId);
        }
    }
}
