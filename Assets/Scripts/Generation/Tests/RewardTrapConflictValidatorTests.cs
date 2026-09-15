using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Generation.Tests
{
    // Задача «награда никогда не лежит на ловушке» (владелец, Спринт «Стены
    // вместо ловушек»): "Плита-источник Маны или Ключей не может
    // одновременно нести ловушку или её триггер." Эти тесты проверяют
    // область поражения каждого из пяти типов триггеров независимо от
    // рантайм-систем Movement — по одной граничной раскладке на тип, плюс
    // контрольные случаи "рядом, но вне области" (не должны считаться
    // конфликтом — иначе проверка была бы бесполезно консервативной).
    public class RewardTrapConflictValidatorTests
    {
        private static SegmentTileType[,] Grid(params string[] rows)
        {
            var height = rows.Length;
            var width = rows[0].Length;
            var tiles = new SegmentTileType[height, width];
            for (var r = 0; r < height; r++)
            for (var c = 0; c < width; c++)
                tiles[r, c] = rows[r][c] switch
                {
                    '.' => SegmentTileType.Open,
                    'm' => SegmentTileType.ManaSource,
                    'k' => SegmentTileType.KeySource,
                    'w' => SegmentTileType.ArrowWaveTrigger,
                    'x' => SegmentTileType.BombTrigger,
                    't' => SegmentTileType.BladeTactTrigger,
                    'r' => SegmentTileType.FallingRockTrigger,
                    'f' => SegmentTileType.LavaWaveTrigger,
                    _ => SegmentTileType.Open,
                };
            return tiles;
        }

        [Test]
        public void ArrowWaveTrigger_RewardInSameRow_IsConflict()
        {
            // ArrowWaveTrapSystem продвигается столбец за столбцом от края
            // до края заявленного ряда (по умолчанию — ряд самого триггера)
            // — весь ряд рано или поздно опасен, независимо от расстояния
            // по столбцу до самого триггера.
            // Шаблон должен занимать [5, 8] рядов (PRD v7 §21) — три
            // добавленных открытых ряда не участвуют в проверке, только
            // добивают раскладку до минимума.
            var template = new SegmentTemplate("t", 1, SegmentRewardTag.Mana, Grid(
                ".....",
                "w...m",
                ".....",
                ".....",
                "....."));

            var conflicts = RewardTrapConflictValidator.FindConflicts(template);

            Assert.AreEqual(1, conflicts.Count);
            Assert.AreEqual(SegmentTileType.ManaSource, conflicts[0].RewardType);
            Assert.AreEqual(SegmentTileType.ArrowWaveTrigger, conflicts[0].TriggerType);
        }

        [Test]
        public void ArrowWaveTrigger_RewardInDifferentRow_IsNotConflict()
        {
            var template = new SegmentTemplate("t", 1, SegmentRewardTag.Mana, Grid(
                ".....",
                "w....",
                "....m",
                ".....",
                "....."));

            Assert.IsEmpty(RewardTrapConflictValidator.FindConflicts(template));
        }

        [Test]
        public void BladeTactTrigger_RewardInSameRow_IsConflict()
        {
            // BladeTactTrapSystem — симметричные кольца от краёв к центру
            // (ComputeRingColumns) — покрывают весь ряд целиком для любой
            // ширины сетки.
            var template = new SegmentTemplate("t", 1, SegmentRewardTag.Keys, Grid(
                ".....",
                "t...k",
                ".....",
                ".....",
                "....."));

            var conflicts = RewardTrapConflictValidator.FindConflicts(template);

            Assert.AreEqual(1, conflicts.Count);
            Assert.AreEqual(SegmentTileType.BladeTactTrigger, conflicts[0].TriggerType);
        }

        [Test]
        public void BombTrigger_RewardWithinRadius_IsConflict()
        {
            // BombTrapSystem.ComputeBlastArea — квадрат радиуса RadiusTiles
            // (по умолчанию 1, "восемь соседей плюс сама плита-триггер").
            var savedRadius = BombTrapSystem.RadiusTiles;
            try
            {
                BombTrapSystem.RadiusTiles = 1;
                var template = new SegmentTemplate("t", 1, SegmentRewardTag.Mana, Grid(
                    ".....",
                    ".xm..",
                    ".....",
                    ".....",
                    "....."));

                var conflicts = RewardTrapConflictValidator.FindConflicts(template);

                Assert.AreEqual(1, conflicts.Count);
                Assert.AreEqual(SegmentTileType.BombTrigger, conflicts[0].TriggerType);
            }
            finally
            {
                BombTrapSystem.RadiusTiles = savedRadius;
            }
        }

        [Test]
        public void BombTrigger_RewardOutsideRadius_IsNotConflict()
        {
            var savedRadius = BombTrapSystem.RadiusTiles;
            try
            {
                BombTrapSystem.RadiusTiles = 1;
                var template = new SegmentTemplate("t", 1, SegmentRewardTag.Mana, Grid(
                    ".....",
                    "x...m",
                    ".....",
                    ".....",
                    "....."));

                Assert.IsEmpty(RewardTrapConflictValidator.FindConflicts(template));
            }
            finally
            {
                BombTrapSystem.RadiusTiles = savedRadius;
            }
        }

        [Test]
        public void LavaWaveTrigger_RewardInEarlierRowWithinMaxRows_IsConflict()
        {
            // LavaWaveTrapSystem: ряд триггера и до MaxRows-1 рядов НАЗАД
            // (меньший индекс ряда) становятся лавой, весь ряд целиком.
            var savedMaxRows = LavaWaveTrapSystem.MaxRows;
            try
            {
                LavaWaveTrapSystem.MaxRows = 6;
                var template = new SegmentTemplate("t", 1, SegmentRewardTag.Mana, Grid(
                    "..m..",
                    ".....",
                    "..f..",
                    ".....",
                    "....."));

                var conflicts = RewardTrapConflictValidator.FindConflicts(template);

                Assert.AreEqual(1, conflicts.Count);
                Assert.AreEqual(SegmentTileType.LavaWaveTrigger, conflicts[0].TriggerType);
            }
            finally
            {
                LavaWaveTrapSystem.MaxRows = savedMaxRows;
            }
        }

        [Test]
        public void LavaWaveTrigger_RewardInLaterRow_IsNotConflict()
        {
            // Волна идёт НАЗАД (к меньшему Row) — награда ВПЕРЕДИ триггера
            // (больший Row, ближе к выходу) вне зоны поражения.
            var template = new SegmentTemplate("t", 1, SegmentRewardTag.Mana, Grid(
                "..f..",
                ".....",
                "..m..",
                ".....",
                "....."));

            Assert.IsEmpty(RewardTrapConflictValidator.FindConflicts(template));
        }

        [Test]
        public void LavaWaveTrigger_RewardBeyondMaxRows_IsNotConflict()
        {
            var savedMaxRows = LavaWaveTrapSystem.MaxRows;
            try
            {
                LavaWaveTrapSystem.MaxRows = 2; // ряд триггера + 1 назад, не больше
                var template = new SegmentTemplate("t", 1, SegmentRewardTag.Mana, Grid(
                    "..m..",
                    ".....",
                    ".....",
                    "..f..",
                    "....."));

                Assert.IsEmpty(RewardTrapConflictValidator.FindConflicts(template));
            }
            finally
            {
                LavaWaveTrapSystem.MaxRows = savedMaxRows;
            }
        }

        [Test]
        public void FallingRockTrigger_NeverConflicts_SelfOnlyAreaCannotOverlapReward()
        {
            // Камень падает только на саму плиту-триггер — плита не может
            // одновременно быть триггером и наградой (SegmentTileType — одно
            // значение на клетку), поэтому даже плотная раскладка с
            // триггерами вплотную к наградам не должна давать конфликтов.
            var template = new SegmentTemplate("t", 1, SegmentRewardTag.Mana, Grid(
                "rmrmr",
                "mrmrm",
                "rmrmr",
                "mrmrm",
                "rmrmr"));

            Assert.IsEmpty(RewardTrapConflictValidator.FindConflicts(template));
        }

        [Test]
        public void WouldEndangerAnyReward_MatchesFindConflicts_ForCandidateOpenCell()
        {
            var savedRadius = BombTrapSystem.RadiusTiles;
            try
            {
                BombTrapSystem.RadiusTiles = 1;
                var template = new SegmentTemplate("t", 1, SegmentRewardTag.Mana, Grid(
                    ".....",
                    "..m..",
                    ".....",
                    ".....",
                    "....."));

                // Кандидатная плита (1,1) — сосед Маны на (1,2): Бомба задела бы её, Падающий камень — нет.
                Assert.IsTrue(RewardTrapConflictValidator.WouldEndangerAnyReward(template, 1, 1, SegmentTileType.BombTrigger));
                Assert.IsFalse(RewardTrapConflictValidator.WouldEndangerAnyReward(template, 1, 1, SegmentTileType.FallingRockTrigger));
                // Кандидатная плита (1,0) — тот же ряд, что Мана: Стрела/Лезвия задели бы её.
                Assert.IsTrue(RewardTrapConflictValidator.WouldEndangerAnyReward(template, 1, 0, SegmentTileType.ArrowWaveTrigger));
                Assert.IsTrue(RewardTrapConflictValidator.WouldEndangerAnyReward(template, 1, 0, SegmentTileType.BladeTactTrigger));
            }
            finally
            {
                BombTrapSystem.RadiusTiles = savedRadius;
            }
        }

        [Test]
        public void FindConflicts_TemplateWithNoRewardsOrTriggers_ReturnsEmpty()
        {
            var template = new SegmentTemplate("t", 1, SegmentRewardTag.Coins, Grid(
                ".....",
                ".....",
                ".....",
                ".....",
                "....."));

            Assert.IsEmpty(RewardTrapConflictValidator.FindConflicts(template));
        }
    }
}
