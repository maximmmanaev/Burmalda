using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class TimedTrapSystemTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail) CreateTrail(GridCoordinate start)
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, start);
            return (grid, trail);
        }

        [Test]
        public void PositionChanged_TrailReachesTrigger_DoesNotArmTargetImmediately()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkTimedTrapTrigger(target, TimedTrapType.Arrow);
            using var timedTrap = new TimedTrapSystem(grid, trail);

            trail.TryAdvanceTo(trigger);

            Assert.IsFalse(grid.GetOrCreateTile(target).IsTimedTrapActive, "должна быть фаза подготовки, а не мгновенная активация");
        }

        [Test]
        public void Tick_BeforeWindUpElapses_TargetStaysInactive()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkTimedTrapTrigger(target, TimedTrapType.Arrow);
            using var timedTrap = new TimedTrapSystem(grid, trail);
            trail.TryAdvanceTo(trigger);

            timedTrap.Tick(TimedTrapSystem.WindUpSeconds - 0.01f);

            Assert.IsFalse(grid.GetOrCreateTile(target).IsTimedTrapActive);
        }

        [Test]
        public void Tick_WindUpElapses_ArmsTargetWithTriggerKind()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkTimedTrapTrigger(target, TimedTrapType.Blade);
            using var timedTrap = new TimedTrapSystem(grid, trail);
            trail.TryAdvanceTo(trigger);

            timedTrap.Tick(TimedTrapSystem.WindUpSeconds);

            var targetTile = grid.GetOrCreateTile(target);
            Assert.IsTrue(targetTile.IsTimedTrapActive);
            Assert.AreEqual(TimedTrapType.Blade, targetTile.TimedTrapKind);
        }

        [Test]
        public void Tick_TargetNotYetMaterialized_IsMaterializedAndArmed()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkTimedTrapTrigger(target, TimedTrapType.Arrow);
            using var timedTrap = new TimedTrapSystem(grid, trail);

            Assert.IsFalse(grid.TryGetTile(target, out _), "цель ещё не должна быть материализована до срабатывания триггера");

            trail.TryAdvanceTo(trigger);
            timedTrap.Tick(TimedTrapSystem.WindUpSeconds);

            Assert.IsTrue(grid.TryGetTile(target, out var armedTile));
            Assert.IsTrue(armedTile.IsTimedTrapActive);
        }

        [Test]
        public void Tick_ActivePhaseElapses_DisarmsTargetPermanently()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkTimedTrapTrigger(target, TimedTrapType.Arrow);
            using var timedTrap = new TimedTrapSystem(grid, trail);
            trail.TryAdvanceTo(trigger);
            timedTrap.Tick(TimedTrapSystem.WindUpSeconds);

            timedTrap.Tick(TimedTrapSystem.ActiveSeconds);

            Assert.IsFalse(grid.GetOrCreateTile(target).IsTimedTrapActive);
        }

        [Test]
        public void Tick_AfterDisarm_FurtherTicksDoNotReArm()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkTimedTrapTrigger(target, TimedTrapType.Arrow);
            using var timedTrap = new TimedTrapSystem(grid, trail);
            trail.TryAdvanceTo(trigger);
            timedTrap.Tick(TimedTrapSystem.WindUpSeconds);
            timedTrap.Tick(TimedTrapSystem.ActiveSeconds);

            timedTrap.Tick(100f);

            Assert.IsFalse(grid.GetOrCreateTile(target).IsTimedTrapActive);
        }

        [Test]
        public void PositionChanged_RevisitingAlreadyFiredTrigger_DoesNotQueueSecondTimer()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkTimedTrapTrigger(target, TimedTrapType.Arrow);
            using var timedTrap = new TimedTrapSystem(grid, trail);
            trail.TryAdvanceTo(trigger);
            timedTrap.Tick(TimedTrapSystem.WindUpSeconds); // цель активна
            timedTrap.Tick(TimedTrapSystem.ActiveSeconds); // цель снова безопасна, таймер снят

            trail.TryAdvanceTo(new GridCoordinate(0, 2)); // назад
            trail.TryAdvanceTo(trigger); // повторно на триггер — не должно поставить новый таймер
            timedTrap.Tick(TimedTrapSystem.WindUpSeconds);

            Assert.IsFalse(grid.GetOrCreateTile(target).IsTimedTrapActive, "триггер одноразовый — повторный проход не должен снова вооружить цель");
        }

        [Test]
        public void Dispose_StopsReactingToFurtherPositionChanges()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkTimedTrapTrigger(target, TimedTrapType.Arrow);
            var timedTrap = new TimedTrapSystem(grid, trail);
            timedTrap.Dispose();

            trail.TryAdvanceTo(trigger);
            timedTrap.Tick(TimedTrapSystem.WindUpSeconds);

            Assert.IsFalse(grid.TryGetTile(target, out _));
        }
    }
}
