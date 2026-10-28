using System.Collections.Generic;
using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Generation.Tests
{
    /// <summary>Авторские инварианты Святилища (PRD v10 §14.2, §14.3) на раскладке Святилища Лешего.</summary>
    public class SanctuaryLayoutTests
    {
        private static SegmentTemplate Layout => SegmentTemplateCatalog.SanctuaryLeshyTemplate;

        private static int Count(SegmentTileType type)
        {
            var n = 0;
            for (var r = 0; r < Layout.RowCount; r++)
            for (var c = 0; c < Layout.Width; c++)
                if (Layout.TileAt(r, c) == type) n++;
            return n;
        }

        [Test]
        public void EntryRow_CoversWholeWidth_SoSanctuaryCannotBeBypassed()
        {
            for (var c = 0; c < Layout.Width; c++)
                Assert.AreEqual(SegmentTileType.SanctuaryEntry, Layout.TileAt(0, c), $"колонка {c}");
        }

        [Test]
        public void LastRow_HasAltarForTheExit()
        {
            Assert.AreEqual(1, Count(SegmentTileType.Altar));
            var last = Layout.RowCount - 1;
            var found = false;
            for (var c = 0; c < Layout.Width; c++) found |= Layout.TileAt(last, c) == SegmentTileType.Altar;
            Assert.IsTrue(found, "Алтарь в конце Святилища — в последнем ряду");
        }

        [Test]
        public void Contains_OneSarcophagus_AndTheMultiplierTiles()
        {
            Assert.AreEqual(1, Count(SegmentTileType.Sarcophagus));
            Assert.Greater(Count(SegmentTileType.SanctuaryVein), 0);
            Assert.Greater(Count(SegmentTileType.SanctuaryResonance), 0);
            Assert.AreEqual(1, Count(SegmentTileType.SanctuaryEcho));
            Assert.AreEqual(1, Count(SegmentTileType.SanctuaryRift));
        }

        [Test]
        public void Rewards_AreNeverInStrikeZonesOfTraps()
        {
            var conflicts = RewardTrapConflictValidator.FindConflicts(Layout);

            Assert.IsEmpty(conflicts, string.Join("; ", conflicts));
        }

        [Test]
        public void MovingWallRows_HaveNoOtherTriggers_AndOnlyFirstGroupTrapsAreUsed()
        {
            // Эксклюзивный ряд стен проверяет конструктор шаблона (ValidateMovingWallExclusiveRow);
            // здесь — что Святилище Лешего (Ярус 1) использует только ловушки 1-й группы (§8.2).
            for (var r = 0; r < Layout.RowCount; r++)
            for (var c = 0; c < Layout.Width; c++)
            {
                var t = Layout.TileAt(r, c);
                Assert.AreNotEqual(SegmentTileType.BombTrigger, t);
                Assert.AreNotEqual(SegmentTileType.BladeTactTrigger, t);
            }
        }

        [Test]
        public void Layout_IsTraversableEntryToExit_IgnoringTraps()
        {
            Assert.IsTrue(SegmentReachabilityValidator.IsTraversable(Layout));
        }

        [Test]
        public void SarcophagusAndExit_AreReachableOverNonWallTiles()
        {
            var sarcophagus = Find(SegmentTileType.Sarcophagus);
            var exit = Find(SegmentTileType.Altar);

            Assert.IsTrue(CanReach(FirstEntryCell(), sarcophagus), "путь по клеткам без стен от входа до Саркофага");
            Assert.IsTrue(CanReach(sarcophagus, exit), "путь по клеткам без стен от Саркофага до выхода");
        }

        [Test]
        public void SanctuaryTiles_AreAppliedToGridWithRolesIntact()
        {
            var grid = new TunnelGrid(Layout.Width);
            using var provider = new SegmentRowProvider(grid, new Burmalda.Movement.GridTraceTrail(grid, new GridCoordinate(0, 2)),
                SingleFiller(), _ => 1, rowsPerTier: 40);

            provider.EnsureCoveredThrough(50);

            var sarcophagusTiles = 0;
            var echoTiles = 0;
            for (var r = 1; r <= 60; r++)
            for (var c = 0; c < Layout.Width; c++)
            {
                var tile = grid.GetOrCreateTile(new GridCoordinate(r, c));
                if (tile.HasSarcophagus) sarcophagusTiles++;
                if (tile.SanctuaryTile == SanctuaryTileKind.Echo) echoTiles++;
            }

            Assert.AreEqual(1, sarcophagusTiles);
            Assert.AreEqual(1, echoTiles);
        }

        private static SegmentSelector SingleFiller() =>
            new SegmentSelector(new List<SegmentTemplate> { SegmentTemplateCatalog.All[0] }, new RunSeed(1));

        private static (int Row, int Column) Find(SegmentTileType type)
        {
            for (var r = 0; r < Layout.RowCount; r++)
            for (var c = 0; c < Layout.Width; c++)
                if (Layout.TileAt(r, c) == type) return (r, c);
            Assert.Fail($"{type} не найден в раскладке");
            return (0, 0);
        }

        private static (int Row, int Column) FirstEntryCell() => (0, 2);

        private static bool CanReach((int Row, int Column) from, (int Row, int Column) to)
        {
            var seen = new HashSet<(int, int)> { from };
            var queue = new Queue<(int Row, int Column)>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var (row, column) = queue.Dequeue();
                if ((row, column) == to) return true;
                for (var dr = -1; dr <= 1; dr++)
                for (var dc = -1; dc <= 1; dc++)
                {
                    var nr = row + dr;
                    var nc = column + dc;
                    if ((dr == 0 && dc == 0) || nr < 0 || nr >= Layout.RowCount || nc < 0 || nc >= Layout.Width) continue;
                    if (Layout.TileAt(nr, nc) == SegmentTileType.Blocked || !seen.Add((nr, nc))) continue;
                    queue.Enqueue((nr, nc));
                }
            }

            return false;
        }
    }
}
