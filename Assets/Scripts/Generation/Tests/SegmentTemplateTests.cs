using System;
using NUnit.Framework;

namespace Burmalda.Generation.Tests
{
    public class SegmentTemplateTests
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
        public void Constructor_ValidTemplate_ExposesMetadata()
        {
            var tiles = OpenRows(5);
            var template = new SegmentTemplate("empty-corridor", 1, SegmentRewardTag.Coins, tiles);

            Assert.AreEqual("empty-corridor", template.Name);
            Assert.AreEqual(1, template.DifficultyTier);
            Assert.AreEqual(SegmentRewardTag.Coins, template.RewardTag);
            Assert.AreEqual(5, template.RowCount);
            Assert.AreEqual(Width, template.Width);
        }

        [Test]
        public void TileAt_ReturnsValueFromSourceArray()
        {
            var tiles = OpenRows(5);
            tiles[2, 1] = SegmentTileType.Blocked;
            var template = new SegmentTemplate("t", 1, SegmentRewardTag.Coins, tiles);

            Assert.AreEqual(SegmentTileType.Blocked, template.TileAt(2, 1));
            Assert.AreEqual(SegmentTileType.Open, template.TileAt(0, 0));
        }

        [TestCase(0)]
        [TestCase(6)]
        public void Constructor_DifficultyTierOutOfRange_Throws(int difficultyTier)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new SegmentTemplate("t", difficultyTier, SegmentRewardTag.Coins, OpenRows(5)));
        }

        [TestCase(1)]
        [TestCase(5)]
        public void Constructor_DifficultyTierAtBounds_DoesNotThrow(int difficultyTier)
        {
            Assert.DoesNotThrow(() =>
                new SegmentTemplate("t", difficultyTier, SegmentRewardTag.Coins, OpenRows(5)));
        }

        [TestCase(4)]
        [TestCase(9)]
        public void Constructor_RowCountOutOfRange_Throws(int rowCount)
        {
            Assert.Throws<ArgumentException>(() =>
                new SegmentTemplate("t", 1, SegmentRewardTag.Coins, OpenRows(rowCount)));
        }

        [TestCase(5)]
        [TestCase(8)]
        public void Constructor_RowCountAtBounds_DoesNotThrow(int rowCount)
        {
            Assert.DoesNotThrow(() =>
                new SegmentTemplate("t", 1, SegmentRewardTag.Coins, OpenRows(rowCount)));
        }

        [Test]
        public void Constructor_NullTiles_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SegmentTemplate("t", 1, SegmentRewardTag.Coins, null));
        }

        [TestCase(SegmentTileType.ExplosiveTrigger)]
        [TestCase(SegmentTileType.TimedTrapArrowTrigger)]
        [TestCase(SegmentTileType.TimedTrapBladeTrigger)]
        public void Constructor_TriggerOnLastRow_Throws(SegmentTileType triggerType)
        {
            var tiles = OpenRows(5);
            tiles[4, 0] = triggerType; // последний ряд (индекс RowCount-1) — цель вышла бы за пределы шаблона

            var ex = Assert.Throws<ArgumentException>(() =>
                new SegmentTemplate("t", 1, SegmentRewardTag.Coins, tiles));
            StringAssert.Contains("последн", ex.Message);
        }

        [TestCase(SegmentTileType.ExplosiveTrigger)]
        [TestCase(SegmentTileType.TimedTrapArrowTrigger)]
        [TestCase(SegmentTileType.TimedTrapBladeTrigger)]
        public void Constructor_TriggerNotOnLastRow_DoesNotThrow(SegmentTileType triggerType)
        {
            var tiles = OpenRows(5);
            tiles[3, 0] = triggerType; // предпоследний ряд — цель (4,0) внутри шаблона

            Assert.DoesNotThrow(() => new SegmentTemplate("t", 1, SegmentRewardTag.Coins, tiles));
        }

        [Test]
        public void Constructor_LeverGateWithoutLever_Throws()
        {
            var tiles = OpenRows(5);
            tiles[2, 2] = SegmentTileType.LeverGate;

            var ex = Assert.Throws<ArgumentException>(() =>
                new SegmentTemplate("t", 1, SegmentRewardTag.Artifact, tiles));
            StringAssert.Contains("Lever", ex.Message);
        }

        [Test]
        public void Constructor_LeverWithGate_DoesNotThrow()
        {
            var tiles = OpenRows(5);
            tiles[2, 0] = SegmentTileType.Lever;
            tiles[2, 2] = SegmentTileType.LeverGate;

            Assert.DoesNotThrow(() => new SegmentTemplate("t", 1, SegmentRewardTag.Artifact, tiles));
        }

        [Test]
        public void Constructor_MoreThanOneLever_Throws()
        {
            var tiles = OpenRows(5);
            tiles[1, 0] = SegmentTileType.Lever;
            tiles[3, 0] = SegmentTileType.Lever;
            tiles[2, 2] = SegmentTileType.LeverGate;

            var ex = Assert.Throws<ArgumentException>(() =>
                new SegmentTemplate("t", 1, SegmentRewardTag.Artifact, tiles));
            StringAssert.Contains("Lever", ex.Message);
        }
    }
}
