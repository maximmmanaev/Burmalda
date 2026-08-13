using System.Collections.Generic;
using NUnit.Framework;

namespace Burmalda.Core.Tests
{
    public class TunnelObstacleGeneratorTests
    {
        private static System.Func<float> Sequence(params float[] rolls)
        {
            var queue = new Queue<float>(rolls);
            return () => queue.Dequeue();
        }

        [Test]
        public void TileMaterialized_RollBelowBlockedThreshold_MarksTileBlocked()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(0f));

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsTrue(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
        }

        [Test]
        public void TileMaterialized_RollAtBlockedThreshold_MarksLethalTrapPit()
        {
            // Верхняя граница диапазона block — начало диапазона pit (полуинтервалы: [0, Blocked) block, [Blocked, Pit) pit).
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.BlockedThreshold));

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsFalse(tile.IsBlocked);
            Assert.AreEqual(LethalTrapType.Pit, tile.LethalTrap);
        }

        [Test]
        public void TileMaterialized_RollAtPitThreshold_MarksLethalTrapLava()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.PitThreshold));

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsFalse(tile.IsBlocked);
            Assert.AreEqual(LethalTrapType.Lava, tile.LethalTrap);
        }

        [Test]
        public void TileMaterialized_RollAtOrAboveLavaThreshold_TileStaysPassable()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.LavaThreshold));

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsFalse(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
        }

        [Test]
        public void TileMaterialized_RowZero_NeverMarkedRegardlessOfRoll()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(0f));

            var tile = grid.GetOrCreateTile(new GridCoordinate(0, 2));

            Assert.IsFalse(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
        }

        [Test]
        public void TileMaterialized_RowZero_DoesNotConsumeRandomRoll()
        {
            // Если бы ряд 0 всё равно бросал кубик, второй Dequeue() на пустой очереди упал бы с исключением.
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(0f));

            grid.GetOrCreateTile(new GridCoordinate(0, 2));
            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsTrue(tile.IsBlocked);
        }

        [Test]
        public void Dispose_StopsReactingToFurtherMaterializedTiles()
        {
            var grid = new TunnelGrid(5);
            var generator = new TunnelObstacleGenerator(grid, Sequence(0f));
            generator.Dispose();

            // После Dispose очередь бросков больше не трогается — исключения из пустой очереди не будет, плита останется непомеченной.
            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsFalse(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
        }
    }
}
