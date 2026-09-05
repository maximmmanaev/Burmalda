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

        // Владелец, 2026-09-05 «оставить только пять новых ловушек»: раньше
        // здесь были Constructor_TriggerOnLastRow_Throws/_TriggerNotOnLastRow_DoesNotThrow
        // — проверка ValidateNoTriggerOnLastRow целилась в ExplosiveTrigger/
        // TimedTrapArrowTrigger/TimedTrapBladeTrigger (единственные типы,
        // чья цель была "следующий ряд, тот же столбец"). Все три удалены,
        // а ни один из пяти оставшихся типов ловушек не целится за пределы
        // своего же ряда (см. SegmentTemplate.cs) — сама проверка удалена
        // как мёртвая вместе с ними, тесты соответственно тоже.

        [Test]
        public void Constructor_LeverGateWithoutLever_Throws()
        {
            var tiles = OpenRows(5);
            tiles[2, 2] = SegmentTileType.LeverGate;

            var ex = Assert.Throws<ArgumentException>(() =>
                new SegmentTemplate("t", 1, SegmentRewardTag.Artifact, tiles));
            StringAssert.Contains("Lever", ex.Message);
        }

        // Issue #193 (владелец, 2026-09-01): "рычаг существует только рядом
        // со своими Воротами — отдельно стоящий рычаг вне связки бессмыслен,
        // его никто никогда не найдёт" — обратная сторона проверки выше.
        [Test]
        public void Constructor_LeverWithoutLeverGate_Throws()
        {
            var tiles = OpenRows(5);
            tiles[2, 0] = SegmentTileType.Lever;

            var ex = Assert.Throws<ArgumentException>(() =>
                new SegmentTemplate("t", 1, SegmentRewardTag.Artifact, tiles));
            StringAssert.Contains("LeverGate", ex.Message);
        }

        [Test]
        public void Constructor_LeverWithGate_DoesNotThrow()
        {
            var tiles = OpenRows(5);
            tiles[2, 0] = SegmentTileType.Lever;
            tiles[2, 2] = SegmentTileType.LeverGate;

            Assert.DoesNotThrow(() => new SegmentTemplate("t", 1, SegmentRewardTag.Artifact, tiles));
        }

        // issue «размер награды за Воротами» (владелец, 2026-09-04).
        [Test]
        public void Constructor_GateVaultKeySourceWithoutPurchases_Throws()
        {
            var tiles = OpenRows(5);
            tiles[2, 0] = SegmentTileType.GateVaultKeySource;

            var ex = Assert.Throws<ArgumentException>(() =>
                new SegmentTemplate("t", 1, SegmentRewardTag.Keys, tiles));
            StringAssert.Contains("GateVaultPurchases", ex.Message);
        }

        [Test]
        public void Constructor_GateVaultPurchasesWithoutKeySource_Throws()
        {
            var tiles = OpenRows(5);

            var ex = Assert.Throws<ArgumentException>(() =>
                new SegmentTemplate("t", 1, SegmentRewardTag.Keys, tiles, gateVaultPurchases: 1.0));
            StringAssert.Contains("GateVaultKeySource", ex.Message);
        }

        [Test]
        public void Constructor_MultipleGateVaultKeySource_Throws()
        {
            var tiles = OpenRows(5);
            tiles[1, 0] = SegmentTileType.GateVaultKeySource;
            tiles[2, 0] = SegmentTileType.GateVaultKeySource;

            var ex = Assert.Throws<ArgumentException>(() =>
                new SegmentTemplate("t", 1, SegmentRewardTag.Keys, tiles, gateVaultPurchases: 1.0));
            StringAssert.Contains("GateVaultKeySource", ex.Message);
        }

        [Test]
        public void Constructor_GateVaultKeySourceWithPurchases_DoesNotThrow_ExposesGateVaultPurchases()
        {
            var tiles = OpenRows(5);
            tiles[2, 0] = SegmentTileType.GateVaultKeySource;

            var template = new SegmentTemplate("t", 1, SegmentRewardTag.Keys, tiles, gateVaultPurchases: 1.5);

            Assert.AreEqual(1.5, template.GateVaultPurchases);
        }

        [Test]
        public void Constructor_NoGateVault_GateVaultPurchasesIsNull()
        {
            var template = new SegmentTemplate("t", 1, SegmentRewardTag.Coins, OpenRows(5));

            Assert.IsFalse(template.GateVaultPurchases.HasValue);
        }

        // Владелец, 2026-09-05 «оставить только пять новых ловушек»: раньше
        // здесь были Constructor_ExplosiveTriggerTargetsConflictingRole_Throws/
        // _ExplosiveTriggerTargetsManaOrKeySource_DoesNotThrow/
        // _TimedTrapTriggerTargetsConflictingRole_DoesNotThrow/
        // _TriggerTargetsLeverGate_Throws/_TriggerTargetsNonExclusiveRole_DoesNotThrow
        // — проверяли ValidateTriggerTargetsDoNotConflict/IsExclusiveRole,
        // специфичные для ExplosiveTrigger (единственный тип с целью вне
        // себя самого — "следующий ряд, тот же столбец"). Тип и проверка
        // удалены вместе (см. SegmentTemplate.cs) — конфликт "триггер
        // целится в структурную роль" для оставшихся пяти типов ловушек не
        // воспроизводим.

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
