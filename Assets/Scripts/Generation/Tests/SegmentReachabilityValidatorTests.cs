using NUnit.Framework;

namespace Burmalda.Generation.Tests
{
    public class SegmentReachabilityValidatorTests
    {
        private const int Width = 3;

        private static SegmentTileType[,] OpenRows(int rowCount)
        {
            var tiles = new SegmentTileType[rowCount, Width];
            for (var r = 0; r < rowCount; r++)
                for (var c = 0; c < Width; c++)
                    tiles[r, c] = SegmentTileType.Open;
            return tiles;
        }

        [Test]
        public void IsTraversable_FullyOpenTemplate_ReturnsTrue()
        {
            var template = new SegmentTemplate("open", 1, SegmentRewardTag.Coins, OpenRows(5));
            Assert.IsTrue(SegmentReachabilityValidator.IsTraversable(template));
        }

        [Test]
        public void IsTraversable_StraightWallSplittingEntryFromExit_ReturnsFalse()
        {
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++) tiles[2, c] = SegmentTileType.Blocked; // сплошная стена поперёк — вход и выход разъединены

            var template = new SegmentTemplate("wall", 1, SegmentRewardTag.Coins, tiles);
            Assert.IsFalse(SegmentReachabilityValidator.IsTraversable(template));
        }

        [Test]
        public void IsTraversable_WallWithGap_ReturnsTrue()
        {
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++) tiles[2, c] = SegmentTileType.Blocked;
            tiles[2, 1] = SegmentTileType.Open; // проход в стене

            var template = new SegmentTemplate("gap", 1, SegmentRewardTag.Coins, tiles);
            Assert.IsTrue(SegmentReachabilityValidator.IsTraversable(template));
        }

        [Test]
        public void IsTraversable_AllEntryCellsBlocked_ReturnsFalse()
        {
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++) tiles[0, c] = SegmentTileType.Blocked;

            var template = new SegmentTemplate("no-entry", 1, SegmentRewardTag.Coins, tiles);
            Assert.IsFalse(SegmentReachabilityValidator.IsTraversable(template));
        }

        [Test]
        public void IsTraversable_AllExitCellsBlocked_ReturnsFalse()
        {
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++) tiles[4, c] = SegmentTileType.Blocked;

            var template = new SegmentTemplate("no-exit", 1, SegmentRewardTag.Coins, tiles);
            Assert.IsFalse(SegmentReachabilityValidator.IsTraversable(template));
        }

        [Test]
        public void IsTraversable_PitLavaAndTriggers_DoNotBlockPath()
        {
            // Опасные-но-не-заблокированные плиты (яма/лава/триггеры) —
            // проходимы для целей гарантии проходимости: игрок физически
            // МОЖЕТ пройти (рискуя), просто не должен. Заблокирован путь
            // считается только по Blocked/LeverGate.
            var tiles = OpenRows(5);
            tiles[1, 1] = SegmentTileType.Pit;
            tiles[2, 1] = SegmentTileType.Lava;
            tiles[3, 1] = SegmentTileType.TimedTrapArrowTrigger;

            var template = new SegmentTemplate("hazards", 1, SegmentRewardTag.Coins, tiles);
            Assert.IsTrue(SegmentReachabilityValidator.IsTraversable(template));
        }

        [Test]
        public void IsTraversable_OnlyPathRunsThroughLeverGate_ReturnsFalse()
        {
            // LeverGate закрыта по умолчанию (issue #51) — если единственный
            // путь идёт через неё, основной маршрут (не боковой) на деле
            // непроходим без активации рычага в другом месте шаблона.
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++) tiles[2, c] = SegmentTileType.Blocked;
            tiles[2, 1] = SegmentTileType.LeverGate;
            tiles[0, 0] = SegmentTileType.Lever; // рычаг где-то в шаблоне, не на пути к выходу

            var template = new SegmentTemplate("gate-only-path", 1, SegmentRewardTag.Artifact, tiles);
            Assert.IsFalse(SegmentReachabilityValidator.IsTraversable(template));
        }

        [Test]
        public void IsTraversable_DiagonalGapAroundWall_ReturnsTrue()
        {
            // Стена с диагональным обходом на краю (8-направленное соседство, как GridCoordinate.IsAdjacentTo).
            var tiles = OpenRows(5);
            tiles[2, 0] = SegmentTileType.Blocked;
            tiles[2, 1] = SegmentTileType.Blocked;
            // tiles[2,2] остаётся Open — диагональный проход из (1,1)/(1,2) в (3,2) и т.п.

            var template = new SegmentTemplate("diagonal", 1, SegmentRewardTag.Coins, tiles);
            Assert.IsTrue(SegmentReachabilityValidator.IsTraversable(template));
        }
    }
}
