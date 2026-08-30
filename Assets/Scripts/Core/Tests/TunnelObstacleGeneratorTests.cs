using System.Collections.Generic;
using NUnit.Framework;

namespace Burmalda.Core.Tests
{
    public class TunnelObstacleGeneratorTests
    {
        private static System.Func<float> Sequence(params float[] rolls)
        {
            var queue = new Queue<float>(rolls);
            return () => queue.Dequeue();
        }

        [Test]
        public void TileMaterialized_RollBelowBlockedThreshold_MarksTileBlocked()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(0f));

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsTrue(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
        }

        [Test]
        public void TileMaterialized_RollAtBlockedThreshold_MarksLethalTrapPit()
        {
            // Верхняя граница диапазона block — начало диапазона pit (полуинтервалы: [0, Blocked) block, [Blocked, Pit) pit).
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.BlockedThreshold));

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsFalse(tile.IsBlocked);
            Assert.AreEqual(LethalTrapType.Pit, tile.LethalTrap);
        }

        [Test]
        public void TileMaterialized_RollAtPitThreshold_MarksLethalTrapLava()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.PitThreshold));

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsFalse(tile.IsBlocked);
            Assert.AreEqual(LethalTrapType.Lava, tile.LethalTrap);
        }

        [Test]
        public void TileMaterialized_RollAtLavaThreshold_MarksExplosiveTrapTrigger()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.LavaThreshold));

            var trigger = new GridCoordinate(1, 0);
            var tile = grid.GetOrCreateTile(trigger);

            Assert.IsFalse(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
            Assert.AreEqual(new GridCoordinate(2, 0), tile.ExplosiveTrapTarget);
        }

        [Test]
        public void TileMaterialized_RollAtExplosiveTriggerThreshold_MarksTimedTrapTriggerArrow()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.ExplosiveTriggerThreshold));

            var trigger = new GridCoordinate(1, 0);
            var tile = grid.GetOrCreateTile(trigger);

            Assert.IsFalse(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
            Assert.AreEqual(new GridCoordinate(2, 0), tile.TimedTrapTarget);
            Assert.AreEqual(TimedTrapType.Arrow, tile.TimedTrapKind);
        }

        [Test]
        public void TileMaterialized_RollAtTimedTrapArrowThreshold_MarksTimedTrapTriggerBlade()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.TimedTrapArrowThreshold));

            var trigger = new GridCoordinate(1, 0);
            var tile = grid.GetOrCreateTile(trigger);

            Assert.AreEqual(new GridCoordinate(2, 0), tile.TimedTrapTarget);
            Assert.AreEqual(TimedTrapType.Blade, tile.TimedTrapKind);
        }

        [Test]
        public void TileMaterialized_RollAtOrAboveTimedTrapBladeThreshold_TileStaysPassable()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.TimedTrapBladeThreshold));

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsFalse(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
            Assert.IsFalse(tile.ExplosiveTrapTarget.HasValue);
            Assert.IsFalse(tile.TimedTrapTarget.HasValue);
        }

        [Test]
        public void TileMaterialized_TargetOfExplosiveTrigger_IsNotIndependentlyRolled()
        {
            // Плита-цель триггера — резервирована: даже если бы ей самой
            // выпал бросок "заблокировано", она должна остаться обычной
            // (см. комментарий про _reservedTrapTargets в генераторе).
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.LavaThreshold, 0f));

            grid.GetOrCreateTile(new GridCoordinate(1, 0)); // становится триггером на (2, 0), резервирует цель
            var target = grid.GetOrCreateTile(new GridCoordinate(2, 0));

            Assert.IsFalse(target.IsBlocked);
            Assert.IsFalse(target.LethalTrap.HasValue);
        }

        [Test]
        public void TileMaterialized_TargetOfTimedTrapTrigger_IsNotIndependentlyRolled()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.ExplosiveTriggerThreshold, 0f));

            grid.GetOrCreateTile(new GridCoordinate(1, 0)); // становится триггером стрелы на (2, 0), резервирует цель
            var target = grid.GetOrCreateTile(new GridCoordinate(2, 0));

            Assert.IsFalse(target.IsBlocked);
            Assert.IsFalse(target.IsTimedTrapActive);
        }

        [Test]
        public void TileMaterialized_TargetOfExplosiveTrigger_ResolvedOnceThenRollsNormallyAgain()
        {
            // Резервация одноразовая: другая, никак не связанная плита с той
            // же координатой (в теории — другой забег/сетка) не должна
            // случайно остаться навсегда защищённой от бросков.
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.LavaThreshold, 0f));

            grid.GetOrCreateTile(new GridCoordinate(1, 0)); // триггер → резервирует (2, 0)
            grid.GetOrCreateTile(new GridCoordinate(2, 0)); // резервированная цель — бросок не тратится, снимается резервация
            var otherTile = grid.GetOrCreateTile(new GridCoordinate(3, 0)); // обычная плита — снова тратит бросок (второе значение очереди)

            Assert.IsTrue(otherTile.IsBlocked);
        }

        [Test]
        public void TileMaterialized_RowZero_NeverMarkedRegardlessOfRoll()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(0f));

            var tile = grid.GetOrCreateTile(new GridCoordinate(0, 2));

            Assert.IsFalse(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
        }

        [Test]
        public void TileMaterialized_RowZero_DoesNotConsumeRandomRoll()
        {
            // Если бы ряд 0 всё равно бросал кубик, второй Dequeue() на пустой очереди упал бы с исключением.
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(0f));

            grid.GetOrCreateTile(new GridCoordinate(0, 2));
            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsTrue(tile.IsBlocked);
        }

        // Разграничение по рядам с Generation.SegmentRowProvider (переходное
        // состояние сосуществования двух генераторов, docs/wiki/roadmap.md) —
        // проверка по конструкции: даже с бросками, которые ВСЕГДА требуют
        // "заблокировано", заявленный ряд остаётся нетронутым. Не полагается
        // на "договорённость" — это прямая проверка условия в OnTileMaterialized.
        [Test]
        public void TileMaterialized_RowClaimed_TileStaysUntouchedRegardlessOfRoll()
        {
            var grid = new TunnelGrid(5);
            grid.ClaimRow(1);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(0f, 0f, 0f, 0f, 0f)); // 0f — гарантированно ниже любого порога

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsFalse(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
            Assert.IsFalse(tile.ExplosiveTrapTarget.HasValue);
            Assert.IsFalse(tile.TimedTrapTarget.HasValue);
        }

        [Test]
        public void TileMaterialized_RowClaimed_DoesNotConsumeRandomRoll()
        {
            // Если бы заявленный ряд всё равно бросал кубик, второй Dequeue() на пустой очереди упал бы с исключением.
            var grid = new TunnelGrid(5);
            grid.ClaimRow(1);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(0f));

            grid.GetOrCreateTile(new GridCoordinate(1, 0)); // заявленный ряд — не должен тронуть очередь
            var tile = grid.GetOrCreateTile(new GridCoordinate(2, 0)); // незаявленный — тратит единственное значение очереди

            Assert.IsTrue(tile.IsBlocked);
        }

        [Test]
        public void TileMaterialized_OnlySpecificRowClaimed_OtherRowsStillRollNormally()
        {
            var grid = new TunnelGrid(5);
            grid.ClaimRow(1);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(0f)); // только для незаявленного ряда 2

            var claimedTile = grid.GetOrCreateTile(new GridCoordinate(1, 0));
            var normalTile = grid.GetOrCreateTile(new GridCoordinate(2, 0));

            Assert.IsFalse(claimedTile.IsBlocked, "Заявленный ряд не трогается генератором");
            Assert.IsTrue(normalTile.IsBlocked, "Незаявленный ряд по-прежнему засеивается генератором");
        }

        [Test]
        public void Dispose_StopsReactingToFurtherMaterializedTiles()
        {
            var grid = new TunnelGrid(5);
            var generator = new TunnelObstacleGenerator(grid, Sequence(0f));
            generator.Dispose();

            // После Dispose очередь бросков больше не трогается — исключения из пустой очереди не будет, плита останется непомеченной.
            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsFalse(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
        }
    }
}
