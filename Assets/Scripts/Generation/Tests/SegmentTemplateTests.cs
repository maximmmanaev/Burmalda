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

        // Найдено на реальном билде (2026-09-01, живой забег до Яруса 6):
        // необработанный InvalidOperationException — триггер взрыва целился
        // в клетку, которая в том же шаблоне уже размечена как одна из этих
        // структурных ролей. ManaSource/KeySource сознательно НЕ в этом
        // списке — см. тест ниже.
        [TestCase(SegmentTileType.Blocked)]
        [TestCase(SegmentTileType.Pit)]
        [TestCase(SegmentTileType.Lava)]
        [TestCase(SegmentTileType.Altar)]
        [TestCase(SegmentTileType.Boss)]
        public void Constructor_ExplosiveTriggerTargetsConflictingRole_Throws(SegmentTileType targetType)
        {
            var tiles = OpenRows(5);
            tiles[2, 0] = SegmentTileType.ExplosiveTrigger;
            tiles[3, 0] = targetType; // (row+1, тот же столбец) — цель триггера

            var ex = Assert.Throws<ArgumentException>(() =>
                new SegmentTemplate("t", 1, SegmentRewardTag.Coins, tiles));
            StringAssert.Contains("целится", ex.Message);
        }

        // Владелец, продолжение той же задачи (2026-09-01): "триггер должен
        // уметь уничтожать ключ под собой" — шаблоны «выкуп»/«последний-
        // рывок» (Generation.SegmentTemplateCatalog) намеренно ставят
        // ExplosiveTrigger над ManaSource/KeySource. Core.Tile.
        // TransitionToLethalTrap (рантайм-переход) обслуживает это без
        // стража — эта проверка (этап авторинга) не должна мешать.
        [TestCase(SegmentTileType.ManaSource)]
        [TestCase(SegmentTileType.KeySource)]
        public void Constructor_ExplosiveTriggerTargetsManaOrKeySource_DoesNotThrow(SegmentTileType targetType)
        {
            var tiles = OpenRows(5);
            tiles[2, 0] = SegmentTileType.ExplosiveTrigger;
            tiles[3, 0] = targetType;

            Assert.DoesNotThrow(() => new SegmentTemplate("t", 1, SegmentRewardTag.Coins, tiles));
        }

        // Намеренно НЕ Throws: Movement.TimedTrapSystem активирует цель через
        // Tile.ArmTimedTrap, который НЕ вызывает GuardAgainstConflictingRole
        // вообще (см. её doc-комментарий в Tile.cs) — этот класс крэша для
        // Arrow/Blade физически невозможен, в отличие от ExplosiveTrigger
        // выше. Проверяет именно СУЖЕНИЕ ValidateTriggerTargetsDoNotConflict
        // до ExplosiveTrigger — не общий случай "все триггеры безопасны".
        [TestCase(SegmentTileType.TimedTrapArrowTrigger, SegmentTileType.ManaSource)]
        [TestCase(SegmentTileType.TimedTrapArrowTrigger, SegmentTileType.KeySource)]
        [TestCase(SegmentTileType.TimedTrapBladeTrigger, SegmentTileType.Pit)]
        [TestCase(SegmentTileType.TimedTrapBladeTrigger, SegmentTileType.Blocked)]
        public void Constructor_TimedTrapTriggerTargetsConflictingRole_DoesNotThrow(SegmentTileType triggerType, SegmentTileType targetType)
        {
            var tiles = OpenRows(5);
            tiles[2, 0] = triggerType;
            tiles[3, 0] = targetType;

            Assert.DoesNotThrow(() => new SegmentTemplate("t", 1, SegmentRewardTag.Coins, tiles));
        }

        [Test]
        public void Constructor_TriggerTargetsLeverGate_Throws()
        {
            // LeverGate требует Lever где-то в шаблоне (иначе своя, другая
            // валидация упадёт первой) — Lever ставим отдельно от триггера/цели.
            var tiles = OpenRows(5);
            tiles[0, 1] = SegmentTileType.Lever;
            tiles[2, 0] = SegmentTileType.ExplosiveTrigger;
            tiles[3, 0] = SegmentTileType.LeverGate;

            var ex = Assert.Throws<ArgumentException>(() =>
                new SegmentTemplate("t", 1, SegmentRewardTag.Coins, tiles));
            StringAssert.Contains("целится", ex.Message);
        }

        [TestCase(SegmentTileType.Open)]
        [TestCase(SegmentTileType.ExplosiveTrigger)]
        [TestCase(SegmentTileType.TimedTrapArrowTrigger)]
        [TestCase(SegmentTileType.TimedTrapBladeTrigger)]
        public void Constructor_TriggerTargetsNonExclusiveRole_DoesNotThrow(SegmentTileType targetType)
        {
            // Цель — обычный пол или сама тоже триггер (не гейтится этим
            // инвариантом, см. IsExclusiveRole) — не должно падать.
            var tiles = OpenRows(5);
            tiles[2, 0] = SegmentTileType.ExplosiveTrigger;
            tiles[3, 0] = targetType;

            Assert.DoesNotThrow(() => new SegmentTemplate("t", 1, SegmentRewardTag.Coins, tiles));
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
