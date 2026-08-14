using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Currencies.Tests
{
    public class TrailCoinSystemTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail, TrailMultiplierSystem multiplier) CreateTrail()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var multiplier = new TrailMultiplierSystem(trail); // сконструирован ДО TrailCoinSystem — критично для порядка подписки на Advanced
            return (grid, trail, multiplier);
        }

        [Test]
        public void Advanced_FirstMove_AddsBaseCoinsTimesMultiplierOne()
        {
            var (_, trail, multiplier) = CreateTrail();
            var runCoins = new RunCurrencyAccumulator();
            using var coinSystem = new TrailCoinSystem(trail, multiplier, runCoins);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.AreEqual(TrailCoinSystem.BaseCoinsPerTile * 1, runCoins.Total);
        }

        [Test]
        public void Advanced_StraightPathAcrossMultiplierBoundary_UsesFreshMultiplierEachMove()
        {
            // Порт формулы из legacy/burmolda_demo.html: multi пересчитывается
            // ДО начисления runCoins для того же хода — а не с предыдущего.
            // 9 ходов по прямой: эффективная длина растёт 2..10, curve[2..9]=1,
            // curve[10]=2 => ходы 1-8 дают 1 монету, ход 9 даёт 2. Итого 10.
            var (_, trail, multiplier) = CreateTrail();
            var runCoins = new RunCurrencyAccumulator();
            using var coinSystem = new TrailCoinSystem(trail, multiplier, runCoins);

            for (var row = 1; row <= 9; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            Assert.AreEqual(2, multiplier.CurrentMultiplier);
            Assert.AreEqual(10, runCoins.Total);
        }

        [Test]
        public void Advanced_RevisitingAlreadyVisitedTile_DoesNotAddCoins()
        {
            var (_, trail, multiplier) = CreateTrail();
            var runCoins = new RunCurrencyAccumulator();
            using var coinSystem = new TrailCoinSystem(trail, multiplier, runCoins);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            var totalAfterFirstMove = runCoins.Total;

            trail.TryAdvanceTo(new GridCoordinate(0, 2)); // назад, уже пройденная целая плита

            Assert.AreEqual(totalAfterFirstMove, runCoins.Total);
        }

        [Test]
        public void Dispose_StopsReactingToFurtherAdvances()
        {
            var (_, trail, multiplier) = CreateTrail();
            var runCoins = new RunCurrencyAccumulator();
            var coinSystem = new TrailCoinSystem(trail, multiplier, runCoins);
            coinSystem.Dispose();

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.AreEqual(0, runCoins.Total);
        }
    }
}
