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
        public void Advanced_WithRewardMultiplier_ScalesAddedAmount()
        {
            // PRD v7 §20, Знамение «Хрупкий Свод»: Кристаллы Маны ×1.5.
            // amountPerSource=2 намеренно — 2*1.5=3.0 без округления, чтобы
            // тест не зависел от режима округления Math.Round на границе .5.
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkManaSource();
            var accumulator = new RunCurrencyAccumulator();
            using var system = new TrailTileCurrencySystem(grid, trail, t => t.IsManaSource, 2, accumulator, rewardMultiplier: 1.5f);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.AreEqual(3, accumulator.Total);
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

        // Перегрузка Func<Tile,int> (задача «размер награды за Воротами»,
        // владелец, 2026-09-04) — сумма считается ПО ПЛИТЕ, не единая.
        [Test]
        public void Advanced_AmountForTileOverload_UsesPerTileAmount()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkKeySource(120); // тайник — своя сумма
            grid.GetOrCreateTile(new GridCoordinate(2, 2)).MarkKeySource(); // обычный источник — сумма по умолчанию
            var accumulator = new RunCurrencyAccumulator();
            using var system = new TrailTileCurrencySystem(grid, trail, t => t.IsKeySource, t => t.KeySourceAmount ?? 15, accumulator);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(new GridCoordinate(2, 2));

            Assert.AreEqual(120 + 15, accumulator.Total);
        }

        [Test]
        public void Advanced_AmountForTileOverload_WithRewardMultiplier_ScalesPerTileAmount()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkKeySource(80);
            var accumulator = new RunCurrencyAccumulator();
            using var system = new TrailTileCurrencySystem(grid, trail, t => t.IsKeySource, t => t.KeySourceAmount ?? 15, accumulator, rewardMultiplier: 2f);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.AreEqual(160, accumulator.Total);
        }
    }
}
