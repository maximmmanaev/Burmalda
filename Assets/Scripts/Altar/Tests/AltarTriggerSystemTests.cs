using Burmalda.Artifacts;
using Burmalda.Core;
using Burmalda.Currencies;
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
        public void Advanced_ReachesAltarWithKeys_OpensRitual()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkAltar();
            var keys = new RunCurrencyAccumulator();
            keys.Add(10);
            using var system = new AltarTriggerSystem(grid, trail, keys, new ArtifactPool(), () => 0f);
            Ritual opened = null;
            system.RitualOpened += r => opened = r;

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsNotNull(opened);
        }

        [Test]
        public void Advanced_ReachesAltarWithoutKeys_DoesNotOpenRitual()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkAltar();
            var keys = new RunCurrencyAccumulator(); // 0
            using var system = new AltarTriggerSystem(grid, trail, keys, new ArtifactPool(), () => 0f);
            var fired = false;
            system.RitualOpened += _ => fired = true;

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsFalse(fired, "без Ключей игрок проходит мимо Алтаря без потерь");
        }

        [Test]
        public void Advanced_NonAltarTile_DoesNotOpenRitual()
        {
            var (grid, trail) = CreateTrail();
            var keys = new RunCurrencyAccumulator();
            keys.Add(10);
            using var system = new AltarTriggerSystem(grid, trail, keys, new ArtifactPool(), () => 0f);
            var fired = false;
            system.RitualOpened += _ => fired = true;

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsFalse(fired);
        }

        [Test]
        public void Advanced_RevisitingAltar_DoesNotReopenRitual()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkAltar();
            var keys = new RunCurrencyAccumulator();
            keys.Add(10);
            using var system = new AltarTriggerSystem(grid, trail, keys, new ArtifactPool(), () => 0f);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            var openCount = 0;
            system.RitualOpened += _ => openCount++;

            trail.TryAdvanceTo(new GridCoordinate(0, 2)); // назад
            trail.TryAdvanceTo(new GridCoordinate(1, 2)); // снова на алтарь — не новая плита (#61)

            Assert.AreEqual(0, openCount);
        }

        [Test]
        public void Dispose_StopsReactingToFurtherAdvances()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkAltar();
            var keys = new RunCurrencyAccumulator();
            keys.Add(10);
            var system = new AltarTriggerSystem(grid, trail, keys, new ArtifactPool(), () => 0f);
            system.Dispose();
            var fired = false;
            system.RitualOpened += _ => fired = true;

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsFalse(fired);
        }
    }
}
