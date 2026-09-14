using System.Collections.Generic;
using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    // Единственный планировщик отложенных угроз в проекте — тактовый
    // TurnBasedThreatScheduler (issue #212) удалён целиком вторым раундом
    // issue #254 (владелец: «никаких ловушек в такт быть не должно, только
    // тайминги»), эти тесты — его спецификация, что и была у удалённого
    // класса, но единица времени секунды, а не ходы.
    public class RealTimeThreatSchedulerTests
    {
        [Test]
        public void ScheduleActivation_SecondsFromNowOne_DoesNotFireBeforeDeadline_FiresAtOrAfter()
        {
            var scheduler = new RealTimeThreatScheduler();
            var coordinate = new GridCoordinate(1, 2);
            var fired = new List<GridCoordinate>();
            scheduler.TileDue += fired.Add;

            scheduler.ScheduleActivation(coordinate, secondsFromNow: 1f);
            scheduler.Tick(0.5f);

            Assert.IsEmpty(fired, "полсекунды из запланированной секунды — срабатывать ещё рано");

            scheduler.Tick(0.5f);

            Assert.AreEqual(new[] { coordinate }, fired);
        }

        [Test]
        public void ScheduleActivation_MultipleTilesWithDifferentDelays_FireInScheduledOrder()
        {
            var scheduler = new RealTimeThreatScheduler();
            var col1 = new GridCoordinate(3, 1);
            var col2 = new GridCoordinate(3, 2);
            var col3 = new GridCoordinate(3, 3);
            var fired = new List<GridCoordinate>();
            scheduler.TileDue += fired.Add;

            scheduler.ScheduleActivation(col1, secondsFromNow: 0.3f);
            scheduler.ScheduleActivation(col2, secondsFromNow: 0.6f);
            scheduler.ScheduleActivation(col3, secondsFromNow: 0.9f);

            scheduler.Tick(0.3f);
            Assert.AreEqual(new[] { col1 }, fired);

            scheduler.Tick(0.3f);
            Assert.AreEqual(new[] { col1, col2 }, fired);

            scheduler.Tick(0.3f);
            Assert.AreEqual(new[] { col1, col2, col3 }, fired);
        }

        // Ядро задачи #254: одна большая порция реального времени за один
        // кадр (игрок стоял на месте несколько секунд, кадры редкие/большие)
        // обязана продвинуть волну ровно так же, как та же сумма секунд,
        // накопленная за много мелких Tick — планировщик не завязан на
        // частоту вызовов, только на сумму deltaSeconds.
        [Test]
        public void Tick_SingleLargeDeltaCoveringMultipleSeconds_FiresImmediately()
        {
            var scheduler = new RealTimeThreatScheduler();
            var coordinate = new GridCoordinate(0, 0);
            var fired = false;
            scheduler.TileDue += _ => fired = true;
            scheduler.ScheduleActivation(coordinate, secondsFromNow: 1f);

            scheduler.Tick(5f); // игрок стоял на месте 5 секунд без единого хода

            Assert.IsTrue(fired, "большая порция реального времени за один Tick обязана продвинуть активацию, а не потеряться");
        }

        [Test]
        public void ScheduleActivation_ZeroOrNegativeSeconds_Throws()
        {
            var scheduler = new RealTimeThreatScheduler();
            var coordinate = new GridCoordinate(0, 0);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => scheduler.ScheduleActivation(coordinate, secondsFromNow: 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => scheduler.ScheduleActivation(coordinate, secondsFromNow: -1f));
        }

        [Test]
        public void Tick_NoScheduledActivations_DoesNotInvokeTileDue()
        {
            var scheduler = new RealTimeThreatScheduler();
            var invoked = false;
            scheduler.TileDue += _ => invoked = true;

            scheduler.Tick(1f);

            Assert.IsFalse(invoked);
        }

        [Test]
        public void PendingCount_ReflectsRemainingActivations()
        {
            var scheduler = new RealTimeThreatScheduler();
            scheduler.ScheduleActivation(new GridCoordinate(0, 0), secondsFromNow: 0.5f);
            scheduler.ScheduleActivation(new GridCoordinate(0, 1), secondsFromNow: 1f);

            Assert.AreEqual(2, scheduler.PendingCount);

            scheduler.Tick(0.5f); // первая активация срабатывает и уходит из очереди

            Assert.AreEqual(1, scheduler.PendingCount);
        }

        [Test]
        public void ScheduleActivation_SameCoordinateScheduledTwice_FiresIndependentlyEachTime()
        {
            var scheduler = new RealTimeThreatScheduler();
            var coordinate = new GridCoordinate(2, 1);
            var fireCount = 0;
            scheduler.TileDue += _ => fireCount++;

            scheduler.ScheduleActivation(coordinate, secondsFromNow: 0.3f);
            scheduler.ScheduleActivation(coordinate, secondsFromNow: 1.2f);

            scheduler.Tick(0.3f);
            Assert.AreEqual(1, fireCount);

            scheduler.Tick(0.6f);
            Assert.AreEqual(1, fireCount, "второй срок ещё не наступил");

            scheduler.Tick(0.3f);
            Assert.AreEqual(2, fireCount);
        }

        [Test]
        public void Tick_AfterActivationFired_SubsequentTicksDoNothing()
        {
            var scheduler = new RealTimeThreatScheduler();
            var coordinate = new GridCoordinate(0, 0);
            var fireCount = 0;
            scheduler.TileDue += _ => fireCount++;
            scheduler.ScheduleActivation(coordinate, secondsFromNow: 0.3f);

            scheduler.Tick(0.3f);
            scheduler.Tick(0.3f);
            scheduler.Tick(0.3f);

            Assert.AreEqual(1, fireCount);
            Assert.AreEqual(0, scheduler.PendingCount);
        }
    }
}
