using System.Collections.Generic;
using System.Linq;
using System.Text;
using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Generation.Tests
{
    public class YarusGenerationTests
    {
        private const int Width = 5;

        private static SegmentTemplate Make(string name, params string[] rows)
        {
            var tiles = new SegmentTileType[rows.Length, Width];
            for (var r = 0; r < rows.Length; r++)
                for (var c = 0; c < Width; c++)
                    tiles[r, c] = rows[r][c] switch
                    {
                        '#' => SegmentTileType.Blocked,
                        'l' => SegmentTileType.Lava,
                        'r' => SegmentTileType.FallingRockTrigger,
                        'w' => SegmentTileType.ArrowWaveTrigger,
                        't' => SegmentTileType.BladeTactTrigger,
                        'x' => SegmentTileType.BombTrigger,
                        'm' => SegmentTileType.ManaSource,
                        _ => SegmentTileType.Open,
                    };
            return new SegmentTemplate(name, 1, SegmentRewardTag.Mana, tiles);
        }

        private static readonly string[] SnakeRows = { ".....", "####.", ".....", ".####", "....." };

        // ---- Метрики маршрута ----

        [Test]
        public void StraightColumn_DetectedOnlyWhenWholeColumnIsOpenAndTrapFree()
        {
            Assert.IsTrue(TemplateRouteMetrics.HasStraightColumn(Make("open", ".....", ".....", ".....", ".....", ".....")));
            Assert.IsFalse(TemplateRouteMetrics.HasStraightColumn(Make("snake", SnakeRows)));
            Assert.IsFalse(TemplateRouteMetrics.HasStraightColumn(Make("trap-row", ".....", ".....", "rrrrr", ".....", ".....")));
        }

        [Test]
        public void SafePath_AvoidsTriggers_FastPathMayUseThem()
        {
            var sealedByTriggers = Make("sealed", ".....", ".....", "wwwww", ".....", ".....");
            Assert.AreEqual(-1, TemplateRouteMetrics.SafePathSteps(sealedByTriggers));
            Assert.AreEqual(4, TemplateRouteMetrics.FastPathSteps(sealedByTriggers));
        }

        [Test]
        public void Temptation_TrapShortcutShorterByTwoOrMore()
        {
            // Ряд 2 — стена с единственным триггером слева и безопасным проходом справа: со входа слева
            // через триггер 4 шага, в обход — на 4 шага больше (идти вбок до прохода).
            var tempting = Make("tempting", ".....", ".....", "w###.", ".....", ".....");
            Assert.IsTrue(TemplateRouteMetrics.HasTemptation(tempting));

            // На ровном поле ловушка путь не сокращает.
            Assert.IsFalse(TemplateRouteMetrics.HasTemptation(Make("flat", ".....", "..w..", ".....", ".....", ".....")));
        }

        [Test]
        public void DetourRatio_SnakeIsLongerThanOpenField()
        {
            var open = TemplateRouteMetrics.DetourRatio(Make("open", ".....", ".....", ".....", ".....", "....."));
            var snake = TemplateRouteMetrics.DetourRatio(Make("snake", SnakeRows));
            Assert.AreEqual(4.0 / 5, open, 1e-9);
            Assert.Greater(snake, open);
        }

        // ---- Допуск ловушек по Ярусам ----

        [Test]
        public void Eligible_Yaruses1To3_NeverContainBladesBombsOrLava()
        {
            foreach (var yarus in new[] { 1, 2, 3 })
                foreach (var template in SegmentTemplateCatalog.All.Where(t => YarusTemplateRules.IsEligible(t, yarus, true)))
                {
                    var traps = TemplateRouteMetrics.TrapTypes(template);
                    CollectionAssert.DoesNotContain(traps, SegmentTileType.BladeTactTrigger, template.Name);
                    CollectionAssert.DoesNotContain(traps, SegmentTileType.BombTrigger, template.Name);
                    CollectionAssert.DoesNotContain(traps, SegmentTileType.Lava, template.Name);
                }
        }

        [Test]
        public void IsTrapOpen_BladesAndBombsFromYarus4_LavaFromYarus4()
        {
            foreach (var type in new[] { SegmentTileType.BladeTactTrigger, SegmentTileType.BombTrigger, SegmentTileType.Lava })
            {
                Assert.IsFalse(YarusTemplateRules.IsTrapOpen(type, 3));
                Assert.IsTrue(YarusTemplateRules.IsTrapOpen(type, 4));
            }

            Assert.IsTrue(YarusTemplateRules.IsTrapOpen(SegmentTileType.FallingRockTrigger, 1));
            Assert.IsTrue(YarusTemplateRules.IsTrapOpen(SegmentTileType.MovingWallBothTrigger, 1));
        }

        [Test]
        public void Eligible_NonTrainingTemplates_HaveSafePathAndNoStraightColumn()
        {
            foreach (var yarus in Enumerable.Range(1, YarusProgression.MaxYarus))
                foreach (var template in SegmentTemplateCatalog.All.Where(t => YarusTemplateRules.IsEligible(t, yarus, false)))
                {
                    Assert.GreaterOrEqual(TemplateRouteMetrics.SafePathSteps(template), 0, template.Name);
                    Assert.IsFalse(TemplateRouteMetrics.HasStraightColumn(template), template.Name);
                }
        }

        [Test]
        public void TrainingTemplates_AreEligibleOnlyWhenTrainingAllowed()
        {
            foreach (var template in SegmentTemplateCatalog.All.Where(YarusTemplateRules.IsTraining))
            {
                Assert.IsFalse(YarusTemplateRules.IsEligible(template, 1, false), template.Name);
                Assert.IsTrue(YarusTemplateRules.IsEligible(template, 1, true), template.Name);
            }
        }

        [Test]
        public void EveryYarus_HasEnoughEligibleTemplatesAndTable()
        {
            var table = new StringBuilder("шаблон | тир | рядов | безопасный | быстрый | коэффициент | прямая | соблазн\n");
            foreach (var t in SegmentTemplateCatalog.All)
                table.AppendLine($"{t.Name} | {t.DifficultyTier} | {t.RowCount} | {TemplateRouteMetrics.SafePathSteps(t)} | {TemplateRouteMetrics.FastPathSteps(t)} | {TemplateRouteMetrics.DetourRatio(t):F2} | {TemplateRouteMetrics.HasStraightColumn(t)} | {TemplateRouteMetrics.HasTemptation(t)}");
            for (var band = 1; band <= 4; band++)
            {
                var yarus = new[] { 1, 4, 7, 10 }[band - 1];
                var pool = SegmentTemplateCatalog.All.Where(t => YarusTemplateRules.IsEligible(t, yarus, false)).ToList();
                var average = pool.Count == 0 ? 0 : pool.Average(TemplateRouteMetrics.DetourRatio);
                table.AppendLine($"полоса {band}: допустимых {pool.Count}, средний коэффициент крюка {average:F2} (цель {YarusTemplateRules.DetourTarget(band):F1})");
                Assert.GreaterOrEqual(pool.Count, 5, $"В полосе {band} слишком мало допустимых шаблонов.");
            }

            Debug.Log(table.ToString());
        }

        // ---- Селектор ----

        [Test]
        public void SelectNext_Context_NeverReturnsClosedTrapOrRepeatsInARow()
        {
            var selector = new SegmentSelector(SegmentTemplateCatalog.All, new RunSeed(7));
            SegmentTemplate previous = null;
            for (var i = 0; i < 1500; i++)
            {
                var yarus = 1 + i % 3;
                var template = selector.SelectNext(new SegmentSelectionContext(yarus, i % 5 == 0, (i % 10) / 10.0, false));
                Assert.IsTrue(YarusTemplateRules.IsEligible(template, yarus, false), template.Name);
                Assert.AreNotSame(previous, template);
                previous = template;
            }
        }

        // ---- Провайдер: длины Ярусов, Алтари на 1/3 и 2/3, Святилище в конце ----

        private static (TunnelGrid grid, SegmentRowProvider provider) BuildProvider(int seed = 3)
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var selector = new SegmentSelector(SegmentTemplateCatalog.All, new RunSeed(seed));
            var provider = new SegmentRowProvider(grid, trail, selector, _ => 1, YarusProgression.TunnelRows(1), YarusProgression.TunnelRows);
            return (grid, provider);
        }

        private static List<int> RowsWhere(TunnelGrid grid, int maxRow, System.Func<Tile, bool> predicate)
        {
            var rows = new List<int>();
            for (var row = 0; row <= maxRow; row++)
                if (Enumerable.Range(0, Width).Any(c => grid.TryGetTile(new GridCoordinate(row, c), out var tile) && predicate(tile)))
                    rows.Add(row);
            return rows;
        }

        [Test]
        public void Yarus1_AltarsAtOneAndTwoThirdsOfTunnel_SanctuaryAtEnd()
        {
            var (grid, provider) = BuildProvider();
            using (provider)
            {
                provider.EnsureCoveredThrough(200);
                var altars = RowsWhere(grid, 200, t => t.IsAltar);
                var entries = RowsWhere(grid, 200, t => t.IsSanctuaryEntry);

                // Тоннель 90 рядов: Алтарь #1 после ~30 рядов (+ квантование сегментов до 7), маркер Алтаря — ряд 1 шаблона.
                Assert.That(altars[0], Is.InRange(31, 38), "Алтарь #1 на 1/3");
                // Алтарь #2 — ещё 30 рядов наполнения после первого (шаблон Алтаря 6 рядов).
                Assert.That(altars[1] - altars[0], Is.InRange(36, 43), "Алтарь #2 на 2/3");
                // Святилище — после оставшихся 30 рядов.
                Assert.That(entries[0] - altars[1], Is.InRange(35, 42), "Святилище в конце");
                Assert.That(provider.YarusAtRow(entries[0]), Is.EqualTo(1));
                Assert.That(provider.YarusAtRow(entries[0] + SegmentTemplateCatalog.SanctuaryLeshyTemplate.RowCount), Is.EqualTo(2));
            }
        }

        [Test]
        public void Yaruses_FollowBandLengths_AndBandChangesOn4_7_10()
        {
            var (grid, provider) = BuildProvider();
            using (provider)
            {
                provider.EnsureCoveredThrough(1700);
                var entries = RowsWhere(grid, 1700, t => t.IsSanctuaryEntry && grid != null).Distinct().ToList();
                // Каждый вход в Святилище — это 5 плит ряда 0 Святилища; берём по одному ряду на Святилище.
                Assert.GreaterOrEqual(entries.Count, 10, "Должно быть покрыто минимум 10 Ярусов.");

                var previousEnd = 0;
                for (var yarus = 1; yarus <= 10; yarus++)
                {
                    var entry = entries[yarus - 1];
                    var expected = YarusProgression.TunnelRows(yarus) + 2 * SegmentTemplateCatalog.AltarTemplate.RowCount;
                    var actual = entry - previousEnd;
                    Assert.That(actual, Is.InRange(expected - 4, expected + 22), $"Ярус {yarus}: длина до Святилища.");
                    previousEnd = entry + SegmentTemplateCatalog.SanctuaryLeshyTemplate.RowCount;
                    Assert.AreEqual(YarusProgression.DifficultyBand(yarus), YarusProgression.DifficultyBand(provider.YarusAtRow(entry)));
                    Assert.AreEqual(yarus, provider.YarusAtRow(entry), $"Ярус {yarus}: номер — явное значение.");
                }

                Assert.AreEqual(1, YarusProgression.DifficultyBand(provider.YarusAtRow(entries[2])));
                Assert.AreEqual(2, YarusProgression.DifficultyBand(provider.YarusAtRow(entries[2] + 20)));
                Assert.AreEqual(3, YarusProgression.DifficultyBand(provider.YarusAtRow(entries[5] + 20)));
                Assert.AreEqual(4, YarusProgression.DifficultyBand(provider.YarusAtRow(entries[8] + 20)));
            }
        }

        [Test]
        public void TrainingTemplate_OnlyInFirstThirdOfYarus1_AtMostOnce()
        {
            var marker = Make("пустой-перегон", ".....", ".....", "m....", ".....", ".....");
            var plain = Make("змея", SnakeRows);
            foreach (var seed in Enumerable.Range(1, 15))
            {
                var grid = new TunnelGrid(Width);
                var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
                var selector = new SegmentSelector(new List<SegmentTemplate> { marker, plain, Make("змея-б", SnakeRows) }, new RunSeed(seed));
                using var provider = new SegmentRowProvider(grid, trail, selector, _ => 1, YarusProgression.TunnelRows(1), YarusProgression.TunnelRows);
                provider.EnsureCoveredThrough(700);

                var markerRows = RowsWhere(grid, 700, t => t.IsManaSource);
                Assert.LessOrEqual(markerRows.Count, 1, $"seed {seed}: обучающий шаблон больше одного раза.");
                Assert.IsTrue(markerRows.All(r => r < YarusProgression.TunnelRows(1) / 3 + SegmentTemplate.MaxRowCount), $"seed {seed}: обучающий шаблон вне первой трети Яруса 1.");
            }
        }
    }
}
