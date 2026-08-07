using NUnit.Framework;

namespace Burmalda.Core.Tests
{
    public class TileTests
    {
        [Test]
        public void NewTile_DecayNotBegun_IsNotDestroyedAndThresholdIsNull()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            Assert.IsFalse(tile.IsDestroyed);
            Assert.IsNull(tile.DecayThresholdSeconds);
            Assert.AreEqual(0f, tile.DecayProgress01);
        }

        [Test]
        public void AdvanceDecay_WithoutBeginDecay_IsNoOp()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.AdvanceDecay(1000f);

            Assert.IsFalse(tile.IsDestroyed);
        }

        [Test]
        public void AdvanceDecay_BelowThreshold_DoesNotDestroyTile()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            tile.BeginDecay(6f);

            tile.AdvanceDecay(5f);

            Assert.IsFalse(tile.IsDestroyed);
            Assert.AreEqual(5f, tile.DecayElapsedSeconds);
        }

        [Test]
        public void AdvanceDecay_ReachesThreshold_DestroysTile()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            tile.BeginDecay(6f);

            tile.AdvanceDecay(6f);

            Assert.IsTrue(tile.IsDestroyed);
        }

        [Test]
        public void AdvanceDecay_ExceedsThreshold_DestroysTile()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            tile.BeginDecay(6f);

            tile.AdvanceDecay(9f);

            Assert.IsTrue(tile.IsDestroyed);
        }

        [Test]
        public void AdvanceDecay_AfterDestroyed_DoesNotAccumulateFurther()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            tile.BeginDecay(6f);
            tile.AdvanceDecay(6f);

            tile.AdvanceDecay(100f);

            Assert.IsTrue(tile.IsDestroyed);
            Assert.AreEqual(6f, tile.DecayElapsedSeconds);
        }

        [Test]
        public void BeginDecay_CalledTwice_KeepsFirstThreshold()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.BeginDecay(6f);
            tile.BeginDecay(999f);

            Assert.AreEqual(6f, tile.DecayThresholdSeconds);
        }

        [Test]
        public void DecayProgress01_HalfwayToThreshold_ReturnsHalf()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            tile.BeginDecay(10f);

            tile.AdvanceDecay(5f);

            Assert.AreEqual(0.5f, tile.DecayProgress01);
        }

        [Test]
        public void DecayProgress01_AfterDestroyed_IsClampedToOne()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            tile.BeginDecay(6f);

            tile.AdvanceDecay(9f);

            Assert.AreEqual(1f, tile.DecayProgress01);
        }

        [Test]
        public void NewTile_IsNotBlocked()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            Assert.IsFalse(tile.IsBlocked);
        }

        [Test]
        public void MarkBlocked_SetsIsBlockedTrue()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.MarkBlocked();

            Assert.IsTrue(tile.IsBlocked);
        }

        [Test]
        public void MarkBlocked_CalledTwice_StaysBlocked()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.MarkBlocked();
            tile.MarkBlocked();

            Assert.IsTrue(tile.IsBlocked);
        }
    }
}
