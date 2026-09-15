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
        public void FallingRockTrigger_RewardOneRowAhead_IsConflict()
        {
            // Задача 4 спринта «Стены вместо ловушек» («падающий камень:
            // новая спецификация»): камень падает на плиту ВПЕРЕДИ, не на
            // саму плиту-триггер — раньше конфликт был физически
            // невозможен (self-only), теперь возможен.
            // Шаблон должен занимать [5, 8] рядов (PRD v7 §21) — добавленные
            // открытые ряды не участвуют в проверке.
            var template = new SegmentTemplate("t", 1, SegmentRewardTag.Mana, Grid(
                "..r..",
                "..m..",
                ".....",
                ".....",
                "....."));

            var conflicts = RewardTrapConflictValidator.FindConflicts(template);

            Assert.AreEqual(1, conflicts.Count);
            Assert.AreEqual(SegmentTileType.FallingRockTrigger, conflicts[0].TriggerType);
        }

        [Test]
        public void FallingRockTrigger_RewardNotDirectlyAhead_IsNotConflict()
        {
            // Область поражения — ровно одна клетка (ряд+1, тот же
            // столбец), не соседний столбец и не сама плита-триггер.
            var template = new SegmentTemplate("t", 1, SegmentRewardTag.Mana, Grid(
                "..r..",
                ".m...",
                ".....",
                ".....",
                "....."));

            Assert.IsEmpty(RewardTrapConflictValidator.FindConflicts(template));
        }

        [Test]
        public void FallingRockTrigger_OnLastRow_TargetBeyondTemplate_IsNotConflict()
        {
            // Цель за пределами шаблона (следующий сегмент) — эта проверка
            // консервативно её не видит, известное ограничение (тот же
            // принцип, что уже принят для LavaWave через границы сегментов).
            // Триггер намеренно на последнем ряду 5-рядного шаблона —
            // добавленные открытые ряды идут ПЕРЕД ним, а не после, иначе
            // "последний ряд" перестал бы им быть.
            var template = new SegmentTemplate("t", 1, SegmentRewardTag.Mana, Grid(
                ".....",
                ".....",
                ".....",
                ".....",
                "..r.."));

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

                // Кандидатная плита (1,1) — сосед Маны на (1,2): Бомба задела бы её (радиус), Падающий камень — нет (цель — ряд ниже, (2,1), а не соседний столбец).
                Assert.IsTrue(RewardTrapConflictValidator.WouldEndangerAnyReward(template, 1, 1, SegmentTileType.BombTrigger));
                Assert.IsFalse(RewardTrapConflictValidator.WouldEndangerAnyReward(template, 1, 1, SegmentTileType.FallingRockTrigger));
                // Кандидатная плита (0,2) — тот же столбец, на ряд выше Маны: Падающий камень целится ровно в неё.
                Assert.IsTrue(RewardTrapConflictValidator.WouldEndangerAnyReward(template, 0, 2, SegmentTileType.FallingRockTrigger));
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
