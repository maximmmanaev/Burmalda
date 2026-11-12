using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Currencies.Tests
{
    /// <summary>Заряд Идола (PRD v10 §12.2, §18.1): каждая плита серии заполняет шкалу, обрыв серии заряд не сбрасывает.</summary>
    public class IdolChargeSystemTests
    {
        private int _oldCapacity;

        [SetUp]
        public void Setup()
        {
            _oldCapacity = IdolChargeSystem.Capacity;
            IdolChargeSystem.Capacity = 4;
        }

        [TearDown]
        public void Teardown()
        {
            IdolChargeSystem.Capacity = _oldCapacity;
            RunEffects.Current = RunEffects.Neutral;
        }

        private static (TunnelGrid grid, GridTraceTrail trail, CleanPassStreakSystem streak) Make()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            return (grid, trail, new CleanPassStreakSystem(grid, trail, new RunCurrencyAccumulator()));
        }

        private static void Walk(GridTraceTrail trail, int steps)
        {
            for (var i = 0; i < steps; i++) Assert.IsTrue(trail.TryAdvanceTo(new GridCoordinate(trail.CurrentPosition.Row + 1, 2)));
        }

        [Test]
        public void EachStreakTile_FillsTheBar_FullBarIsOneUse()
        {
            var (_, trail, streak) = Make();
            using var charge = new IdolChargeSystem(streak, () => true);

            Walk(trail, 3);
            Assert.AreEqual(3, charge.Charge);
            Assert.IsFalse(charge.IsFull);
            Assert.IsFalse(charge.TryConsume(), "не заряжен — нечего тратить");

            Walk(trail, 1);
            Assert.IsTrue(charge.IsFull);
            Assert.AreEqual(1f, charge.Fill);
            Assert.IsTrue(charge.TryConsume());
            Assert.AreEqual(0, charge.Charge);
        }

        [Test]
        public void BreakingTheStreak_DoesNotResetCharge()
        {
            var (grid, trail, streak) = Make();
            grid.GetOrCreateTile(new GridCoordinate(3, 2)).MarkBombTrigger();
            using var charge = new IdolChargeSystem(streak, () => true);

            Walk(trail, 2);
            Assert.AreEqual(2, charge.Charge);
            Walk(trail, 1); // плита-триггер: серия оборвана
            Assert.AreEqual(0, streak.Streak);
            Assert.AreEqual(2, charge.Charge, "заряд сохранился");

            Walk(trail, 1); // серия начинается заново и снова заряжает
            Assert.AreEqual(3, charge.Charge);
        }

        [Test]
        public void NoIdolEquipped_NoChargeAccumulates()
        {
            var (_, trail, streak) = Make();
            using var charge = new IdolChargeSystem(streak, () => false);

            Walk(trail, 5);

            Assert.AreEqual(0, charge.Charge);
        }

        [Test]
        public void ChargeIsCapped()
        {
            var (_, trail, streak) = Make();
            using var charge = new IdolChargeSystem(streak, () => true);

            Walk(trail, 9);

            Assert.AreEqual(IdolChargeSystem.Capacity, charge.Charge);
        }
    }
}
