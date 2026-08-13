using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class TunnelGridRevealTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail) CreateTrail(GridCoordinate start)
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, start);
            return (grid, trail);
        }

        private static bool RowFullyMaterialized(TunnelGrid grid, int row)
        {
            for (var column = 0; column < Width; column++)
                if (!grid.TryGetTile(new GridCoordinate(row, column), out _)) return false;
            return true;
        }

        [Test]
        public void Constructor_RevealsCurrentRowAndRowsAheadAcrossFullWidth()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            using var reveal = new TunnelGridReveal(grid, trail);

            for (var row = 0; row <= TunnelGridReveal.RowsAheadOfPlayer; row++)
                Assert.IsTrue(RowFullyMaterialized(grid, row), $"ряд {row} должен быть полностью материализован");
        }

        [Test]
        public void Constructor_DoesNotRevealBeyondRowsAheadWindow()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            using var reveal = new TunnelGridReveal(grid, trail);

            var beyondRow = TunnelGridReveal.RowsAheadOfPlayer + 1;
            Assert.IsFalse(grid.TryGetTile(new GridCoordinate(beyondRow, 2), out _));
        }

        [Test]
        public void OnPositionChanged_TrailAdvances_RevealsFurtherRowsAhead()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            using var reveal = new TunnelGridReveal(grid, trail);

            for (var row = 1; row <= 3; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            var expectedRevealedThrough = 3 + TunnelGridReveal.RowsAheadOfPlayer;
            Assert.IsTrue(RowFullyMaterialized(grid, expectedRevealedThrough));
            Assert.IsFalse(grid.TryGetTile(new GridCoordinate(expectedRevealedThrough + 1, 2), out _));
        }

        [Test]
        public void OnPositionChanged_TileAlreadyRevealed_DoesNotRefireMaterialized()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            using var reveal = new TunnelGridReveal(grid, trail);

            var materializedCount = 0;
            grid.TileMaterialized += _ => materializedCount++;

            trail.TryAdvanceTo(new GridCoordinate(1, 2)); // (1,2) уже был материализован конструктором reveal

            Assert.AreEqual(0, materializedCount);
        }

        [Test]
        public void Dispose_StopsRevealingOnFurtherPositionChanges()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var reveal = new TunnelGridReveal(grid, trail);
            reveal.Dispose();

            for (var row = 1; row <= TunnelGridReveal.RowsAheadOfPlayer + 5; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            var farRow = TunnelGridReveal.RowsAheadOfPlayer + 4; // за пределами исходного окна, до которого дошли шагами, а не reveal
            Assert.IsFalse(RowFullyMaterialized(grid, farRow));
        }
    }
}
