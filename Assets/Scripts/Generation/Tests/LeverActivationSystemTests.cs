using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Generation.Tests
{
    public class LeverActivationSystemTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail) CreateTrail(GridCoordinate start)
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, start);
            return (grid, trail);
        }

        [Test]
        public void PositionChanged_TrailReachesLever_OpensAllGateTargets()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var lever = new GridCoordinate(1, 2);
            var gateA = new GridCoordinate(1, 3);
            var gateB = new GridCoordinate(2, 3);
            grid.GetOrCreateTile(lever).MarkLever(new[] { gateA, gateB });
            grid.GetOrCreateTile(gateA).MarkGated();
            grid.GetOrCreateTile(gateB).MarkGated();
            using var leverSystem = new LeverActivationSystem(grid, trail);

            trail.TryAdvanceTo(lever);

            Assert.IsTrue(grid.GetOrCreateTile(gateA).IsLeverGateOpen);
            Assert.IsTrue(grid.GetOrCreateTile(gateB).IsLeverGateOpen);
        }

        [Test]
        public void PositionChanged_NonLeverTile_DoesNotOpenAnything()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var gateA = new GridCoordinate(1, 3);
            grid.GetOrCreateTile(gateA).MarkGated();
            using var leverSystem = new LeverActivationSystem(grid, trail);

            trail.TryAdvanceTo(new GridCoordinate(1, 2)); // обычная плита, не рычаг

            Assert.IsFalse(grid.GetOrCreateTile(gateA).IsLeverGateOpen);
        }

        [Test]
        public void PositionChanged_TargetNotYetMaterialized_IsMaterializedAndOpened()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var lever = new GridCoordinate(1, 2);
            var gate = new GridCoordinate(1, 3);
            grid.GetOrCreateTile(lever).MarkLever(new[] { gate });
            using var leverSystem = new LeverActivationSystem(grid, trail);

            Assert.IsFalse(grid.TryGetTile(gate, out _), "цель ещё не должна быть материализована до активации рычага");

            trail.TryAdvanceTo(lever);

            Assert.IsTrue(grid.TryGetTile(gate, out var gateTile));
            Assert.IsTrue(gateTile.IsLeverGateOpen);
        }

        [Test]
        public void Dispose_StopsReactingToFurtherPositionChanges()
        {
            var (grid, trail) = CreateTrail(new GridCoordinate(0, 2));
            var lever = new GridCoordinate(1, 2);
            var gate = new GridCoordinate(1, 3);
            grid.GetOrCreateTile(lever).MarkLever(new[] { gate });
            grid.GetOrCreateTile(gate).MarkGated();
            var leverSystem = new LeverActivationSystem(grid, trail);
            leverSystem.Dispose();

            trail.TryAdvanceTo(lever);

            Assert.IsFalse(grid.GetOrCreateTile(gate).IsLeverGateOpen);
        }
    }
}
