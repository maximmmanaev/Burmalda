using System;
using System.Collections.Generic;
using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Generation.Tests
{
    public class SegmentRowProviderTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail) CreateTrail()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            return (grid, trail);
        }

        private static SegmentTileType[,] OpenRows(int rowCount, int width = Width)
        {
            var tiles = new SegmentTileType[rowCount, width];
            for (var r = 0; r < rowCount; r++)
                for (var c = 0; c < width; c++)
                    tiles[r, c] = SegmentTileType.Open;
            return tiles;
        }

        private static SegmentSelector SingleTemplateSelector(SegmentTemplate template, int seed = 1) =>
            new SegmentSelector(new List<SegmentTemplate> { template }, new RunSeed(seed));

        // Все шаблоны в этом файле — tier 1 (единственный кандидат в
        // каталоге-заглушке SingleTemplateSelector). maxDifficultyForRow
        // передаётся сюда как "_ => 1" (совпадает с тиром шаблона) — с
        // задачи «партии 1 и 2 + правила отбора» SegmentSelector фильтрует
        // ОКНОМ вокруг цели (по умолчанию [цель, цель+1]), не "<= цель":
        // "_ => 5" (прежнее значение, тест сам по себе к числу 5 не
        // привязан) больше не включал бы tier-1 в окно [5,6].

        [Test]
        public void Constructor_CoversRowsAheadOfPlayerImmediately()
        {
            var (grid, trail) = CreateTrail();
            var template = new SegmentTemplate("open-5", 1, SegmentRewardTag.Coins, OpenRows(5));
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1);

            // 5-рядные шаблоны, старт row=0 => появляется минимум до row 8 (RowsAheadOfPlayer) включительно.
            Assert.IsTrue(grid.TryGetTile(new GridCoordinate(8, 0), out _));
        }

        [Test]
        public void ApplyTemplate_BlockedTile_MarksAbsoluteCoordinate()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[2, 1] = SegmentTileType.Blocked; // локально (2,1) -> глобально (baseRow=1) + 2 = row 3
            var template = new SegmentTemplate("with-block", 1, SegmentRewardTag.Coins, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1);

            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(3, 1)).IsBlocked);
            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(3, 0)).IsBlocked);
        }

        [Test]
        public void ApplyTemplate_ExplosiveTrigger_SetsTargetOneRowAheadSameColumn()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[1, 2] = SegmentTileType.ExplosiveTrigger; // baseRow=1 => глобально row 2

            var template = new SegmentTemplate("with-trigger", 1, SegmentRewardTag.Coins, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1);

            var triggerTile = grid.GetOrCreateTile(new GridCoordinate(2, 2));
            Assert.AreEqual(new GridCoordinate(3, 2), triggerTile.ExplosiveTrapTarget);
        }

        [Test]
        public void ApplyTemplate_TimedTrapTrigger_SetsTargetAndKind()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[1, 2] = SegmentTileType.TimedTrapBladeTrigger;

            var template = new SegmentTemplate("with-timed", 1, SegmentRewardTag.Coins, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1);

            var triggerTile = grid.GetOrCreateTile(new GridCoordinate(2, 2));
            Assert.AreEqual(new GridCoordinate(3, 2), triggerTile.TimedTrapTarget);
            Assert.AreEqual(TimedTrapType.Blade, triggerTile.TimedTrapKind);
        }

        [Test]
        public void ApplyTemplate_ManaSource_MarksTile()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[1, 2] = SegmentTileType.ManaSource;

            var template = new SegmentTemplate("with-mana", 1, SegmentRewardTag.Mana, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1);

            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(2, 2)).IsManaSource);
            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(2, 2)).IsKeySource);
        }

        [Test]
        public void ApplyTemplate_KeySource_MarksTile()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[1, 2] = SegmentTileType.KeySource;

            var template = new SegmentTemplate("with-key", 1, SegmentRewardTag.Keys, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1);

            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(2, 2)).IsKeySource);
            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(2, 2)).IsManaSource);
        }

        [Test]
        public void ApplyTemplate_Altar_MarksTile()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[1, 2] = SegmentTileType.Altar;

            var template = new SegmentTemplate("with-altar", 1, SegmentRewardTag.Coins, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1);

            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(2, 2)).IsAltar);
        }

        [Test]
        public void ApplyTemplate_Boss_MarksTile()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[1, 2] = SegmentTileType.Boss;

            var template = new SegmentTemplate("with-boss", 1, SegmentRewardTag.Coins, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1);

            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(2, 2)).IsBoss);
        }

        [Test]
        public void ApplyTemplate_LeverAndGate_WiresAbsoluteGateCoordinates()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[1, 0] = SegmentTileType.Lever;   // -> row 2, col 0
            tiles[1, 1] = SegmentTileType.LeverGate; // -> row 2, col 1
            tiles[2, 1] = SegmentTileType.LeverGate; // -> row 3, col 1

            var template = new SegmentTemplate("with-lever", 1, SegmentRewardTag.Artifact, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1);

            var lever = grid.GetOrCreateTile(new GridCoordinate(2, 0));
            Assert.IsTrue(lever.IsLever);
            CollectionAssert.AreEquivalent(
                new[] { new GridCoordinate(2, 1), new GridCoordinate(3, 1) },
                lever.LeverGateTargets);

            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(2, 1)).IsGated);
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(3, 1)).IsGated);

            // Issue #193: обе плиты-Ворота должны знать координату СВОЕГО
            // рычага — TunnelDebugVisual использует её для подсказки
            // направления на закрытых Воротах.
            var leverCoordinate = new GridCoordinate(2, 0);
            Assert.AreEqual(leverCoordinate, grid.GetOrCreateTile(new GridCoordinate(2, 1)).LeverCoordinate);
            Assert.AreEqual(leverCoordinate, grid.GetOrCreateTile(new GridCoordinate(3, 1)).LeverCoordinate);
        }

        [Test]
        public void EnsureCoveredThrough_AdvancesByWholeTemplatesNotPartialRows()
        {
            var (grid, trail) = CreateTrail();
            var template = new SegmentTemplate("open-8", 1, SegmentRewardTag.Coins, OpenRows(8));
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1);

            provider.EnsureCoveredThrough(9); // запрос на 9, но шаблон занимает по 8 рядов — покрытие прыгнет минимум до row 8

            Assert.IsTrue(grid.TryGetTile(new GridCoordinate(8, 0), out _));
        }

        [Test]
        public void Constructor_TemplateWidthMismatch_Throws()
        {
            var (grid, trail) = CreateTrail();
            var template = new SegmentTemplate("wrong-width", 1, SegmentRewardTag.Coins, OpenRows(5, width: 3));

            Assert.Throws<InvalidOperationException>(() =>
                new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1));
        }

        [Test]
        public void PositionChanged_PlayerAdvances_CoversFurtherRows()
        {
            var (grid, trail) = CreateTrail();
            var template = new SegmentTemplate("open-5", 1, SegmentRewardTag.Coins, OpenRows(5));
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1);

            for (var row = 1; row <= 6; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            // playerRow=6 + RowsAheadOfPlayer(8) = 14 должно быть покрыто.
            Assert.IsTrue(grid.TryGetTile(new GridCoordinate(14, 0), out _));
        }

        [Test]
        public void Dispose_StopsReactingToFurtherPositionChanges()
        {
            var (grid, trail) = CreateTrail();
            var template = new SegmentTemplate("open-5", 1, SegmentRewardTag.Coins, OpenRows(5));
            var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1);
            var coveredBefore = grid.TryGetTile(new GridCoordinate(8, 0), out _); // покрыто ещё в конструкторе (rows 1..10)
            provider.Dispose();

            // Трейл идёт только по столбцу 2 — если бы provider всё ещё
            // реагировал, применение сегментов материализовало бы ВСЮ
            // ширину (см. ApplyTemplate), включая столбец 0 на дальних рядах.
            for (var row = 1; row <= 20; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            Assert.IsTrue(coveredBefore);
            Assert.IsFalse(grid.TryGetTile(new GridCoordinate(15, 0), out _), "после Dispose сегменты по всей ширине больше не применяются");
        }

        [Test]
        public void RowZero_NeverTouchedByTemplateApplication()
        {
            // Первый применённый шаблон всегда начинается с baseRow=1 (row 0
            // уже безопасен по построению трейла, см. EnsureCoveredThrough) —
            // даже шаблон, чей ЛОКАЛЬНЫЙ ряд 0 заблокирован, не может задеть
            // глобальный ряд 0.
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++) tiles[0, c] = SegmentTileType.Blocked;
            var template = new SegmentTemplate("blocked-local-row0", 1, SegmentRewardTag.Coins, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1);

            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(0, 2)).IsBlocked, "стартовый ряд должен оставаться безопасным (#9)");
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(1, 2)).IsBlocked, "локальный ряд 0 шаблона применяется к глобальному ряду 1, не 0");
        }
    }
}
