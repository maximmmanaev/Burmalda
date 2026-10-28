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

        private static readonly string[] SnakeDoorNames =
        {
            "ступени", "змейка-с-дверью", "стрельбище", "давильня", "правый-пресс", "левый-пресс",
        };

        private static SegmentTemplate ByName(string name) => SegmentTemplateCatalog.All.Single(t => t.Name == name);

        private static int TriggerCount(SegmentTemplate t) =>
            Enumerable.Range(0, t.RowCount).Sum(r => Enumerable.Range(0, t.Width).Count(c => TemplateRouteMetrics.IsTrap(t.TileAt(r, c))));

        [Test]
        public void SnakeDoorTemplates_HaveDetour4AndTemptation4_AndFamily()
        {
            foreach (var name in SnakeDoorNames)
            {
                var t = ByName(name);
                Assert.AreEqual(4, TemplateRouteMetrics.Detour(t), name);
                Assert.AreEqual(4, TemplateRouteMetrics.Temptation(t), name);
                Assert.AreEqual(SegmentFamily.SnakeDoor, t.Family, name);
            }
        }

        [Test]
        public void TraplessSnake_IsRemoved_AndOldDesignerTemplatesStayRemoved()
        {
            foreach (var name in new[] { "змеиный-ход", "змеиный-ход-зеркало", "горлышко-со-стрелой", "перекрёсток-стрел", "петля-жадности" })
                Assert.IsFalse(SegmentTemplateCatalog.All.Any(t => t.Name == name), name);
        }

        [Test]
        public void NewFamilies_MinedSnake_Minefield_PillarHall_HaveDesignedGeometry()
        {
            var mined = ByName("змея-с-минами");
            Assert.AreEqual(SegmentFamily.MinedSnake, mined.Family);
            Assert.AreEqual(9, TemplateRouteMetrics.SafePathSteps(mined));
            Assert.AreEqual(2, TemplateRouteMetrics.Detour(mined));
            Assert.AreEqual(8, TriggerCount(mined));

            foreach (var name in new[] { "минное-поле", "минное-поле-2" })
            {
                var t = ByName(name);
                Assert.AreEqual(SegmentFamily.Minefield, t.Family, name);
                Assert.AreEqual(11, TriggerCount(t), name);
                Assert.GreaterOrEqual(TemplateRouteMetrics.SafePathSteps(t), 0, name);
            }

            Assert.AreEqual(SegmentFamily.PillarHall, ByName("колонный-зал").Family);
            Assert.AreEqual(SegmentRewardTag.Coins, ByName("минное-поле").RewardTag);
        }

        [Test]
        public void MinefieldAndPillarHall_HaveTriggerDensityAtLeast25Percent_AndTriggerInEveryLaneRow()
        {
            var checkedCount = 0;
            foreach (var t in SegmentTemplateCatalog.All.Where(t => t.Family == SegmentFamily.Minefield || t.Family == SegmentFamily.PillarHall))
            {
                Assert.GreaterOrEqual(TemplateRouteMetrics.LaneTriggerDensity(t), 0.25, t.Name);
                Assert.IsTrue(TemplateRouteMetrics.EveryLaneRowHasTrigger(t), t.Name);
                checkedCount++;
            }

            Assert.AreEqual(2 * 3, checkedCount, "3 шаблона и 3 зеркала");
        }

        [Test]
        public void EveryTemplate_HasFamily_OnlyReliefOrSnakeDoorAmongOldOnes()
        {
            var newOnes = new HashSet<string> { "змея-с-минами", "минное-поле", "минное-поле-2", "колонный-зал" };
            foreach (var t in SegmentTemplateCatalog.All)
            {
                var name = t.Name.Replace(SegmentTemplate.MirrorSuffix, "");
                if (newOnes.Contains(name) || SnakeDoorNames.Contains(name)) continue;
                Assert.AreEqual(SegmentFamily.Relief, t.Family, t.Name);
            }
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
        public void Mirrors_ExistForAllNonReliefBand1Templates_WithSameFamilyDetourAndTemptation()
        {
            foreach (var source in SegmentTemplateCatalog.All.Where(t => t.Family != SegmentFamily.Relief && !t.Name.EndsWith(SegmentTemplate.MirrorSuffix)))
            {
                var mirror = ByName(source.Name + SegmentTemplate.MirrorSuffix);
                Assert.AreEqual(source.Family, mirror.Family, source.Name);
                Assert.AreEqual(TemplateRouteMetrics.Detour(source), TemplateRouteMetrics.Detour(mirror), source.Name);
                Assert.AreEqual(TemplateRouteMetrics.Temptation(source), TemplateRouteMetrics.Temptation(mirror), source.Name);
                Assert.IsTrue(YarusTemplateRules.IsEligible(mirror, 1, false), source.Name);
            }

            Assert.AreEqual(SegmentTemplateCatalog.All.Count, SegmentTemplateCatalog.All.Select(t => t.Name).Distinct().Count(), "имена уникальны");
        }

        // ---- Правило: Падающий камень не запечатывает единственный проход ----

        [Test]
        public void FallingRockSealing_DetectsRockThatClosesOnlyPassage()
        {
            // Камень в (1,2) бьёт по (2,2); в ряду 2 проход один — центр.
            var sealing = Make("seal", ".....", "..r..", "##.##", ".....", ".....");
            Assert.IsTrue(TemplateRouteMetrics.FallingRockSealsPassage(sealing));

            var fine = Make("fine", ".....", "..r..", ".....", ".....", ".....");
            Assert.IsFalse(TemplateRouteMetrics.FallingRockSealsPassage(fine));
        }

        [Test]
        public void FallingRockSealing_NoCatalogTemplateSealsPassage()
        {
            foreach (var t in SegmentTemplateCatalog.All)
                Assert.IsFalse(TemplateRouteMetrics.FallingRockSealsPassage(t), t.Name);
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
        public void Eligible_NonTrainingTemplates_HaveSafePath_AndNonReliefIsNeverStraight()
        {
            foreach (var yarus in Enumerable.Range(1, YarusProgression.MaxYarus))
                foreach (var template in SegmentTemplateCatalog.All.Where(t => YarusTemplateRules.IsEligible(t, yarus, false)))
                {
                    Assert.GreaterOrEqual(TemplateRouteMetrics.SafePathSteps(template), 0, template.Name);
                    // Площадка (minefield) и зал колонн крюка не имеют по замыслу — прямое правило только для змеек.
                    if (template.Family == SegmentFamily.SnakeDoor || template.Family == SegmentFamily.MinedSnake) Assert.IsFalse(TemplateRouteMetrics.HasStraightColumn(template), template.Name);
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
        public void EveryBand_HasEligibleTemplates_AndPrintsFamilyTable()
        {
            var table = new StringBuilder("шаблон | семейство | тир | рядов | безопасный | быстрый | крюк | соблазн | ловушек | плотность\n");
            foreach (var t in SegmentTemplateCatalog.All)
                table.AppendLine($"{t.Name} | {t.Family} | {t.DifficultyTier} | {t.RowCount} | {TemplateRouteMetrics.SafePathSteps(t)} | {TemplateRouteMetrics.FastPathSteps(t)} | {TemplateRouteMetrics.Detour(t)} | {TemplateRouteMetrics.Temptation(t)} | {TriggerCount(t)} | {TemplateRouteMetrics.LaneTriggerDensity(t):F2}");
            foreach (var yarus in new[] { 1, 4, 7, 10 })
            {
                var pool = SegmentTemplateCatalog.All.Where(t => YarusTemplateRules.IsEligible(t, yarus, false)).ToList();
                var families = string.Join(", ", pool.GroupBy(t => t.Family).Select(g => $"{g.Key}={g.Count()}"));
                table.AppendLine($"Ярус {yarus}: допустимых {pool.Count} ({families})");
                Assert.GreaterOrEqual(pool.Select(t => t.Family).Distinct().Count(), 4, $"Ярус {yarus}: мало семейств в пуле.");
            }

            Debug.Log(table.ToString());
        }

        // ---- Селектор ----

        [Test]
        public void SelectNext_Context_NeverReturnsClosedTrapOrRepeatsFamilyInARow()
        {
            var selector = new SegmentSelector(SegmentTemplateCatalog.All, new RunSeed(7));
            SegmentTemplate previous = null;
            for (var i = 0; i < 1500; i++)
            {
                var yarus = 1 + (i / 12) % 3;
                var template = selector.SelectNext(new SegmentSelectionContext(yarus, i % 6 == 0, (i % 10) / 10.0, false, i % 6 >= 4));
                Assert.IsTrue(YarusTemplateRules.IsEligible(template, yarus, false), template.Name);
                if (previous != null) Assert.AreNotEqual(previous.Family, template.Family, $"шаг {i}: семейство дважды подряд");
                previous = template;
            }
        }

        [Test]
        public void SelectNext_Easy_IsRelief_AndNoReliefBeforeLandmark_IsNotRelief()
        {
            foreach (var seed in Enumerable.Range(1, 20))
            {
                var selector = new SegmentSelector(SegmentTemplateCatalog.All, new RunSeed(seed));
                for (var i = 0; i < 200; i++)
                {
                    var position = i % 5; // секция: лёгкий, свободные, два перед ориентиром
                    var easy = position == 0;
                    var noRelief = position >= 3;
                    var t = selector.SelectNext(new SegmentSelectionContext(1, easy, position / 5.0, false, noRelief));
                    if (easy) Assert.AreEqual(SegmentFamily.Relief, t.Family, $"seed {seed}");
                    if (noRelief) Assert.AreNotEqual(SegmentFamily.Relief, t.Family, $"seed {seed}");
                }
            }
        }

        [Test]
        public void SelectNext_PoolWithoutNonRelief_FallsBackWithoutThrowing()
        {
            var selector = new SegmentSelector(new List<SegmentTemplate> { Make("а", SnakeRows), Make("б", ".....", ".....", ".....", ".....", ".....") }, new RunSeed(1));
            for (var i = 0; i < 30; i++)
                Assert.DoesNotThrow(() => selector.SelectNext(new SegmentSelectionContext(1, false, 0.5, false, true)));
        }

        [Test]
        public void Simulation_Yaruses1To3_FamiliesVaryAndTargetsMet()
        {
            var perYarus = new Dictionary<(int seed, int yarus), List<SegmentTemplate>>();
            var all = new List<(SegmentSelectionContext ctx, SegmentTemplate t)>();
            foreach (var seed in Enumerable.Range(1, 12))
            {
                var grid = new TunnelGrid(Width);
                var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
                var selector = new SegmentSelector(SegmentTemplateCatalog.All, new RunSeed(seed));
                SegmentTemplate previous = null;
                selector.Selected += (context, template) =>
                {
                    if (context.Yarus > 3) return;
                    if (previous != null) Assert.AreNotEqual(previous.Family, template.Family, $"seed {seed}: семейство дважды подряд");
                    previous = template;
                    if (context.Easy) Assert.AreEqual(SegmentFamily.Relief, template.Family, $"seed {seed}: после Алтаря");
                    if (context.NoRelief) Assert.AreNotEqual(SegmentFamily.Relief, template.Family, $"seed {seed}: перед ориентиром");
                    if (!perYarus.TryGetValue((seed, context.Yarus), out var list)) perYarus[(seed, context.Yarus)] = list = new List<SegmentTemplate>();
                    list.Add(template);
                    all.Add((context, template));
                };
                using var provider = new SegmentRowProvider(grid, trail, selector, _ => 1, YarusProgression.TunnelRows(1), YarusProgression.TunnelRows);
                provider.EnsureCoveredThrough(3 * (90 + 2 * SegmentTemplateCatalog.AltarTemplate.RowCount + SegmentTemplateCatalog.SanctuaryLeshyTemplate.RowCount));
            }

            foreach (var kv in perYarus.Where(kv => kv.Key.yarus <= 3))
            {
                var list = kv.Value;
                var snakeShare = (double)list.Count(t => t.Family == SegmentFamily.SnakeDoor) / list.Count;
                Assert.LessOrEqual(snakeShare, 0.35, $"seed {kv.Key.seed} Ярус {kv.Key.yarus}: snake-door {snakeShare:P0}");
                Assert.GreaterOrEqual(list.Where(t => t.Family != SegmentFamily.Relief).Select(t => t.Family).Distinct().Count(), 3, $"seed {kv.Key.seed} Ярус {kv.Key.yarus}: мало семейств");
            }

            var snakes = all.Where(x => x.t.Family == SegmentFamily.SnakeDoor || x.t.Family == SegmentFamily.MinedSnake).Select(x => TemplateRouteMetrics.Detour(x.t)).ToList();
            Assert.GreaterOrEqual(snakes.Average(), 2.5, "средний крюк snake-door + mined-snake");
            foreach (var x in all.Where(x => x.t.Family == SegmentFamily.Minefield || x.t.Family == SegmentFamily.PillarHall))
                Assert.GreaterOrEqual(TemplateRouteMetrics.LaneTriggerDensity(x.t), 0.25, x.t.Name);

            var table = new StringBuilder($"симуляция Ярусов 1–3: {all.Count} сегментов, средний крюк snake-door+mined-snake {snakes.Average():F2}\n");
            foreach (var g in all.GroupBy(x => x.t.Family).OrderByDescending(g => g.Count()))
                table.AppendLine($"  {g.Key}: {g.Count()} ({(double)g.Count() / all.Count:P0})");
            Debug.Log(table.ToString());
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
        public void AltarSpots_AreRegistered_OneAndTwoBeforeSanctuary_ThreeAtSanctuaryEnd()
        {
            var (grid, provider) = BuildProvider();
            using (provider)
            {
                provider.EnsureCoveredThrough(400);
                var altarRows = RowsWhere(grid, 400, t => t.IsAltar);
                var spots = altarRows.Select(r => provider.TryGetAltarSpot(r, out var y, out var o) ? (y, o) : (0, 0)).ToList();

                Assert.IsTrue(spots.All(s => s.Item2 >= 1), "каждый Алтарь тоннеля известен провайдеру");
                CollectionAssert.AreEqual(new[] { (1, 1), (1, 2), (1, 3), (2, 1) }, spots.Take(4).ToList());
                Assert.IsFalse(provider.TryGetAltarSpot(1, out _, out _), "обычный ряд — не Алтарь");
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
