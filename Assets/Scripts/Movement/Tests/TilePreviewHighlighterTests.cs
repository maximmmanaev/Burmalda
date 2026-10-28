using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class TilePreviewHighlighterTests
    {
        [Test]
        public void IsLethalContour_LavaTile_ReturnsTrue()
        {
            var grid = new TunnelGrid(5);
            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 2));
            tile.TransitionToLethalTrap(LethalTrapType.Lava);

            Assert.IsTrue(TilePreviewHighlighter.IsLethalContour(tile));
        }

        [Test]
        public void IsLethalContour_DecayedTile_ReturnsTrue()
        {
            var grid = new TunnelGrid(5);
            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 2));
            tile.BeginDecay(1f);
            tile.AdvanceDecay(2f);

            Assert.IsTrue(TilePreviewHighlighter.IsLethalContour(tile));
        }

        [Test]
        public void IsLethalContour_PlainTile_ReturnsFalse()
        {
            var grid = new TunnelGrid(5);
            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 2));

            Assert.IsFalse(TilePreviewHighlighter.IsLethalContour(tile));
        }
    }
}
