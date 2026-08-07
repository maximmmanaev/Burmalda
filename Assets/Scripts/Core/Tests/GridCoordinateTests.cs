using NUnit.Framework;

namespace Burmalda.Core.Tests
{
    public class GridCoordinateTests
    {
        [TestCase(0, 1)]
        [TestCase(0, -1)]
        [TestCase(1, 0)]
        [TestCase(-1, 0)]
        [TestCase(1, 1)]
        [TestCase(1, -1)]
        [TestCase(-1, 1)]
        [TestCase(-1, -1)]
        public void IsAdjacentTo_AllEightSurroundingCells_ReturnsTrue(int deltaRow, int deltaColumn)
        {
            var origin = new GridCoordinate(3, 3);
            var neighbor = new GridCoordinate(3 + deltaRow, 3 + deltaColumn);

            Assert.IsTrue(origin.IsAdjacentTo(neighbor));
        }

        [Test]
        public void IsAdjacentTo_SameCoordinate_ReturnsFalse()
        {
            var origin = new GridCoordinate(2, 2);

            Assert.IsFalse(origin.IsAdjacentTo(origin));
        }

        [TestCase(0, 2)]
        [TestCase(2, 0)]
        [TestCase(2, 2)]
        public void IsAdjacentTo_MoreThanOneCellAway_ReturnsFalse(int deltaRow, int deltaColumn)
        {
            var origin = new GridCoordinate(5, 5);
            var farAway = new GridCoordinate(5 + deltaRow, 5 + deltaColumn);

            Assert.IsFalse(origin.IsAdjacentTo(farAway));
        }

        [Test]
        public void Equals_SameRowAndColumn_ReturnsTrue()
        {
            Assert.AreEqual(new GridCoordinate(4, 1), new GridCoordinate(4, 1));
        }
    }
}
