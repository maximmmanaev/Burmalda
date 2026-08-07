using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class GridTraceTrailTests
    {
        private static GridTraceTrail CreateTrail(GridCoordinate start)
        {
            var grid = new TunnelGrid(5);
            return new GridTraceTrail(grid, start);
        }

        [Test]
        public void Constructor_StartCoordinate_IsCurrentPositionAndOnlyPathEntry()
        {
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);

            Assert.AreEqual(start, trail.CurrentPosition);
            Assert.AreEqual(1, trail.Path.Count);
            Assert.AreEqual(start, trail.Path[0]);
        }

        [Test]
        public void CanAdvanceTo_AdjacentUnvisitedTile_ReturnsTrue()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));

            Assert.IsTrue(trail.CanAdvanceTo(new GridCoordinate(1, 2)));
        }

        [Test]
        public void CanAdvanceTo_NonAdjacentTile_ReturnsFalse()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));

            Assert.IsFalse(trail.CanAdvanceTo(new GridCoordinate(2, 2)));
        }

        [Test]
        public void CanAdvanceTo_OutOfGridBounds_ReturnsFalse()
        {
            var trail = CreateTrail(new GridCoordinate(0, 0));

            Assert.IsFalse(trail.CanAdvanceTo(new GridCoordinate(0, -1)));
            Assert.IsFalse(trail.CanAdvanceTo(new GridCoordinate(-1, 0)));
        }

        [Test]
        public void CanAdvanceTo_AlreadyVisitedTile_ReturnsFalse()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));
            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            // Прямой возврат назад — попытка снова встать на уже пройденную плиту.
            Assert.IsFalse(trail.CanAdvanceTo(new GridCoordinate(0, 2)));
        }

        [Test]
        public void TryAdvanceTo_ValidMove_AppendsToPathAndUpdatesCurrentPosition()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));
            var target = new GridCoordinate(1, 3);

            var advanced = trail.TryAdvanceTo(target);

            Assert.IsTrue(advanced);
            Assert.AreEqual(target, trail.CurrentPosition);
            Assert.AreEqual(2, trail.Path.Count);
        }

        [Test]
        public void TryAdvanceTo_InvalidMove_ReturnsFalseAndDoesNotMutatePath()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));

            var advanced = trail.TryAdvanceTo(new GridCoordinate(3, 3));

            Assert.IsFalse(advanced);
            Assert.AreEqual(1, trail.Path.Count);
        }

        [Test]
        public void TryAdvanceTo_ZigzagPathOverNeverVisitedTiles_AllStepsSucceed()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));

            Assert.IsTrue(trail.TryAdvanceTo(new GridCoordinate(1, 3)));
            Assert.IsTrue(trail.TryAdvanceTo(new GridCoordinate(2, 2)));
            Assert.IsTrue(trail.TryAdvanceTo(new GridCoordinate(3, 3)));
            Assert.AreEqual(4, trail.Path.Count);
        }
    }
}
