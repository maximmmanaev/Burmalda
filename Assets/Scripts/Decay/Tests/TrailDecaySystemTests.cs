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
            // По запросу владельца продукта распад ускорен в 2 раза относительно
            // legacy/burmolda_demo.html (maxD = 6 + trail.length*0.18): пороги
            // вдвое меньше (3 + trail.length*0.09); index плиты в трейле — 1.
            Assert.AreEqual(3f + 1 * 0.09f, tile.DecayThresholdSeconds.Value, 1e-5f);
        }

        [Test]
        public void Tick_SingleLargeStep_DestroysTileOnceThresholdExceeded()
        {
            var (grid, trail, decay) = CreateSystem();
            var target = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(target); // порог = 3.09с

            // accel = 1 + 3*0.025 = 1.075; накоплено = 3*1.075 = 3.225 >= 3.09 (порог).
            decay.Tick(3f);

            Assert.IsTrue(grid.GetOrCreateTile(target).IsDestroyed);
            Assert.IsTrue(decay.IsCurrentTileDestroyed);
        }

        [Test]
        public void Tick_AccumulatesAcrossMultipleCalls_DestroysOnlyOnceThresholdReached()
        {
            var (grid, trail, decay) = CreateSystem();
            var target = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(target); // порог = 3.09с

            // Тик 1: accel = 1.0375, накоплено = 1.5*1.0375 = 1.55625 (< 3.09).
            decay.Tick(1.5f);
            Assert.IsFalse(grid.GetOrCreateTile(target).IsDestroyed);

            // Тик 2: accel = 1.075, += 1.5*1.075 = 1.6125; итого 3.16875 (>= 3.09).
            decay.Tick(1.5f);
            Assert.IsTrue(grid.GetOrCreateTile(target).IsDestroyed);
        }

        [Test]
        public void Tick_ZigzagPath_EachTileDecaysIndependentlyByOwnThreshold()
        {
            var (grid, trail, decay) = CreateSystem();
            trail.TryAdvanceTo(new GridCoordinate(1, 3)); // index 1, порог 3.09
            trail.TryAdvanceTo(new GridCoordinate(2, 2)); // index 2, порог 3.18
            trail.TryAdvanceTo(new GridCoordinate(3, 3)); // index 3, порог 3.27

            // accel = 1 + 4*0.025 = 1.1; накоплено = 4*1.1 = 4.4 — превышает все три порога разом.
            decay.Tick(4f);

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

        [Test]
        public void Suspend_TickDuringSuspension_DoesNotAdvanceDecay()
        {
            // PRD раздел 12 (Тотем, Неуязвимость): "Трейл временно не распадается".
            var (grid, trail, decay) = CreateSystem();
            var target = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(target);

            decay.Suspend(5f);
            decay.Tick(1000f); // без Suspend гарантированно разрушило бы плиту

            Assert.IsFalse(grid.GetOrCreateTile(target).IsDestroyed);
        }

        [Test]
        public void Suspend_TickBeyondSuspensionDuration_ResumesDecay()
        {
            var (grid, trail, decay) = CreateSystem();
            var target = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(target);

            decay.Suspend(2f);
            decay.Tick(2f); // ровно на границе — приостановка исчерпана
            decay.Tick(1000f); // теперь распад снова тикает

            Assert.IsTrue(grid.GetOrCreateTile(target).IsDestroyed);
        }

        [Test]
        public void IsSuspended_ReflectsRemainingSuspension()
        {
            var (_, _, decay) = CreateSystem();

            Assert.IsFalse(decay.IsSuspended);

            decay.Suspend(3f);
            Assert.IsTrue(decay.IsSuspended);

            decay.Tick(3f);
            Assert.IsFalse(decay.IsSuspended);
        }
    }
}
