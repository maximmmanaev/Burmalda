using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Decay.Tests
{
    public class TrailDecaySystemTests
    {
        private const float TileSize = 1f;
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail, TrailDecaySystem decay) CreateSystem()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var decay = new TrailDecaySystem(grid, trail);
            return (grid, trail, decay);
        }

        [Test]
        public void Tick_StartTileNeverDecays_EvenAfterLongTime()
        {
            var (grid, trail, decay) = CreateSystem();

            decay.Tick(1000f);

            Assert.IsFalse(grid.GetOrCreateTile(trail.CurrentPosition).IsDestroyed);
            Assert.IsFalse(decay.IsCurrentTileDestroyed);
        }

        [Test]
        public void TryAdvanceTo_BeginsDecayImmediatelyWithThresholdForTrailIndex()
        {
            var (grid, trail, _) = CreateSystem();
            var target = new GridCoordinate(1, 2);

            trail.TryAdvanceTo(target);

            var tile = grid.GetOrCreateTile(target);
            Assert.IsTrue(tile.DecayThresholdSeconds.HasValue);
            // legacy/burmolda_demo.html: maxD = 6 + trail.length*0.18; index плиты в трейле — 1.
            Assert.AreEqual(6f + 1 * 0.18f, tile.DecayThresholdSeconds.Value, 1e-5f);
        }

        [Test]
        public void Tick_SingleLargeStep_DestroysTileOnceThresholdExceeded()
        {
            var (grid, trail, decay) = CreateSystem();
            var target = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(target);

            // accel = 1 + 7*0.025 = 1.175; накоплено = 7*1.175 = 8.225 >= 6.18 (порог).
            decay.Tick(7f);

            Assert.IsTrue(grid.GetOrCreateTile(target).IsDestroyed);
            Assert.IsTrue(decay.IsCurrentTileDestroyed);
        }

        [Test]
        public void Tick_AccumulatesAcrossMultipleCalls_DestroysOnlyOnceThresholdReached()
        {
            var (grid, trail, decay) = CreateSystem();
            var target = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(target); // порог = 6.18с

            // Тик 1: accel = 1.075, накоплено = 3*1.075 = 3.225 (< 6.18).
            decay.Tick(3f);
            Assert.IsFalse(grid.GetOrCreateTile(target).IsDestroyed);

            // Тик 2: accel = 1.15, += 3*1.15 = 3.45; итого 6.675 (>= 6.18).
            decay.Tick(3f);
            Assert.IsTrue(grid.GetOrCreateTile(target).IsDestroyed);
        }

        [Test]
        public void Tick_ZigzagPath_EachTileDecaysIndependentlyByOwnThreshold()
        {
            var (grid, trail, decay) = CreateSystem();
            trail.TryAdvanceTo(new GridCoordinate(1, 3)); // index 1, порог 6.18
            trail.TryAdvanceTo(new GridCoordinate(2, 2)); // index 2, порог 6.36
            trail.TryAdvanceTo(new GridCoordinate(3, 3)); // index 3, порог 6.54

            // accel = 1 + 10*0.025 = 1.25; накоплено = 10*1.25 = 12.5 — превышает все три порога разом.
            decay.Tick(10f);

            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(1, 3)).IsDestroyed);
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(2, 2)).IsDestroyed);
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(3, 3)).IsDestroyed);
            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(0, 2)).IsDestroyed);
        }

        [Test]
        public void TileDestroyed_FiresExactlyOnceForEachTileTransition()
        {
            var (grid, trail, decay) = CreateSystem();
            var target = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(target);

            var firedCount = 0;
            GridCoordinate? firedCoordinate = null;
            decay.TileDestroyed += coordinate =>
            {
                firedCount++;
                firedCoordinate = coordinate;
            };

            decay.Tick(7f); // разрушает плиту
            decay.Tick(7f); // плита уже разрушена — повторного события быть не должно

            Assert.AreEqual(1, firedCount);
            Assert.AreEqual(target, firedCoordinate);
        }

        [Test]
        public void Dispose_StopsBeginningDecayForFutureTrailAdvances()
        {
            var (grid, trail, decay) = CreateSystem();
            decay.Dispose();

            var target = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(target);

            decay.Tick(1000f);

            Assert.IsFalse(grid.GetOrCreateTile(target).IsDestroyed);
        }
    }
}
