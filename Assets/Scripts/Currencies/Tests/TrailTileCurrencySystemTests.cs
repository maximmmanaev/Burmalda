using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Currencies.Tests
{
    public class TrailTileCurrencySystemTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail) CreateTrail()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            return (grid, trail);
        }

        [Test]
        public void Advanced_ReachesSourceTile_AddsAmountPerSourceToAccumulator()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkManaSource();
            var accumulator = new RunCurrencyAccumulator();
            using var system = new TrailTileCurrencySystem(grid, trail, t => t.IsManaSource, 3, accumulator);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.AreEqual(3, accumulator.Total);
        }

        [Test]
        public void Advanced_NonSourceTile_DoesNotAddToAccumulator()
        {
            var (grid, trail) = CreateTrail();
            var accumulator = new RunCurrencyAccumulator();
            using var system = new TrailTileCurrencySystem(grid, trail, t => t.IsManaSource, 3, accumulator);

            trail.TryAdvanceTo(new GridCoordinate(1, 2)); // обычная плита, не источник

            Assert.AreEqual(0, accumulator.Total);
        }

        [Test]
        public void Advanced_UsesProvidedPredicate_IgnoresOtherSourceTypes()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkKeySource(); // не Mana
            var accumulator = new RunCurrencyAccumulator();
            using var system = new TrailTileCurrencySystem(grid, trail, t => t.IsManaSource, 3, accumulator);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.AreEqual(0, accumulator.Total);
        }

        [Test]
        public void Advanced_MultipleSourceTiles_AccumulatesAcrossAll()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkManaSource();
            grid.GetOrCreateTile(new GridCoordinate(2, 2)).MarkManaSource();
            var accumulator = new RunCurrencyAccumulator();
            using var system = new TrailTileCurrencySystem(grid, trail, t => t.IsManaSource, 1, accumulator);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(new GridCoordinate(2, 2));

            Assert.AreEqual(2, accumulator.Total);
        }

        [Test]
        public void Advanced_RevisitingAlreadyVisitedSourceTile_DoesNotDoubleCount()
        {
            // Подписан на GridTraceTrail.Advanced — срабатывает только на
            // по-настоящему новых плитах (#61), не на повторных шагах.
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkManaSource();
            var accumulator = new RunCurrencyAccumulator();
            using var system = new TrailTileCurrencySystem(grid, trail, t => t.IsManaSource, 1, accumulator);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            trail.TryAdvanceTo(new GridCoordinate(0, 2)); // назад
            trail.TryAdvanceTo(new GridCoordinate(1, 2)); // повторно на источник

            Assert.AreEqual(1, accumulator.Total);
        }

        [Test]
        public void Dispose_StopsReactingToFurtherAdvances()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkManaSource();
            var accumulator = new RunCurrencyAccumulator();
            var system = new TrailTileCurrencySystem(grid, trail, t => t.IsManaSource, 1, accumulator);
            system.Dispose();

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.AreEqual(0, accumulator.Total);
        }
    }
}
