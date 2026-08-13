using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class ExplosiveTrapArmingSystemTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail) CreateTrail(GridCoordinate start)
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, start);
            return (grid, trail);
        }

        [Test]
        public void PositionChanged_TrailReachesTrigger_ArmsTargetAsExplosion()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkExplosiveTrapTrigger(target);
            using var arming = new ExplosiveTrapArmingSystem(grid, trail);

            trail.TryAdvanceTo(trigger);

            Assert.AreEqual(LethalTrapType.Explosion, grid.GetOrCreateTile(target).LethalTrap);
        }

        [Test]
        public void PositionChanged_TargetNotYetMaterialized_IsMaterializedAndArmed()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkExplosiveTrapTrigger(target);
            using var arming = new ExplosiveTrapArmingSystem(grid, trail);

            Assert.IsFalse(grid.TryGetTile(target, out _), "цель ещё не должна быть материализована до срабатывания триггера");

            trail.TryAdvanceTo(trigger);

            Assert.IsTrue(grid.TryGetTile(target, out var armedTile));
            Assert.AreEqual(LethalTrapType.Explosion, armedTile.LethalTrap);
        }

        [Test]
        public void PositionChanged_TileWithoutTrigger_DoesNotArmAnything()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            using var arming = new ExplosiveTrapArmingSystem(grid, trail);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsFalse(grid.TryGetTile(new GridCoordinate(2, 2), out _));
        }

        [Test]
        public void PositionChanged_RevisitingTrigger_StaysIdempotent()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkExplosiveTrapTrigger(target);
            using var arming = new ExplosiveTrapArmingSystem(grid, trail);

            trail.TryAdvanceTo(trigger);
            trail.TryAdvanceTo(new GridCoordinate(0, 2)); // назад на трейл, ещё не разрушенный (#61)
            trail.TryAdvanceTo(trigger); // снова на триггер

            Assert.AreEqual(LethalTrapType.Explosion, grid.GetOrCreateTile(target).LethalTrap);
        }

        [Test]
        public void Dispose_StopsReactingToFurtherPositionChanges()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkExplosiveTrapTrigger(target);
            var arming = new ExplosiveTrapArmingSystem(grid, trail);
            arming.Dispose();

            trail.TryAdvanceTo(trigger);

            Assert.IsFalse(grid.TryGetTile(target, out _));
        }
    }
}
