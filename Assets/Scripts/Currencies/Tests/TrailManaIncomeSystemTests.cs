using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Currencies.Tests
{
    public class TrailManaIncomeSystemTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail, TrailMultiplierSystem multiplier) CreateTrail()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var multiplier = new TrailMultiplierSystem(trail); // сконструирован ДО TrailManaIncomeSystem — критично для порядка подписки на Advanced
            return (grid, trail, multiplier);
        }

        [Test]
        public void Advanced_FirstMove_AddsBaseManaTimesMultiplierOne()
        {
            var (_, trail, multiplier) = CreateTrail();
            var runMana = new RunCurrencyAccumulator();
            using var manaSystem = new TrailManaIncomeSystem(trail, multiplier, runMana);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.AreEqual(TrailManaIncomeSystem.BaseManaPerTile * 1, runMana.Total);
        }

        [Test]
        public void Advanced_StraightPathAcrossMultiplierBoundary_UsesFreshMultiplierEachMove()
        {
            // Порт формулы из legacy/burmolda_demo.html: multi пересчитывается
            // ДО начисления runMana для того же хода — а не с предыдущего.
            // 9 ходов по прямой: эффективная длина растёт 2..10, curve[2..9]=1,
            // curve[10]=2 => ходы 1-8 дают BaseManaPerTile(10), ход 9 даёт 2×10=20.
            // Итого 8×10 + 20 = 100.
            var (_, trail, multiplier) = CreateTrail();
            var runMana = new RunCurrencyAccumulator();
            using var manaSystem = new TrailManaIncomeSystem(trail, multiplier, runMana);

            for (var row = 1; row <= 9; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            Assert.AreEqual(2, multiplier.CurrentMultiplier);
            Assert.AreEqual(100, runMana.Total);
        }

        [Test]
        public void Advanced_RevisitingAlreadyVisitedTile_DoesNotAddMana()
        {
            var (_, trail, multiplier) = CreateTrail();
            var runMana = new RunCurrencyAccumulator();
            using var manaSystem = new TrailManaIncomeSystem(trail, multiplier, runMana);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            var totalAfterFirstMove = runMana.Total;

            trail.TryAdvanceTo(new GridCoordinate(0, 2)); // назад, уже пройденная целая плита

            Assert.AreEqual(totalAfterFirstMove, runMana.Total);
        }

        [Test]
        public void Dispose_StopsReactingToFurtherAdvances()
        {
            var (_, trail, multiplier) = CreateTrail();
            var runMana = new RunCurrencyAccumulator();
            var manaSystem = new TrailManaIncomeSystem(trail, multiplier, runMana);
            manaSystem.Dispose();

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.AreEqual(0, runMana.Total);
        }
    }
}
