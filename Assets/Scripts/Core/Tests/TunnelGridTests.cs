using System;
using System.Linq;
using NUnit.Framework;

namespace Burmalda.Core.Tests
{
    public class TunnelGridTests
    {
        [Test]
        public void Contains_ColumnWithinWidthAndNonNegativeRow_ReturnsTrue()
        {
            var grid = new TunnelGrid(5);

            Assert.IsTrue(grid.Contains(new GridCoordinate(0, 0)));
            Assert.IsTrue(grid.Contains(new GridCoordinate(100, 4)));
        }

        [TestCase(0, -1)]
        [TestCase(0, 5)]
        [TestCase(-1, 0)]
        public void Contains_OutOfBoundsCoordinate_ReturnsFalse(int row, int column)
        {
            var grid = new TunnelGrid(5);

            Assert.IsFalse(grid.Contains(new GridCoordinate(row, column)));
        }

        [Test]
        public void GetOrCreateTile_SameCoordinateTwice_ReturnsSameInstance()
        {
            var grid = new TunnelGrid(5);
            var coordinate = new GridCoordinate(2, 2);

            var first = grid.GetOrCreateTile(coordinate);
            var second = grid.GetOrCreateTile(coordinate);

            Assert.AreSame(first, second);
        }

        [Test]
        public void GetOrCreateTile_OutOfBoundsCoordinate_Throws()
        {
            var grid = new TunnelGrid(5);

            Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetOrCreateTile(new GridCoordinate(0, -1)));
        }

        [Test]
        public void GetNeighbors_CornerCoordinate_ReturnsOnlyInBoundsNeighbors()
        {
            var grid = new TunnelGrid(5);

            var neighbors = grid.GetNeighbors(new GridCoordinate(0, 0)).ToList();

            // (0,0) в углу тоннеля: рядов выше нет (row<0 вне сетки), столбца
            // левее нет (column<0 вне сетки) — валидны только 3 соседа.
            Assert.AreEqual(3, neighbors.Count);
            CollectionAssert.AreEquivalent(new[]
            {
                new GridCoordinate(0, 1),
                new GridCoordinate(1, 0),
                new GridCoordinate(1, 1),
            }, neighbors);
        }

        [Test]
        public void GetNeighbors_MiddleCoordinate_ReturnsAllEightNeighbors()
        {
            var grid = new TunnelGrid(5);

            var neighbors = grid.GetNeighbors(new GridCoordinate(3, 2)).ToList();

            Assert.AreEqual(8, neighbors.Count);
        }

        [Test]
        public void TryGetTile_NeverMaterialized_ReturnsFalseWithoutCreatingTile()
        {
            var grid = new TunnelGrid(5);
            var fired = false;
            grid.TileMaterialized += _ => fired = true;

            var found = grid.TryGetTile(new GridCoordinate(2, 2), out var tile);

            Assert.IsFalse(found);
            Assert.IsNull(tile);
            Assert.IsFalse(fired, "TryGetTile не должен материализовывать плиту как побочный эффект");
        }

        [Test]
        public void TryGetTile_AlreadyMaterialized_ReturnsTrueWithSameInstance()
        {
            var grid = new TunnelGrid(5);
            var coordinate = new GridCoordinate(2, 2);
            var created = grid.GetOrCreateTile(coordinate);

            var found = grid.TryGetTile(coordinate, out var tile);

            Assert.IsTrue(found);
            Assert.AreSame(created, tile);
        }

        [Test]
        public void TileMaterialized_FirstGetOrCreateTile_FiresWithNewTile()
        {
            var grid = new TunnelGrid(5);
            var coordinate = new GridCoordinate(2, 2);
            Tile materialized = null;
            grid.TileMaterialized += tile => materialized = tile;

            var created = grid.GetOrCreateTile(coordinate);

            Assert.AreSame(created, materialized);
        }

        [Test]
        public void TileMaterialized_SameCoordinateTwice_FiresOnlyOnce()
        {
            var grid = new TunnelGrid(5);
            var coordinate = new GridCoordinate(2, 2);
            var firedCount = 0;
            grid.TileMaterialized += _ => firedCount++;

            grid.GetOrCreateTile(coordinate);
            grid.GetOrCreateTile(coordinate);

            Assert.AreEqual(1, firedCount);
        }
    }
}
