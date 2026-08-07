using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class GridTraceTrailTests
    {
        private static GridTraceTrail CreateTrail(GridCoordinate start)
        {
            var grid = new TunnelGrid(5);
            return new GridTraceTrail(grid, start);
        }

        [Test]
        public void Constructor_StartCoordinate_IsCurrentPositionAndOnlyPathEntry()
        {
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);

            Assert.AreEqual(start, trail.CurrentPosition);
            Assert.AreEqual(1, trail.Path.Count);
            Assert.AreEqual(start, trail.Path[0]);
        }

        [Test]
        public void CanAdvanceTo_AdjacentUnvisitedTile_ReturnsTrue()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));

            Assert.IsTrue(trail.CanAdvanceTo(new GridCoordinate(1, 2)));
        }

        [Test]
        public void CanAdvanceTo_NonAdjacentTile_ReturnsFalse()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));

            Assert.IsFalse(trail.CanAdvanceTo(new GridCoordinate(2, 2)));
        }

        [Test]
        public void CanAdvanceTo_OutOfGridBounds_ReturnsFalse()
        {
            var trail = CreateTrail(new GridCoordinate(0, 0));

            Assert.IsFalse(trail.CanAdvanceTo(new GridCoordinate(0, -1)));
            Assert.IsFalse(trail.CanAdvanceTo(new GridCoordinate(-1, 0)));
        }

        [Test]
        public void CanAdvanceTo_AlreadyVisitedNotDestroyedTile_ReturnsTrue()
        {
            // #61: повторный шаг на пройденную, но целую плиту — разрешён
            // (правило из прототипа/старой версии #6 отменено явным запросом).
            var trail = CreateTrail(new GridCoordinate(0, 2));
            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsTrue(trail.CanAdvanceTo(new GridCoordinate(0, 2)));
        }

        [Test]
        public void CanAdvanceTo_AlreadyVisitedDestroyedTile_ReturnsFalse()
        {
            // #61: блокируется только реально разрушенная распадом плита.
            var grid = new TunnelGrid(5);
            var start = new GridCoordinate(0, 2);
            var trail = new GridTraceTrail(grid, start);
            var previous = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(previous);

            var previousTile = grid.GetOrCreateTile(previous);
            previousTile.BeginDecay(1f);
            previousTile.AdvanceDecay(2f); // порог 1с превышен — плита разрушена
            Assert.IsTrue(previousTile.IsDestroyed, "тест некорректен, если плита не разрушилась");

            Assert.IsFalse(trail.CanAdvanceTo(previous));
        }

        [Test]
        public void TryAdvanceTo_ValidMove_AppendsToPathAndUpdatesCurrentPosition()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));
            var target = new GridCoordinate(1, 3);

            var advanced = trail.TryAdvanceTo(target);

            Assert.IsTrue(advanced);
            Assert.AreEqual(target, trail.CurrentPosition);
            Assert.AreEqual(2, trail.Path.Count);
        }

        [Test]
        public void TryAdvanceTo_InvalidMove_ReturnsFalseAndDoesNotMutatePath()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));

            var advanced = trail.TryAdvanceTo(new GridCoordinate(3, 3));

            Assert.IsFalse(advanced);
            Assert.AreEqual(1, trail.Path.Count);
        }

        [Test]
        public void TryAdvanceTo_ZigzagPathOverNeverVisitedTiles_AllStepsSucceed()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));

            Assert.IsTrue(trail.TryAdvanceTo(new GridCoordinate(1, 3)));
            Assert.IsTrue(trail.TryAdvanceTo(new GridCoordinate(2, 2)));
            Assert.IsTrue(trail.TryAdvanceTo(new GridCoordinate(3, 3)));
            Assert.AreEqual(4, trail.Path.Count);
        }

        // #61: повторный проход по не разрушенной плите — CurrentPosition
        // двигается, но Path остаётся списком уникальных плит без дублей
        // (см. обсуждение issue #61 — Decay/DebugVisuals рассчитывают на то,
        // что Path не содержит повторов).

        [Test]
        public void TryAdvanceTo_RevisitNotDestroyedTile_MovesCurrentPositionWithoutDuplicatingPath()
        {
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            var pathCountBeforeRevisit = trail.Path.Count;

            var revisited = trail.TryAdvanceTo(start);

            Assert.IsTrue(revisited);
            Assert.AreEqual(start, trail.CurrentPosition);
            Assert.AreEqual(pathCountBeforeRevisit, trail.Path.Count);
        }

        [Test]
        public void TryAdvanceTo_RevisitDestroyedTile_ReturnsFalseAndDoesNotMoveCurrentPosition()
        {
            var grid = new TunnelGrid(5);
            var start = new GridCoordinate(0, 2);
            var trail = new GridTraceTrail(grid, start);
            var previous = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(previous);
            trail.TryAdvanceTo(new GridCoordinate(2, 2)); // текущая позиция теперь не previous

            var previousTile = grid.GetOrCreateTile(previous);
            previousTile.BeginDecay(1f);
            previousTile.AdvanceDecay(2f);

            var revisited = trail.TryAdvanceTo(previous);

            Assert.IsFalse(revisited);
            Assert.AreEqual(new GridCoordinate(2, 2), trail.CurrentPosition);
        }

        [Test]
        public void TryAdvanceTo_RevisitNotDestroyedTile_DoesNotFireAdvancedEvent()
        {
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            var advancedCoordinates = new System.Collections.Generic.List<GridCoordinate>();
            trail.Advanced += c => advancedCoordinates.Add(c);

            trail.TryAdvanceTo(start);

            Assert.IsEmpty(advancedCoordinates);
        }

        [Test]
        public void TryAdvanceTo_NewTile_FiresAdvancedEventWithTargetCoordinate()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));
            var target = new GridCoordinate(1, 2);
            GridCoordinate? fired = null;
            trail.Advanced += c => fired = c;

            trail.TryAdvanceTo(target);

            Assert.AreEqual(target, fired);
        }

        [Test]
        public void TryAdvanceTo_AfterRevisit_CanContinueToNewAdjacentTile()
        {
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(start); // возврат на старт

            var advanced = trail.TryAdvanceTo(new GridCoordinate(1, 3)); // новая плита, соседняя старту

            Assert.IsTrue(advanced);
            Assert.AreEqual(new GridCoordinate(1, 3), trail.CurrentPosition);
            Assert.AreEqual(3, trail.Path.Count); // start, (1,2), (1,3) — без дублей
        }
    }
}
