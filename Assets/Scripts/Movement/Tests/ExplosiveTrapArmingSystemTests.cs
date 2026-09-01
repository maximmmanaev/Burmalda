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

        // Владелец, задача «разрушение плиты», продолжение (2026-09-01):
        // "триггер должен уметь уничтожать ключ под собой" — шаблоны
        // «выкуп»/«последний-рывок» (Generation.SegmentTemplateCatalog)
        // намеренно ставят взрывной триггер над ManaSource/KeySource.
        // Раньше это бросало InvalidOperationException в рантайме
        // (Tile.MarkLethalTrap → GuardAgainstConflictingRole) — найдено на
        // реальном билде. Теперь Tile.TransitionToLethalTrap (рантайм-API,
        // без стража) должен спокойно превратить награду в ловушку.
        [Test]
        public void PositionChanged_TargetAlreadyManaSource_TransitionsToExplosion_DoesNotThrow()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkExplosiveTrapTrigger(target);
            grid.GetOrCreateTile(target).MarkManaSource();
            using var arming = new ExplosiveTrapArmingSystem(grid, trail);

            Assert.DoesNotThrow(() => trail.TryAdvanceTo(trigger));

            var targetTile = grid.GetOrCreateTile(target);
            Assert.AreEqual(LethalTrapType.Explosion, targetTile.LethalTrap);
            Assert.IsTrue(targetTile.IsManaSource, "Флаг IsManaSource намеренно не сбрасывается (тот же принцип, что и у собранных источников) — визуальный слой/проходимость решают по LethalTrap, не по этому флагу.");
        }

        [Test]
        public void PositionChanged_TargetAlreadyKeySource_TransitionsToExplosion_DoesNotThrow()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkExplosiveTrapTrigger(target);
            grid.GetOrCreateTile(target).MarkKeySource();
            using var arming = new ExplosiveTrapArmingSystem(grid, trail);

            Assert.DoesNotThrow(() => trail.TryAdvanceTo(trigger));
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
