using System.Collections.Generic;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.BossRoom.Tests
{
    public class BossRoomRunSystemTests
    {
        // Задача «Комната Босса» — те же изменяемые static-поля, что и у
        // BossRoomGeneratorTests: снимок/восстановление вокруг КАЖДОГО
        // теста (порядок выполнения NUnit не гарантирован).
        private int _savedResonanceMin, _savedResonanceMax, _savedEchoMin, _savedEchoMax, _savedRoomLength, _savedVeinAmount;
        private float _savedVeinShare, _savedWaveSpeed;

        [SetUp]
        public void SaveDefaults()
        {
            _savedResonanceMin = BossRoomGenerator.ResonanceCountMin;
            _savedResonanceMax = BossRoomGenerator.ResonanceCountMax;
            _savedEchoMin = BossRoomGenerator.EchoCountMin;
            _savedEchoMax = BossRoomGenerator.EchoCountMax;
            _savedRoomLength = BossRoomGenerator.DefaultRoomLengthRows;
            _savedVeinAmount = BossRoomGenerator.VeinBaseAmount;
            _savedVeinShare = BossRoomGenerator.VeinShareOfRemaining;
            _savedWaveSpeed = BossRoomRunSystem.WaveRowsPerSecond;

            // Дефолт всех тестов файла — ноль Резонанса/Эха, вся остальная
            // Комната — Жила (детерминированно, без веса на shuffle/roll).
            BossRoomGenerator.ResonanceCountMin = 0;
            BossRoomGenerator.ResonanceCountMax = 0;
            BossRoomGenerator.EchoCountMin = 0;
            BossRoomGenerator.EchoCountMax = 0;
            BossRoomGenerator.VeinShareOfRemaining = 1f;
            BossRoomGenerator.VeinBaseAmount = 30;
            BossRoomGenerator.DefaultRoomLengthRows = 2;
        }

        [TearDown]
        public void RestoreDefaults()
        {
            BossRoomGenerator.ResonanceCountMin = _savedResonanceMin;
            BossRoomGenerator.ResonanceCountMax = _savedResonanceMax;
            BossRoomGenerator.EchoCountMin = _savedEchoMin;
            BossRoomGenerator.EchoCountMax = _savedEchoMax;
            BossRoomGenerator.DefaultRoomLengthRows = _savedRoomLength;
            BossRoomGenerator.VeinBaseAmount = _savedVeinAmount;
            BossRoomGenerator.VeinShareOfRemaining = _savedVeinShare;
            BossRoomRunSystem.WaveRowsPerSecond = _savedWaveSpeed;
        }

        private static System.Func<float> Constant(float value) => () => value;

        // Однополосный тоннель (ширина 1) — каждый ряд Комнаты состоит
        // ровно из одной клетки, что делает результат генерации
        // предсказуемым без необходимости предугадывать порядок shuffle.
        private static (TunnelGrid grid, GridTraceTrail trail, RunCurrencyAccumulator mana, List<string> defeats) BuildHarness(int bossRow)
        {
            var grid = new TunnelGrid(1);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 0));
            grid.GetOrCreateTile(new GridCoordinate(bossRow, 0)).MarkBoss();
            var mana = new RunCurrencyAccumulator();
            var defeats = new List<string>();
            return (grid, trail, mana, defeats);
        }

        [Test]
        public void Advanced_ReachingBossTile_CreatesActiveRoomStartingAtThatRow()
        {
            var (grid, trail, mana, defeats) = BuildHarness(bossRow: 1);
            using var system = new BossRoomRunSystem(grid, trail, mana, Constant(0f), defeats.Add);

            trail.TryAdvanceTo(new GridCoordinate(1, 0));

            Assert.IsNotNull(system.ActiveRoom);
            Assert.AreEqual(1, system.ActiveRoom.EntryRow);
            Assert.IsTrue(system.ActiveRoom.IsActive);
        }

        [Test]
        public void Advanced_CollectVeinTileInRoom_AddsIncomeAtCurrentMultiplier()
        {
            var (grid, trail, mana, defeats) = BuildHarness(bossRow: 1);
            using var system = new BossRoomRunSystem(grid, trail, mana, Constant(0f), defeats.Add);
            trail.TryAdvanceTo(new GridCoordinate(1, 0)); // вход в Комнату

            trail.TryAdvanceTo(new GridCoordinate(2, 0)); // первая клетка Комнаты — Жила (VeinShareOfRemaining=1)

            Assert.AreEqual(BossRoomTileKind.Vein, grid.GetOrCreateTile(new GridCoordinate(2, 0)).BossRoomTile);
            Assert.AreEqual(30, system.ActiveRoom.AccumulatedIncome);
        }

        [Test]
        public void Advanced_CollectResonanceTileInRoom_IncreasesMultiplier()
        {
            BossRoomGenerator.ResonanceCountMin = 1;
            BossRoomGenerator.ResonanceCountMax = 1;
            BossRoomGenerator.DefaultRoomLengthRows = 1; // единственная клетка Комнаты — гарантированно Резонанс

            var (grid, trail, mana, defeats) = BuildHarness(bossRow: 1);
            using var system = new BossRoomRunSystem(grid, trail, mana, Constant(0f), defeats.Add);
            trail.TryAdvanceTo(new GridCoordinate(1, 0));

            trail.TryAdvanceTo(new GridCoordinate(2, 0));

            Assert.AreEqual(BossRoomTileKind.Resonance, grid.GetOrCreateTile(new GridCoordinate(2, 0)).BossRoomTile);
            Assert.AreEqual(1.5f, system.ActiveRoom.Multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void Advanced_CollectEchoTileInRoom_MultipliesMultiplier()
        {
            BossRoomGenerator.EchoCountMin = 1;
            BossRoomGenerator.EchoCountMax = 1;
            BossRoomGenerator.DefaultRoomLengthRows = 1;

            var (grid, trail, mana, defeats) = BuildHarness(bossRow: 1);
            using var system = new BossRoomRunSystem(grid, trail, mana, Constant(0f), defeats.Add);
            trail.TryAdvanceTo(new GridCoordinate(1, 0));

            trail.TryAdvanceTo(new GridCoordinate(2, 0));

            Assert.AreEqual(BossRoomTileKind.Echo, grid.GetOrCreateTile(new GridCoordinate(2, 0)).BossRoomTile);
            Assert.AreEqual(2f, system.ActiveRoom.Multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void PositionChanged_ReachingExitRow_CreditsManaAndDeactivatesRoom()
        {
            BossRoomGenerator.DefaultRoomLengthRows = 1; // выход совпадает с единственной клеткой Комнаты

            var (grid, trail, mana, defeats) = BuildHarness(bossRow: 1);
            using var system = new BossRoomRunSystem(grid, trail, mana, Constant(0f), defeats.Add);
            trail.TryAdvanceTo(new GridCoordinate(1, 0));

            trail.TryAdvanceTo(new GridCoordinate(2, 0)); // Жила (доход 30) И выход одним и тем же ходом

            Assert.AreEqual(30, mana.Total);
            Assert.IsFalse(system.ActiveRoom.IsActive);
            Assert.IsTrue(system.ActiveRoom.HasExited);
            Assert.IsEmpty(defeats);
        }

        [Test]
        public void Tick_WaveCatchesPlayer_ReportsBossDefeatAndDeactivatesRoom()
        {
            BossRoomRunSystem.WaveRowsPerSecond = 100f;
            BossRoomGenerator.DefaultRoomLengthRows = 5;

            var (grid, trail, mana, defeats) = BuildHarness(bossRow: 1);
            using var system = new BossRoomRunSystem(grid, trail, mana, Constant(0f), defeats.Add);
            trail.TryAdvanceTo(new GridCoordinate(1, 0)); // вход — волна стартует на entryRow-3

            system.Tick(1f); // волна улетает далеко вперёд игрока

            trail.TryAdvanceTo(new GridCoordinate(2, 0));

            Assert.AreEqual(1, defeats.Count);
            Assert.IsFalse(system.ActiveRoom.IsActive);
            Assert.IsTrue(system.ActiveRoom.HasBeenCaughtByWave);
            Assert.AreEqual(0, mana.Total); // волна поймала — счёт Комнаты не начисляется
        }

        [Test]
        public void Dispose_UnsubscribesFromTrail_BossTileNoLongerEntersRoom()
        {
            var (grid, trail, mana, defeats) = BuildHarness(bossRow: 1);
            var system = new BossRoomRunSystem(grid, trail, mana, Constant(0f), defeats.Add);
            system.Dispose();

            trail.TryAdvanceTo(new GridCoordinate(1, 0));

            Assert.IsNull(system.ActiveRoom);
        }
    }
}
