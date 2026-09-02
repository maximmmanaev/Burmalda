using System;
using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.BossRoom.Tests
{
    public class BossRoomTests
    {
        [Test]
        public void Constructor_ExitRowNotAfterEntryRow_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossRoom(entryRow: 5, exitRow: 5, waveRowsPerSecond: 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossRoom(entryRow: 5, exitRow: 4, waveRowsPerSecond: 1f));
        }

        [Test]
        public void Constructor_WaveStartsBehindEntryByMargin()
        {
            var room = new BossRoom(entryRow: 10, exitRow: 20, waveRowsPerSecond: 1f, waveStartingMarginRows: 3);

            Assert.AreEqual(7f, room.Wave.RowPosition);
        }

        [Test]
        public void NewBossRoom_IsActive_NotExitedNotCaught()
        {
            var room = new BossRoom(entryRow: 0, exitRow: 10, waveRowsPerSecond: 1f);

            Assert.IsTrue(room.IsActive);
            Assert.IsFalse(room.HasExited);
            Assert.IsFalse(room.HasBeenCaughtByWave);
            Assert.AreEqual(0, room.AccumulatedIncome);
        }

        [Test]
        public void CollectVein_AddsIncomeAtCurrentMultiplier()
        {
            var room = new BossRoom(entryRow: 0, exitRow: 10, waveRowsPerSecond: 1f);
            room.CollectResonance(); // 1.5x

            room.CollectVein(10);

            Assert.AreEqual(15, room.AccumulatedIncome);
        }

        [Test]
        public void CollectVein_LaterCollectionsWorthMore_MultiplierAppliedToIncomeNotAccumulated()
        {
            // PRD v8 §8.3: "каждая следующая Жила приносит больше, чем предыдущая".
            var room = new BossRoom(entryRow: 0, exitRow: 10, waveRowsPerSecond: 1f);

            room.CollectVein(10); // ×1 -> +10 (income = 10)
            room.CollectResonance(); // -> ×1.5
            room.CollectVein(10); // ×1.5 -> +15 (income = 25)

            Assert.AreEqual(25, room.AccumulatedIncome);
        }

        [Test]
        public void CollectResonance_AppliesToMultiplier()
        {
            var room = new BossRoom(entryRow: 0, exitRow: 10, waveRowsPerSecond: 1f);

            room.CollectResonance();

            Assert.AreEqual(1.5f, room.Multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void CollectEcho_MultipliesCurrentMultiplier()
        {
            var room = new BossRoom(entryRow: 0, exitRow: 10, waveRowsPerSecond: 1f);

            room.CollectEcho();

            Assert.AreEqual(2f, room.Multiplier.CurrentMultiplier, 1e-5f);
        }

        // PRD v9 §8.3, таблица "Математика взрыва": порядок имеет значение.
        [Test]
        public void CollectEcho_AfterTwoResonances_MatchesPrdWorkedExample_TimesFour()
        {
            var room = new BossRoom(entryRow: 0, exitRow: 10, waveRowsPerSecond: 1f);

            room.CollectResonance(); // 1 + 0.5
            room.CollectResonance(); // + 0.5
            room.CollectEcho(); // × 2

            Assert.AreEqual(4f, room.Multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void CollectEcho_BeforeTwoResonances_MatchesPrdWorkedExample_TimesThree()
        {
            var room = new BossRoom(entryRow: 0, exitRow: 10, waveRowsPerSecond: 1f);

            room.CollectEcho(); // 1 × 2
            room.CollectResonance(); // + 0.5
            room.CollectResonance(); // + 0.5

            Assert.AreEqual(3f, room.Multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void TickRift_VeinSubtype_AddsIncomePerSecond()
        {
            var room = new BossRoom(entryRow: 0, exitRow: 10, waveRowsPerSecond: 1f);

            room.TickRift(BossRoomRiftSubtype.Vein, 2f);

            Assert.AreEqual(BossRoom.RoomVeinIncomePerSecond * 2, room.AccumulatedIncome);
        }

        [Test]
        public void TickRift_ResonanceSubtype_IncreasesMultiplier()
        {
            var room = new BossRoom(entryRow: 0, exitRow: 10, waveRowsPerSecond: 1f);

            room.TickRift(BossRoomRiftSubtype.Resonance, 2f);

            Assert.AreEqual(1f + 0.1f * 2f, room.Multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void ReportPlayerRow_ReachesExitRow_SetsHasExited()
        {
            var room = new BossRoom(entryRow: 0, exitRow: 10, waveRowsPerSecond: 1f);

            room.ReportPlayerRow(10);

            Assert.IsTrue(room.HasExited);
            Assert.IsFalse(room.IsActive);
        }

        [Test]
        public void ReportPlayerRow_BeforeExitRow_StaysActive()
        {
            var room = new BossRoom(entryRow: 0, exitRow: 10, waveRowsPerSecond: 1f);

            room.ReportPlayerRow(9);

            Assert.IsTrue(room.IsActive);
        }

        [Test]
        public void ReportPlayerRow_WaveCatchesUp_SetsHasBeenCaughtByWave()
        {
            var room = new BossRoom(entryRow: 10, exitRow: 20, waveRowsPerSecond: 100f, waveStartingMarginRows: 3);
            room.TickWave(1f); // волна далеко впереди игрока

            room.ReportPlayerRow(12);

            Assert.IsTrue(room.HasBeenCaughtByWave);
            Assert.IsFalse(room.IsActive);
        }

        [Test]
        public void ReportPlayerRow_WaveCatchesExactlyAtExitRow_IsDeathNotEscape()
        {
            // PRD v8 §8.4: волна не подкручивается под ситуацию игрока —
            // касание на самом пороге выхода всё ещё смерть, не спасение впритык.
            var room = new BossRoom(entryRow: 0, exitRow: 10, waveRowsPerSecond: 1f, waveStartingMarginRows: 0);
            // Волна стартует на entryRow=0; догоняет ExitRow=10 за 10 секунд.
            room.TickWave(10f);

            room.ReportPlayerRow(10);

            Assert.IsTrue(room.HasBeenCaughtByWave);
            Assert.IsFalse(room.HasExited);
        }

        [Test]
        public void AfterCaughtByWave_FurtherCollectionsAreNoOp()
        {
            var room = new BossRoom(entryRow: 10, exitRow: 20, waveRowsPerSecond: 100f, waveStartingMarginRows: 3);
            room.TickWave(1f);
            room.ReportPlayerRow(12);

            room.CollectVein(100);
            room.CollectResonance();

            Assert.AreEqual(0, room.AccumulatedIncome);
            Assert.AreEqual(1f, room.Multiplier.CurrentMultiplier);
        }

        [Test]
        public void AfterExited_FurtherCollectionsAreNoOp()
        {
            var room = new BossRoom(entryRow: 0, exitRow: 10, waveRowsPerSecond: 1f);
            room.ReportPlayerRow(10);

            room.CollectVein(100);

            Assert.AreEqual(0, room.AccumulatedIncome);
        }
    }
}
