using System.Collections.Generic;
using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class TrailMultiplierSystemTests
    {
        private const int Width = 7;
        private static readonly GridCoordinate Start = new GridCoordinate(0, 3);

        private static (TunnelGrid grid, GridTraceTrail trail) CreateTrail()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, Start);
            return (grid, trail);
        }

        [Test]
        public void Constructor_SingleTileTrail_StartsAtMultiplierOneWithNoTurns()
        {
            var (_, trail) = CreateTrail();
            using var multiplierSystem = new TrailMultiplierSystem(trail);

            Assert.AreEqual(0, multiplierSystem.TurnCount);
            Assert.AreEqual(MultiplierCurve.GetMultiplier(1), multiplierSystem.CurrentMultiplier);
        }

        [Test]
        public void Advanced_FirstMove_NeverCountsAsATurn()
        {
            // Порт legacy/burmolda_demo.html: lastDir изначально null, первый
            // ход не с чем сравнивать — turnCount не растёт независимо от направления.
            var (_, trail) = CreateTrail();
            using var multiplierSystem = new TrailMultiplierSystem(trail);

            trail.TryAdvanceTo(new GridCoordinate(1, 3));

            Assert.AreEqual(0, multiplierSystem.TurnCount);
        }

        [Test]
        public void Advanced_SecondMoveSameDirection_DoesNotCountAsATurn()
        {
            var (_, trail) = CreateTrail();
            using var multiplierSystem = new TrailMultiplierSystem(trail);

            trail.TryAdvanceTo(new GridCoordinate(1, 3)); // вперёд
            trail.TryAdvanceTo(new GridCoordinate(2, 3)); // снова вперёд — то же направление

            Assert.AreEqual(0, multiplierSystem.TurnCount);
        }

        [Test]
        public void Advanced_SecondMoveDifferentDirection_CountsAsATurn()
        {
            var (_, trail) = CreateTrail();
            using var multiplierSystem = new TrailMultiplierSystem(trail);

            trail.TryAdvanceTo(new GridCoordinate(1, 3)); // вперёд
            trail.TryAdvanceTo(new GridCoordinate(1, 4)); // вбок — смена направления

            Assert.AreEqual(1, multiplierSystem.TurnCount);
        }

        [Test]
        public void Advanced_RevisitingAlreadyVisitedTile_DoesNotChangeTurnCountOrMultiplier()
        {
            // GridTraceTrail.Advanced не срабатывает при повторном шаге (#61)
            // — TrailMultiplierSystem подписан именно на Advanced, значит и
            // сам не должен реагировать на повтор.
            var (_, trail) = CreateTrail();
            using var multiplierSystem = new TrailMultiplierSystem(trail);
            trail.TryAdvanceTo(new GridCoordinate(1, 3));
            var turnsBefore = multiplierSystem.TurnCount;
            var multiplierBefore = multiplierSystem.CurrentMultiplier;

            trail.TryAdvanceTo(Start); // назад на уже пройденную (и не разрушенную) плиту

            Assert.AreEqual(turnsBefore, multiplierSystem.TurnCount);
            Assert.AreEqual(multiplierBefore, multiplierSystem.CurrentMultiplier);
        }

        [Test]
        public void CurrentMultiplier_StraightPath_MatchesCurveByUniqueTileCount()
        {
            var (_, trail) = CreateTrail();
            using var multiplierSystem = new TrailMultiplierSystem(trail);

            for (var row = 1; row <= 9; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 3));

            // Path.Count == 10 (старт + 9 ходов), TurnCount == 0 (прямая линия) => эффективная длина 10.
            Assert.AreEqual(10, trail.Path.Count);
            Assert.AreEqual(0, multiplierSystem.TurnCount);
            Assert.AreEqual(MultiplierCurve.GetMultiplier(10), multiplierSystem.CurrentMultiplier);
        }

        [Test]
        public void CurrentMultiplier_ZigzagPath_ReachesHigherCurveStepWithFewerTiles()
        {
            var (_, trail) = CreateTrail();
            using var multiplierSystem = new TrailMultiplierSystem(trail);

            // Зигзаг: каждый шаг меняет направление (вперёд-вбок чередуются) — 4 хода, 4 поворота.
            var zigzag = new List<GridCoordinate>
            {
                new GridCoordinate(1, 3), // вперёд
                new GridCoordinate(1, 4), // вбок — поворот 1
                new GridCoordinate(2, 4), // вперёд — поворот 2
                new GridCoordinate(2, 5), // вбок — поворот 3
            };
            foreach (var step in zigzag) trail.TryAdvanceTo(step);

            // Path.Count == 5 (старт + 4 хода), TurnCount == 3 (первый ход не считается) => эффективная длина 8.
            Assert.AreEqual(5, trail.Path.Count);
            Assert.AreEqual(3, multiplierSystem.TurnCount);
            Assert.AreEqual(MultiplierCurve.GetMultiplier(8), multiplierSystem.CurrentMultiplier);
        }

        [Test]
        public void MultiplierChanged_WithinSamePlateau_DoesNotFire()
        {
            var (_, trail) = CreateTrail();
            using var multiplierSystem = new TrailMultiplierSystem(trail);
            var fireCount = 0;
            multiplierSystem.MultiplierChanged += _ => fireCount++;

            // Path.Count 1 -> 2..9 остаётся на плато "1" (curve[1..9] == 1).
            for (var row = 1; row <= 8; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 3));

            Assert.AreEqual(0, fireCount);
        }

        [Test]
        public void MultiplierChanged_CrossingPlateauBoundary_FiresWithNewValue()
        {
            var (_, trail) = CreateTrail();
            using var multiplierSystem = new TrailMultiplierSystem(trail);
            var seenValues = new List<int>();
            multiplierSystem.MultiplierChanged += seenValues.Add;

            for (var row = 1; row <= 9; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 3)); // Path.Count 2..10

            CollectionAssert.AreEqual(new[] { 2 }, seenValues, "curve[1..9]==1, curve[10]==2 — событие только на самом переходе");
        }

        [Test]
        public void Dispose_StopsReactingToFurtherAdvances()
        {
            var (_, trail) = CreateTrail();
            var multiplierSystem = new TrailMultiplierSystem(trail);
            multiplierSystem.Dispose();

            trail.TryAdvanceTo(new GridCoordinate(1, 3));

            Assert.AreEqual(0, multiplierSystem.TurnCount);
            Assert.AreEqual(MultiplierCurve.GetMultiplier(1), multiplierSystem.CurrentMultiplier);
        }
    }
}
