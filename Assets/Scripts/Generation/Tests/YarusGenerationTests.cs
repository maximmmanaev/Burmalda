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
        public void Temptation_IsSafePathMinusTrapPath()
        {
            // Дверь-Стрела в ряду-стене с проходом у дальнего края: через дверь короче, чем змейкой.
            var tempting = Make("tempting", ".....", "###w.", ".....", ".w###", ".....");
            Assert.AreEqual(TemplateRouteMetrics.SafePathSteps(tempting) - TemplateRouteMetrics.FastPathSteps(tempting), TemplateRouteMetrics.Temptation(tempting));
            Assert.GreaterOrEqual(TemplateRouteMetrics.Temptation(tempting), 2);

            // На ровном поле ловушка путь не сокращает.
            Assert.AreEqual(0, TemplateRouteMetrics.Temptation(Make("flat", ".....", "..w..", ".....", ".....", ".....")));
        }

        [Test]
        public void Detour_IsSafePathMinusRowsMinusOne()
        {
            Assert.AreEqual(0, TemplateRouteMetrics.Detour(Make("open", ".....", ".....", ".....", ".....", ".....")));
            Assert.AreEqual(TemplateRouteMetrics.SafePathSteps(Make("snake", SnakeRows)) - 4, TemplateRouteMetrics.Detour(Make("snake", SnakeRows)));
            Assert.Greater(TemplateRouteMetrics.Detour(Make("snake", SnakeRows)), 0);
        }

        [Test]
        public void IsTension_NeedsDetourAtLeast3_AndTemptationAtLeast2()
        {
            var door = Make("door", ".....", "###w.", ".....", ".w###", ".....", "###w.", "..m..", ".....");
            Assert.IsTrue(TemplateRouteMetrics.IsTension(door));
            Assert.IsFalse(TemplateRouteMetrics.IsTension(Make("snake-no-door", ".....", "####.", ".....", ".####", ".....", "####.", "..m..", ".....")));
            Assert.IsFalse(TemplateRouteMetrics.IsTension(Make("flat", ".....", "..w..", ".....", ".....", ".....")));
        }

        private static readonly string[] DesignerTension =
        {
            "змеиный-ход", "ступени", "змейка-с-дверью", "стрельбище", "давильня", "правый-пресс", "левый-пресс",
        };

        private static SegmentTemplate ByName(string name) => SegmentTemplateCatalog.All.Single(t => t.Name == name);

        [Test]
        public void DesignerTemplates_AreTension_WithDetour4AndTemptation4()
        {
            foreach (var name in DesignerTension.Skip(1))
            {
                var t = ByName(name);
                Assert.AreEqual(4, TemplateRouteMetrics.Detour(t), name);
                Assert.AreEqual(4, TemplateRouteMetrics.Temptation(t), name);
                Assert.IsTrue(TemplateRouteMetrics.IsTension(t), name);
            }

            // «змеиный-ход» оставлен по решению геймдизайнера, но дверей-ловушек в нём нет (Камень в конце
            // не сокращает путь): крюк 4, соблазн 0 — «передышка» с длинным маршрутом.
            Assert.AreEqual(4, TemplateRouteMetrics.Detour(ByName("змеиный-ход")));
            Assert.IsFalse(TemplateRouteMetrics.IsTension(ByName("змеиный-ход")));
        }

        [Test]
        public void OldDesignerTemplates_AreRemoved()
        {
            foreach (var name in new[] { "горлышко-со-стрелой", "перекрёсток-стрел", "петля-жадности" })
                Assert.IsFalse(SegmentTemplateCatalog.All.Any(t => t.Name == name), name);
        }

        // ---- Зеркала ----

        [Test]
        public void Mirror_FlipsColumns_AndSwapsLeftRightWalls()
        {
            var mirror = ByName("левый-пресс-зеркало");
            var source = ByName("левый-пресс");
            Assert.AreEqual(source.RowCount, mirror.RowCount);
            for (var r = 0; r < source.RowCount; r++)
                for (var c = 0; c < source.Width; c++)
                {
                    var expected = source.TileAt(r, source.Width - 1 - c) switch
                    {
                        SegmentTileType.MovingWallLeftTrigger => SegmentTileType.MovingWallRightTrigger,
                        SegmentTileType.MovingWallRightTrigger => SegmentTileType.MovingWallLeftTrigger,
                        var other => other,
                    };
                    Assert.AreEqual(expected, mirror.TileAt(r, c), $"{r},{c}");
                }
        }

        [Test]
        public void Mirrors_ExistForBand1TensionTemplates_WithSameDetourAndTemptation()
        {
            foreach (var name in DesignerTension.Skip(1))
            {
                var source = ByName(name);
                var mirror = ByName(name + "-зеркало");
                Assert.AreEqual(TemplateRouteMetrics.Detour(source), TemplateRouteMetrics.Detour(mirror), name);
                Assert.AreEqual(TemplateRouteMetrics.Temptation(source), TemplateRouteMetrics.Temptation(mirror), name);
                Assert.IsTrue(YarusTemplateRules.IsEligible(mirror, 1, false), name);
            }

            Assert.AreEqual(SegmentTemplateCatalog.All.Count, SegmentTemplateCatalog.All.Select(t => t.Name).Distinct().Count(), "имена уникальны");
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
        public void Eligible_NonTrainingTemplates_HaveSafePath_AndTensionIsNeverStraight()
        {
            foreach (var yarus in Enumerable.Range(1, YarusProgression.MaxYarus))
                foreach (var template in SegmentTemplateCatalog.All.Where(t => YarusTemplateRules.IsEligible(t, yarus, false)))
                {
                    Assert.GreaterOrEqual(TemplateRouteMetrics.SafePathSteps(template), 0, template.Name);
                    if (TemplateRouteMetrics.IsTension(template)) Assert.IsFalse(TemplateRouteMetrics.HasStraightColumn(template), template.Name);
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
        public void EveryBand_HasEligibleTemplates_AndPrintsTable()
        {
            var table = new StringBuilder("шаблон | тир | рядов | безопасный | быстрый | крюк | соблазн | напряжение\n");
            foreach (var t in SegmentTemplateCatalog.All)
                table.AppendLine($"{t.Name} | {t.DifficultyTier} | {t.RowCount} | {TemplateRouteMetrics.SafePathSteps(t)} | {TemplateRouteMetrics.FastPathSteps(t)} | {TemplateRouteMetrics.Detour(t)} | {TemplateRouteMetrics.Temptation(t)} | {TemplateRouteMetrics.IsTension(t)}");
            for (var band = 1; band <= 4; band++)
            {
                var yarus = new[] { 1, 4, 7, 10 }[band - 1];
                var pool = SegmentTemplateCatalog.All.Where(t => YarusTemplateRules.IsEligible(t, yarus, false)).ToList();
                var tension = pool.Count(TemplateRouteMetrics.IsTension);
                table.AppendLine($"полоса {band}: допустимых {pool.Count}, напряжения {tension}, средний крюк {pool.Average(TemplateRouteMetrics.Detour):F2}");
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

        private static string BaseName(SegmentTemplate t) => t.Name.EndsWith("-зеркало") ? t.Name.Substring(0, t.Name.Length - "-зеркало".Length) : t.Name;

        [Test]
        public void Rhythm_NoRepeatInclMirror_AtMostOneBreatherPerThree_TensionOnlyBeforeLandmark()
        {
            foreach (var seed in Enumerable.Range(1, 20))
            {
                var selector = new SegmentSelector(SegmentTemplateCatalog.All, new RunSeed(seed));
                var history = new List<(SegmentTemplate t, bool easy, bool tensionOnly)>();
                for (var i = 0; i < 400; i++)
                {
                    var position = i % 4; // секция из 4 сегментов: лёгкий, свободный, свободный, напряжение
                    var easy = position == 0;
                    var tensionOnly = position >= 2;
                    var t = selector.SelectNext(new SegmentSelectionContext(1, easy, position / 4.0, false, tensionOnly));
                    history.Add((t, easy, tensionOnly));
                }

                for (var i = 0; i < history.Count; i++)
                {
                    if (i > 0) Assert.AreNotEqual(BaseName(history[i - 1].t), BaseName(history[i].t), $"seed {seed}: повтор подряд");
                    if (history[i].tensionOnly) Assert.IsTrue(TemplateRouteMetrics.IsTension(history[i].t), $"seed {seed}: не напряжение перед ориентиром");
                    if (history[i].easy) Assert.IsFalse(TemplateRouteMetrics.IsTension(history[i].t), $"seed {seed}: после Алтаря должна быть передышка");
                    if (i >= 2 && !history[i].easy)
                    {
                        var breathers = Enumerable.Range(i - 2, 3).Count(k => !TemplateRouteMetrics.IsTension(history[k].t) && !history[k].easy);
                        Assert.LessOrEqual(breathers, 1, $"seed {seed}: больше 1 передышки на 3 сегмента (индекс {i})");
                    }
                }
            }
        }

        [Test]
        public void Rhythm_BandWithoutTension_FallsBackToBestDetour_WithoutThrowing()
        {
            var low = Make("низкий", ".....", ".....", ".....", ".....", ".....");
            var high = Make("высокий", ".....", "####.", ".....", ".####", ".....");
            var selector = new SegmentSelector(new List<SegmentTemplate> { low, high, Make("средний", ".....", "###..", ".....", ".....", ".....") }, new RunSeed(1));
            for (var i = 0; i < 30; i++)
            {
                var t = selector.SelectNext(new SegmentSelectionContext(1, false, 0.5, false, true));
                Assert.AreEqual("высокий", t.Name); // напряжения нет — лучший по крюку; повтор единственного лучшего допустим
            }
        }

        [Test]
        public void Simulation_Yaruses1To3_MeanDetourAtLeast2_5_AndTensionShareAtLeast60Percent()
        {
            var detours = new List<int>();
            var tension = 0;
            foreach (var seed in Enumerable.Range(1, 12))
            {
                var grid = new TunnelGrid(Width);
                var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
                var selector = new SegmentSelector(SegmentTemplateCatalog.All, new RunSeed(seed));
                selector.Selected += (context, template) =>
                {
                    if (context.Yarus > 3) return;
                    detours.Add(TemplateRouteMetrics.Detour(template));
                    if (TemplateRouteMetrics.IsTension(template)) tension++;
                };
                using var provider = new SegmentRowProvider(grid, trail, selector, _ => 1, YarusProgression.TunnelRows(1), YarusProgression.TunnelRows);
                // Ярусы 1–3: 3 × (90 + 2 Алтаря + Святилище) рядов.
                provider.EnsureCoveredThrough(3 * (90 + 2 * SegmentTemplateCatalog.AltarTemplate.RowCount + SegmentTemplateCatalog.SanctuaryLeshyTemplate.RowCount));
            }

            var mean = detours.Average();
            var share = (double)tension / detours.Count;
            Debug.Log($"симуляция Ярусов 1–3: сегментов {detours.Count}, средний крюк {mean:F2}, доля напряжения {share:P0}");
            Assert.GreaterOrEqual(mean, 2.5);
            Assert.GreaterOrEqual(share, 0.6);
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
