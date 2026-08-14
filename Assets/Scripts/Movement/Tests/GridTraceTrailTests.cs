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
        public void CanAdvanceTo_UnvisitedNotYetMaterializedTile_ReturnsTrue()
        {
            // Регрессия: плита, до которой ещё никто не дотрагивался, не
            // материализована и не может быть препятствием по определению.
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));

            Assert.IsTrue(trail.CanAdvanceTo(new GridCoordinate(1, 2)));
        }

        [Test]
        public void CanAdvanceTo_UnvisitedBlockedTile_ReturnsFalse()
        {
            // #9: статичное препятствие — видно заранее, обойти нельзя, наступить нельзя.
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var blocked = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(blocked).MarkBlocked();

            Assert.IsFalse(trail.CanAdvanceTo(blocked));
        }

        [Test]
        public void TryAdvanceTo_BlockedTile_DoesNotAdvanceAndDoesNotMutatePath()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var blocked = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(blocked).MarkBlocked();

            var advanced = trail.TryAdvanceTo(blocked);

            Assert.IsFalse(advanced);
            Assert.AreEqual(new GridCoordinate(0, 2), trail.CurrentPosition);
            Assert.AreEqual(1, trail.Path.Count);
        }

        [Test]
        public void CanAdvanceTo_ClosedGatedTile_ReturnsFalse()
        {
            // #51: плита бокового прохода закрыта, пока не активирован рычаг.
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var gated = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(gated).MarkGated();

            Assert.IsFalse(trail.CanAdvanceTo(gated));
        }

        [Test]
        public void CanAdvanceTo_OpenedGatedTile_ReturnsTrue()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var gated = new GridCoordinate(1, 2);
            var tile = grid.GetOrCreateTile(gated);
            tile.MarkGated();
            tile.OpenLeverGate();

            Assert.IsTrue(trail.CanAdvanceTo(gated));
        }

        [Test]
        public void CanAdvanceTo_UnvisitedLethalTrapTile_ReturnsFalse()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var pit = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(pit).MarkLethalTrap(LethalTrapType.Pit);

            Assert.IsFalse(trail.CanAdvanceTo(pit));
        }

        [Test]
        public void TryAdvanceTo_LethalTrapTile_DoesNotAdvanceAndDoesNotMutatePath()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var lava = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(lava).MarkLethalTrap(LethalTrapType.Lava);

            var advanced = trail.TryAdvanceTo(lava);

            Assert.IsFalse(advanced);
            Assert.AreEqual(new GridCoordinate(0, 2), trail.CurrentPosition);
            Assert.AreEqual(1, trail.Path.Count);
        }

        [Test]
        public void TryAdvanceTo_LethalTrapTile_FiresLethalTrapTriggeredWithCoordinateAndType()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var pit = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(pit).MarkLethalTrap(LethalTrapType.Pit);
            GridCoordinate? firedCoordinate = null;
            LethalTrapType? firedType = null;
            trail.LethalTrapTriggered += (coordinate, type) =>
            {
                firedCoordinate = coordinate;
                firedType = type;
            };

            trail.TryAdvanceTo(pit);

            Assert.AreEqual(pit, firedCoordinate);
            Assert.AreEqual(LethalTrapType.Pit, firedType);
        }

        [Test]
        public void TryAdvanceTo_BlockedTile_DoesNotFireLethalTrapTriggered()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var blocked = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(blocked).MarkBlocked();
            var fired = false;
            trail.LethalTrapTriggered += (_, _) => fired = true;

            trail.TryAdvanceTo(blocked);

            Assert.IsFalse(fired, "заблокированная плита — не ловушка, событие смерти не при чём");
        }

        [Test]
        public void TryAdvanceTo_ValidMove_DoesNotFireLethalTrapTriggered()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));
            var fired = false;
            trail.LethalTrapTriggered += (_, _) => fired = true;

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsFalse(fired);
        }

        [Test]
        public void CanAdvanceTo_ActiveTimedTrapTile_ReturnsFalse()
        {
            // #45: пока ловушка с таймингом активна, плита-цель непроходима.
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var target = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(target).ArmTimedTrap(TimedTrapType.Arrow);

            Assert.IsFalse(trail.CanAdvanceTo(target));
        }

        [Test]
        public void CanAdvanceTo_DisarmedTimedTrapTile_ReturnsTrue()
        {
            // Снаряд/лезвие уже прошли — плита снова безопасна.
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var target = new GridCoordinate(1, 2);
            var tile = grid.GetOrCreateTile(target);
            tile.ArmTimedTrap(TimedTrapType.Arrow);
            tile.DisarmTimedTrap();

            Assert.IsTrue(trail.CanAdvanceTo(target));
        }

        [Test]
        public void TryAdvanceTo_ActiveTimedTrapTile_DoesNotAdvanceAndDoesNotMutatePath()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var target = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(target).ArmTimedTrap(TimedTrapType.Blade);

            var advanced = trail.TryAdvanceTo(target);

            Assert.IsFalse(advanced);
            Assert.AreEqual(new GridCoordinate(0, 2), trail.CurrentPosition);
            Assert.AreEqual(1, trail.Path.Count);
        }

        [Test]
        public void TryAdvanceTo_ActiveTimedTrapTile_FiresTimedTrapTriggeredWithCoordinateAndType()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var target = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(target).ArmTimedTrap(TimedTrapType.Blade);
            GridCoordinate? firedCoordinate = null;
            TimedTrapType? firedType = null;
            trail.TimedTrapTriggered += (coordinate, type) =>
            {
                firedCoordinate = coordinate;
                firedType = type;
            };

            trail.TryAdvanceTo(target);

            Assert.AreEqual(target, firedCoordinate);
            Assert.AreEqual(TimedTrapType.Blade, firedType);
        }

        [Test]
        public void TryAdvanceTo_ValidMove_DoesNotFireTimedTrapTriggered()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));
            var fired = false;
            trail.TimedTrapTriggered += (_, _) => fired = true;

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsFalse(fired);
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
        public void TryAdvanceTo_NewTile_FiresPositionChangedEventWithTargetCoordinate()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));
            var target = new GridCoordinate(1, 2);
            GridCoordinate? fired = null;
            trail.PositionChanged += c => fired = c;

            trail.TryAdvanceTo(target);

            Assert.AreEqual(target, fired);
        }

        [Test]
        public void TryAdvanceTo_RevisitNotDestroyedTile_FiresPositionChangedEvent()
        {
            // В отличие от Advanced (не срабатывает на повтор, #61),
            // PositionChanged должен срабатывать на любой успешный ход —
            // иначе следящие за текущей позицией системы (например, камера)
            // не смогут узнать, что игрок вернулся назад по трейлу.
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            GridCoordinate? fired = null;
            trail.PositionChanged += c => fired = c;

            trail.TryAdvanceTo(start);

            Assert.AreEqual(start, fired);
        }

        [Test]
        public void TryAdvanceTo_InvalidMove_DoesNotFirePositionChangedEvent()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));
            var firedCount = 0;
            trail.PositionChanged += _ => firedCount++;

            trail.TryAdvanceTo(new GridCoordinate(3, 3));

            Assert.AreEqual(0, firedCount);
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

        [Test]
        public void TeleportTo_SetsCurrentPosition()
        {
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            trail.TeleportTo(start);

            Assert.AreEqual(start, trail.CurrentPosition);
        }

        [Test]
        public void TeleportTo_NonAdjacentCoordinate_Succeeds()
        {
            // Не ход игрока — соседство не требуется (#24, откат к Алтарю может быть далеко).
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);

            trail.TeleportTo(new GridCoordinate(4, 0));

            Assert.AreEqual(new GridCoordinate(4, 0), trail.CurrentPosition);
        }

        [Test]
        public void TeleportTo_RaisesPositionChangedButNotAdvanced()
        {
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);
            var advancedFired = false;
            GridCoordinate? positionChanged = null;
            trail.Advanced += _ => advancedFired = true;
            trail.PositionChanged += c => positionChanged = c;

            trail.TeleportTo(new GridCoordinate(3, 3));

            Assert.IsFalse(advancedFired);
            Assert.AreEqual(new GridCoordinate(3, 3), positionChanged);
        }

        [Test]
        public void TeleportTo_DoesNotMutatePathOrVisitedSet()
        {
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            var pathCountBefore = trail.Path.Count;

            trail.TeleportTo(new GridCoordinate(4, 4));

            Assert.AreEqual(pathCountBefore, trail.Path.Count);
        }

        [Test]
        public void TeleportTo_OutOfGridBounds_Throws()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));

            Assert.Throws<System.ArgumentOutOfRangeException>(() => trail.TeleportTo(new GridCoordinate(0, 99)));
        }
    }
}
