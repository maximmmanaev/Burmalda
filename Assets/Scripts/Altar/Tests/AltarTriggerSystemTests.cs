using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Altar.Tests
{
    public class AltarTriggerSystemTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail) CreateTrail()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            return (grid, trail);
        }

        [Test]
        public void Advanced_ReachesAltar_RaisesAltarReached_WithoutAnyCurrency()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkAltar();
            using var system = new AltarTriggerSystem(grid, trail);
            GridCoordinate? reached = null;
            system.AltarReached += c => reached = c;

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.AreEqual(new GridCoordinate(1, 2), reached);
        }

        [Test]
        public void Advanced_NonAltarTile_DoesNotRaise()
        {
            var (grid, trail) = CreateTrail();
            using var system = new AltarTriggerSystem(grid, trail);
            var fired = false;
            system.AltarReached += _ => fired = true;

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsFalse(fired);
        }

        [Test]
        public void Dispose_StopsRaising()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkAltar();
            var system = new AltarTriggerSystem(grid, trail);
            var fired = false;
            system.AltarReached += _ => fired = true;
            system.Dispose();

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsFalse(fired);
        }

        [Test]
        public void VaultBonus_SteppingOnGateVault_GrantsOneExtraOption_ConsumedAtAltar()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkGateVault();
            using var tracker = new VaultBonusTracker(grid, trail);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.AreEqual(1, tracker.PendingExtraOptions);
            Assert.AreEqual(1, tracker.Consume());
            Assert.AreEqual(0, tracker.PendingExtraOptions);
            Assert.AreEqual(0, tracker.Consume(), "повторно бонус не выдаётся");
        }

        [Test]
        public void VaultBonus_TwoVaultsBeforeOneAltar_DoNotStack()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkGateVault();
            grid.GetOrCreateTile(new GridCoordinate(2, 2)).MarkGateVault();
            using var tracker = new VaultBonusTracker(grid, trail);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(new GridCoordinate(2, 2));

            Assert.AreEqual(VaultBonusTracker.MaxPendingOptions, tracker.PendingExtraOptions);
        }

        [Test]
        public void VaultBonus_OrdinaryTile_GrantsNothing()
        {
            var (grid, trail) = CreateTrail();
            using var tracker = new VaultBonusTracker(grid, trail);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.AreEqual(0, tracker.PendingExtraOptions);
        }
    }
}
