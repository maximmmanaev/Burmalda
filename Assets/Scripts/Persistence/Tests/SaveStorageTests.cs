using System;
using System.Collections.Generic;
using Burmalda.Artifacts;
using Burmalda.Currencies;
using Burmalda.Progression;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Persistence.Tests
{
    /// <summary>Хранилище в памяти: синхронное по умолчанию, с «зависшими» записями для проверки очереди.</summary>
    internal sealed class FakeSaveStorage : ISaveStorage
    {
        public string Stored;
        public bool FailRead;
        public bool FailWrite;
        public bool HoldWrites;
        public int WriteCount;
        public readonly List<Action> PendingWrites = new List<Action>();

        public void Read(Action<string> onRead) => onRead(FailRead ? null : Stored);

        public void Write(string text, Action<bool> onDone)
        {
            WriteCount++;
            Action finish = () =>
            {
                if (!FailWrite) Stored = text;
                onDone(!FailWrite);
            };
            if (HoldWrites) PendingWrites.Add(finish);
            else finish();
        }

        public void CompleteHeld()
        {
            var copy = new List<Action>(PendingWrites);
            PendingWrites.Clear();
            foreach (var a in copy) a();
        }
    }

    public class SaveFormatTests
    {
        private static SaveData Sample() => new SaveData
        {
            coinsBalance = 42,
            collectionRecordedIds = new List<string> { "a" },
            poolUnlockedIds = new List<string> { "b", "c" },
            depthRecordBestTier = 3
        };

        [Test]
        public void Roundtrip_PreservesDataAndTimestamp()
        {
            var r = SaveFormat.Deserialize(SaveFormat.Serialize(Sample(), 1000));

            Assert.AreEqual(SaveReadStatus.Ok, r.Status);
            Assert.AreEqual(1000, r.SavedAt);
            Assert.AreEqual(42, r.Data.coinsBalance);
            CollectionAssert.AreEqual(new[] { "a" }, r.Data.collectionRecordedIds);
            CollectionAssert.AreEqual(new[] { "b", "c" }, r.Data.poolUnlockedIds);
            Assert.AreEqual(3, r.Data.depthRecordBestTier);
        }

        [Test]
        public void Serialize_WritesCurrentVersion()
        {
            var env = JsonUtility.FromJson<SaveEnvelope>(SaveFormat.Serialize(Sample(), 1));
            Assert.AreEqual(SaveFormat.CurrentVersion, env.version);
            Assert.GreaterOrEqual(SaveFormat.CurrentVersion, 1);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void EmptyText_IsEmpty(string text)
        {
            Assert.AreEqual(SaveReadStatus.Empty, SaveFormat.Deserialize(text).Status);
        }

        [TestCase("not json at all")]
        [TestCase("{\"version\":1,\"savedAt\":5,\"data\":")]
        [TestCase("{}")]
        [TestCase("[1,2,3]")]
        [TestCase("{\"version\":1,\"savedAt\":5}")]
        public void Garbage_IsCorrupt(string text)
        {
            var r = SaveFormat.Deserialize(text);
            Assert.AreEqual(SaveReadStatus.Corrupt, r.Status);
            Assert.IsNull(r.Data);
        }

        [Test]
        public void LegacyUnversionedFile_IsAcceptedAsOk()
        {
            var legacy = JsonUtility.ToJson(Sample());
            var r = SaveFormat.Deserialize(legacy);

            Assert.AreEqual(SaveReadStatus.Ok, r.Status);
            Assert.AreEqual(0, r.SavedAt);
            Assert.AreEqual(42, r.Data.coinsBalance);
        }

        [Test]
        public void NewerVersion_IsTooNew_AndNotParsedIntoData()
        {
            var json = "{\"version\":" + (SaveFormat.CurrentVersion + 1) + ",\"savedAt\":9,\"data\":{\"coinsBalance\":5}}";
            var r = SaveFormat.Deserialize(json);
            Assert.AreEqual(SaveReadStatus.TooNew, r.Status);
            Assert.IsNull(r.Data);
        }

        [Test]
        public void Sanitize_ClampsNegativesAndDropsBadIds()
        {
            var bad = new SaveData
            {
                coinsBalance = -5,
                depthRecordBestTier = -1,
                collectionRecordedIds = new List<string> { "ok", "", null, "ok" },
                poolUnlockedIds = null
            };
            var r = SaveFormat.Deserialize(SaveFormat.Serialize(bad, 1));

            Assert.AreEqual(SaveReadStatus.Ok, r.Status);
            Assert.AreEqual(0, r.Data.coinsBalance);
            Assert.AreEqual(0, r.Data.depthRecordBestTier);
            CollectionAssert.AreEqual(new[] { "ok" }, r.Data.collectionRecordedIds);
            Assert.IsNotNull(r.Data.poolUnlockedIds);
            Assert.IsEmpty(r.Data.poolUnlockedIds);
        }
    }

    public class CompositeSaveStorageTests
    {
        private static string Save(int coins, long at) => SaveFormat.Serialize(new SaveData { coinsBalance = coins }, at);

        private static string ReadOnce(ISaveStorage s)
        {
            string result = "unset";
            s.Read(t => result = t);
            return result;
        }

        [Test]
        public void Read_CloudFails_UsesLocal()
        {
            var cloud = new FakeSaveStorage { FailRead = true };
            var local = new FakeSaveStorage { Stored = Save(7, 5) };
            Assert.AreEqual(local.Stored, ReadOnce(new CompositeSaveStorage(cloud, local)));
        }

        [Test]
        public void Read_BothValid_PicksNewer()
        {
            var cloud = new FakeSaveStorage { Stored = Save(1, 10) };
            var local = new FakeSaveStorage { Stored = Save(2, 20) };
            Assert.AreEqual(local.Stored, ReadOnce(new CompositeSaveStorage(cloud, local)));

            cloud.Stored = Save(1, 30);
            Assert.AreEqual(cloud.Stored, ReadOnce(new CompositeSaveStorage(cloud, local)));
        }

        [Test]
        public void Read_TieGoesToCloud()
        {
            var cloud = new FakeSaveStorage { Stored = Save(1, 10) };
            var local = new FakeSaveStorage { Stored = Save(2, 10) };
            Assert.AreEqual(cloud.Stored, ReadOnce(new CompositeSaveStorage(cloud, local)));
        }

        [Test]
        public void Read_CorruptCloud_ValidLocal_UsesLocal()
        {
            var cloud = new FakeSaveStorage { Stored = "{broken" };
            var local = new FakeSaveStorage { Stored = Save(2, 1) };
            Assert.AreEqual(local.Stored, ReadOnce(new CompositeSaveStorage(cloud, local)));
        }

        [Test]
        public void Read_BothCorrupt_ReturnsCorruptText_NotNull()
        {
            var cloud = new FakeSaveStorage { Stored = "{broken" };
            var local = new FakeSaveStorage { Stored = "also broken" };
            var text = ReadOnce(new CompositeSaveStorage(cloud, local));
            Assert.AreEqual(SaveReadStatus.Corrupt, SaveFormat.Deserialize(text).Status);
        }

        [Test]
        public void Read_TooNewAnywhere_BeatsCorrupt()
        {
            var newer = "{\"version\":" + (SaveFormat.CurrentVersion + 1) + ",\"savedAt\":1,\"data\":{}}";
            var cloud = new FakeSaveStorage { Stored = newer };
            var local = new FakeSaveStorage { Stored = "{broken" };
            Assert.AreEqual(SaveReadStatus.TooNew, SaveFormat.Deserialize(ReadOnce(new CompositeSaveStorage(cloud, local))).Status);
        }

        [Test]
        public void Read_NothingAnywhere_IsNull()
        {
            Assert.IsNull(ReadOnce(new CompositeSaveStorage(new FakeSaveStorage(), new FakeSaveStorage())));
        }

        [Test]
        public void Write_GoesToBoth_AndReportsSuccessIfEitherSucceeds()
        {
            var cloud = new FakeSaveStorage { FailWrite = true };
            var local = new FakeSaveStorage();
            var composite = new CompositeSaveStorage(cloud, local);
            bool? ok = null;
            composite.Write("x", r => ok = r);
            Assert.AreEqual("x", local.Stored);
            Assert.AreEqual(1, cloud.WriteCount);
            Assert.AreEqual(true, ok);

            local.FailWrite = true;
            composite.Write("y", r => ok = r);
            Assert.AreEqual(false, ok);
        }

        [Test]
        public void Write_WaitsForSlowCloud_BeforeReportingBothFailed()
        {
            var cloud = new FakeSaveStorage { HoldWrites = true, FailWrite = true };
            var local = new FakeSaveStorage { FailWrite = true };
            var composite = new CompositeSaveStorage(cloud, local);
            var results = new List<bool>();
            composite.Write("x", results.Add);
            Assert.IsEmpty(results);
            cloud.CompleteHeld();
            CollectionAssert.AreEqual(new[] { false }, results);
        }
    }

    public class SaveCoordinatorTests
    {
        private FakeSaveStorage _storage;
        private SaveData _state;
        private SaveData _applied;
        private long _clock;
        private SaveCoordinator _c;

        [SetUp]
        public void SetUp()
        {
            _storage = new FakeSaveStorage();
            _state = new SaveData { coinsBalance = 0 };
            _applied = null;
            _clock = 100;
            _c = new SaveCoordinator(_storage, () => _state, d => { _applied = d; _state.coinsBalance += d.coinsBalance; }, () => _clock);
        }

        private int StoredCoins() => SaveFormat.Deserialize(_storage.Stored).Data.coinsBalance;

        [Test]
        public void Load_AppliesStoredData_AndMarksLoaded()
        {
            _storage.Stored = SaveFormat.Serialize(new SaveData { coinsBalance = 9 }, 1);
            _c.Load();
            Assert.IsTrue(_c.IsLoaded);
            Assert.AreEqual(SaveReadStatus.Ok, _c.LoadStatus);
            Assert.AreEqual(9, _applied.coinsBalance);
        }

        [Test]
        public void Load_NothingStored_LoadedWithoutApply()
        {
            _c.Load();
            Assert.IsTrue(_c.IsLoaded);
            Assert.AreEqual(SaveReadStatus.Empty, _c.LoadStatus);
            Assert.IsNull(_applied);
        }

        [Test]
        public void NoWrite_BeforeLoadFinished_ProtectsStoredProgressFromEmptyOverwrite()
        {
            _storage.Stored = SaveFormat.Serialize(new SaveData { coinsBalance = 50 }, 1);
            _c.MarkDirty();
            _c.Tick(10f);
            _c.Flush();
            Assert.AreEqual(0, _storage.WriteCount);
        }

        [Test]
        public void Dirty_IsWrittenOnNextTick_WithClockTimestamp()
        {
            _c.Load();
            _state.coinsBalance = 5;
            _c.MarkDirty();
            _c.Tick(10f);
            var r = SaveFormat.Deserialize(_storage.Stored);
            Assert.AreEqual(5, r.Data.coinsBalance);
            Assert.AreEqual(100, r.SavedAt);
        }

        [Test]
        public void NotDirty_NoWrite()
        {
            _c.Load();
            _c.Tick(10f);
            Assert.AreEqual(0, _storage.WriteCount);
        }

        [Test]
        public void Writes_AreThrottled_AndTrailingChangeIsNotLost()
        {
            _c.Load();
            _state.coinsBalance = 1; _c.MarkDirty(); _c.Tick(10f);
            _state.coinsBalance = 2; _c.MarkDirty(); _c.Tick(11f);
            Assert.AreEqual(1, _storage.WriteCount);
            Assert.AreEqual(1, StoredCoins());

            _c.Tick(10f + SaveCoordinator.MinWriteIntervalSeconds);
            Assert.AreEqual(2, _storage.WriteCount);
            Assert.AreEqual(2, StoredCoins());
        }

        [Test]
        public void Flush_IgnoresThrottle()
        {
            _c.Load();
            _state.coinsBalance = 1; _c.MarkDirty(); _c.Tick(10f);
            _state.coinsBalance = 2; _c.MarkDirty(); _c.Flush();
            Assert.AreEqual(2, _storage.WriteCount);
            Assert.AreEqual(2, StoredCoins());
        }

        [Test]
        public void NoSecondWrite_WhileOneInFlight()
        {
            _storage.HoldWrites = true;
            _c.Load();
            _state.coinsBalance = 1; _c.MarkDirty(); _c.Tick(10f);
            _state.coinsBalance = 2; _c.MarkDirty(); _c.Tick(20f);
            Assert.AreEqual(1, _storage.WriteCount);

            _storage.CompleteHeld();
            _c.Tick(40f);
            Assert.AreEqual(2, _storage.WriteCount);
        }

        [Test]
        public void HungWrite_DoesNotBlockSavingForever()
        {
            _storage.HoldWrites = true;
            _c.Load();
            _state.coinsBalance = 1; _c.MarkDirty(); _c.Tick(10f);
            _state.coinsBalance = 2; _c.MarkDirty();
            _c.Tick(10f + SaveCoordinator.WriteTimeoutSeconds + 1f);
            Assert.AreEqual(2, _storage.WriteCount);
        }

        [Test]
        public void FailedWrite_IsRetriedAfterInterval()
        {
            _c.Load();
            _storage.FailWrite = true;
            _state.coinsBalance = 3; _c.MarkDirty(); _c.Tick(10f);
            Assert.AreEqual(1, _storage.WriteCount);

            _storage.FailWrite = false;
            _c.Tick(10f + SaveCoordinator.MinWriteIntervalSeconds);
            Assert.AreEqual(2, _storage.WriteCount);
            Assert.AreEqual(3, StoredCoins());
        }

        [Test]
        public void CorruptStorage_StartsFresh_ThenCanWrite()
        {
            _storage.Stored = "{garbage";
            _c.Load();
            Assert.IsTrue(_c.IsLoaded);
            Assert.AreEqual(SaveReadStatus.Corrupt, _c.LoadStatus);
            Assert.IsNull(_applied);

            _state.coinsBalance = 4; _c.MarkDirty(); _c.Tick(10f);
            Assert.AreEqual(4, StoredCoins());
        }

        [Test]
        public void TooNewStorage_IsNeverOverwritten()
        {
            var newer = "{\"version\":" + (SaveFormat.CurrentVersion + 1) + ",\"savedAt\":1,\"data\":{}}";
            _storage.Stored = newer;
            _c.Load();
            Assert.IsTrue(_c.IsBlocked);
            _c.MarkDirty(); _c.Tick(10f); _c.Flush();
            Assert.AreEqual(0, _storage.WriteCount);
            Assert.AreEqual(newer, _storage.Stored);
        }

        [Test]
        public void ReadFailure_TreatedAsEmpty_ButStillLoaded()
        {
            _storage.FailRead = true;
            _c.Load();
            Assert.IsTrue(_c.IsLoaded);
            Assert.AreEqual(SaveReadStatus.Empty, _c.LoadStatus);
        }

        [Test]
        public void ReloadAfterPageRefresh_RestoresProgress_WithRealSnapshot()
        {
            // «Сессия 1»: игрок получил прогресс, он сразу сохранился.
            var s1 = new Parts();
            var c1 = new SaveCoordinator(_storage, s1.Capture, s1.Apply, () => 1);
            c1.Load();
            s1.Coins.Deposit(120);
            s1.Pool.Unlock("relic-1");
            s1.Collection.Record("relic-1");
            s1.Depth.ReportTier(2);
            c1.MarkDirty();
            c1.Tick(10f);

            // «Сессия 2»: перезагрузка страницы, свежие пустые объекты, то же хранилище.
            var s2 = new Parts();
            var c2 = new SaveCoordinator(_storage, s2.Capture, s2.Apply, () => 2);
            c2.Load();

            Assert.AreEqual(120, s2.Coins.Balance);
            Assert.IsTrue(s2.Pool.IsUnlocked("relic-1"));
            Assert.AreEqual(2, s2.Depth.BestTier);
        }

        private sealed class Parts
        {
            public PersistentWallet Coins = new PersistentWallet();
            public ArtifactCollection Collection = new ArtifactCollection();
            public ArtifactPool Pool = new ArtifactPool();
            public DepthRecord Depth = new DepthRecord();
            public SaveData Capture() => ProgressSnapshot.Capture(Coins, Collection, Pool, Depth);
            public void Apply(SaveData d) => ProgressSnapshot.Apply(d, Coins, Collection, Pool, Depth);
        }
    }
}

namespace Burmalda.Persistence.Tests
{
    public class SaveStorageFactoryTests
    {
        [Test]
        public void Select_NotWebGl_IsLocalOnly()
        {
            Assert.IsInstanceOf<LocalSaveStorage>(SaveStorageFactory.Select(false, "h"));
        }

        [Test]
        public void Select_WebGl_IsCloudPlusLocal()
        {
            Assert.IsInstanceOf<CompositeSaveStorage>(SaveStorageFactory.Select(true, "h"));
        }

        [Test]
        public void CloudStorage_OutsideWebGl_HasNoData_AndFailsWrites()
        {
            var cloud = new YandexCloudSaveStorage("h");
            string read = "unset";
            bool? wrote = null;
            cloud.Read(t => read = t);
            cloud.Write("x", ok => wrote = ok);
            Assert.IsNull(read);
            Assert.AreEqual(false, wrote);
        }
    }
}
