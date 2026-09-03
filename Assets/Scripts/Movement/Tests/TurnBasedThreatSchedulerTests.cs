using System.Collections.Generic;
using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class TurnBasedThreatSchedulerTests
    {
        [Test]
        public void ScheduleActivation_TicksFromNowOne_FiresOnFirstTick()
        {
            var scheduler = new TurnBasedThreatScheduler();
            var coordinate = new GridCoordinate(1, 2);
            var fired = new List<GridCoordinate>();
            scheduler.TileDue += fired.Add;

            scheduler.ScheduleActivation(coordinate, ticksFromNow: 1);
            scheduler.Tick();

            Assert.AreEqual(new[] { coordinate }, fired);
        }

        [Test]
        public void ScheduleActivation_TicksFromNowTwo_DoesNotFireOnFirstTick_FiresOnSecondTick()
        {
            var scheduler = new TurnBasedThreatScheduler();
            var coordinate = new GridCoordinate(1, 2);
            var fired = new List<GridCoordinate>();
            scheduler.TileDue += fired.Add;

            scheduler.ScheduleActivation(coordinate, ticksFromNow: 2);
            scheduler.Tick();

            Assert.IsEmpty(fired, "через 1 ход из 2 запланированных срабатывать ещё рано");

            scheduler.Tick();

            Assert.AreEqual(new[] { coordinate }, fired);
        }

        // PRD-сценарий: Стрела — плиты ряда становятся опасными по очереди,
        // одна за другой, на последовательных ходах (docs/wiki/traps.md).
        [Test]
        public void ScheduleActivation_MultipleTilesWithDifferentTicks_FireInScheduledOrder()
        {
            var scheduler = new TurnBasedThreatScheduler();
            var col1 = new GridCoordinate(3, 1);
            var col2 = new GridCoordinate(3, 2);
            var col3 = new GridCoordinate(3, 3);
            var fired = new List<GridCoordinate>();
            scheduler.TileDue += fired.Add;

            scheduler.ScheduleActivation(col1, ticksFromNow: 1);
            scheduler.ScheduleActivation(col2, ticksFromNow: 2);
            scheduler.ScheduleActivation(col3, ticksFromNow: 3);

            scheduler.Tick();
            Assert.AreEqual(new[] { col1 }, fired);

            scheduler.Tick();
            Assert.AreEqual(new[] { col1, col2 }, fired);

            scheduler.Tick();
            Assert.AreEqual(new[] { col1, col2, col3 }, fired);
        }

        [Test]
        public void ScheduleActivation_ZeroOrNegativeTicks_Throws()
        {
            var scheduler = new TurnBasedThreatScheduler();
            var coordinate = new GridCoordinate(0, 0);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => scheduler.ScheduleActivation(coordinate, ticksFromNow: 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => scheduler.ScheduleActivation(coordinate, ticksFromNow: -1));
        }

        [Test]
        public void Tick_NoScheduledActivations_DoesNotInvokeTileDue()
        {
            var scheduler = new TurnBasedThreatScheduler();
            var invoked = false;
            scheduler.TileDue += _ => invoked = true;

            scheduler.Tick();

            Assert.IsFalse(invoked);
        }

        [Test]
        public void PendingCount_ReflectsRemainingActivations()
        {
            var scheduler = new TurnBasedThreatScheduler();
            scheduler.ScheduleActivation(new GridCoordinate(0, 0), ticksFromNow: 1);
            scheduler.ScheduleActivation(new GridCoordinate(0, 1), ticksFromNow: 2);

            Assert.AreEqual(2, scheduler.PendingCount);

            scheduler.Tick(); // первая активация срабатывает и уходит из очереди

            Assert.AreEqual(1, scheduler.PendingCount);
        }

        // PRD-сценарий: Лезвия — один и тот же столбец опасен несколько раз
        // за цикл (1&5 → 2&4 → 3 → 2&4 → 1&5), planировщик не схлопывает
        // повторные регистрации одной и той же плиты в одну.
        [Test]
        public void ScheduleActivation_SameCoordinateScheduledTwice_FiresIndependentlyEachTime()
        {
            var scheduler = new TurnBasedThreatScheduler();
            var coordinate = new GridCoordinate(2, 1);
            var fireCount = 0;
            scheduler.TileDue += _ => fireCount++;

            scheduler.ScheduleActivation(coordinate, ticksFromNow: 1);
            scheduler.ScheduleActivation(coordinate, ticksFromNow: 4);

            scheduler.Tick();
            Assert.AreEqual(1, fireCount);

            scheduler.Tick();
            scheduler.Tick();
            Assert.AreEqual(1, fireCount, "второй срок ещё не наступил");

            scheduler.Tick();
            Assert.AreEqual(2, fireCount);
        }

        [Test]
        public void Tick_AfterActivationFired_SubsequentTicksDoNothing()
        {
            var scheduler = new TurnBasedThreatScheduler();
            var coordinate = new GridCoordinate(0, 0);
            var fireCount = 0;
            scheduler.TileDue += _ => fireCount++;
            scheduler.ScheduleActivation(coordinate, ticksFromNow: 1);

            scheduler.Tick();
            scheduler.Tick();
            scheduler.Tick();

            Assert.AreEqual(1, fireCount);
            Assert.AreEqual(0, scheduler.PendingCount);
        }
    }
}
