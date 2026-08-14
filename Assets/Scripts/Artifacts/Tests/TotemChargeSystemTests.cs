using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class TotemChargeSystemTests
    {
        private static (TunnelGrid grid, GridTraceTrail trail, Totem totem, TotemChargeSystem charge) CreateSystem(int totemLevel = 0)
        {
            var grid = new TunnelGrid(10);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 5));
            var totem = new Totem("t1", "Тестовый Тотем", TotemAbilityType.Breach);
            for (var i = 0; i < totemLevel; i++) totem.UpgradeLevel();
            var charge = new TotemChargeSystem(trail, totem);
            return (grid, trail, totem, charge);
        }

        [Test]
        public void NewSystem_StartsAtZeroChargeAndNotReady()
        {
            var (_, _, _, charge) = CreateSystem();

            Assert.AreEqual(0f, charge.Charge);
            Assert.IsFalse(charge.IsReady);
        }

        [Test]
        public void Advanced_LevelZero_AddsBaseChargePerNewTile()
        {
            // legacy/burmolda_demo.html: totemCharge+=2.5*totemLevel (totemLevel=1 в прототипе
            // соответствует Level=0 в этом проекте — см. doc-комментарий класса).
            var (_, trail, _, charge) = CreateSystem();

            trail.TryAdvanceTo(new GridCoordinate(1, 5));

            Assert.AreEqual(2.5f, charge.Charge, 1e-5f);
        }

        [Test]
        public void Advanced_UpgradedLevel_AddsProportionallyMoreCharge()
        {
            var (_, trail, _, charge) = CreateSystem(totemLevel: 1);

            trail.TryAdvanceTo(new GridCoordinate(1, 5));

            // ChargePerTileBase * (1 + Level) = 2.5 * 2 = 5.
            Assert.AreEqual(5f, charge.Charge, 1e-5f);
        }

        [Test]
        public void Advanced_RevisitAlreadyVisitedTile_DoesNotAddCharge()
        {
            // #61: повтор уже пройденной плиты не засчитывается Advanced-ом,
            // значит и заряд Тотема не должен расти.
            var (_, trail, _, charge) = CreateSystem();
            trail.TryAdvanceTo(new GridCoordinate(1, 5));
            var chargeAfterFirstAdvance = charge.Charge;

            trail.TryAdvanceTo(new GridCoordinate(0, 5)); // назад, на уже пройденную стартовую плиту

            Assert.AreEqual(chargeAfterFirstAdvance, charge.Charge, 1e-5f);
        }

        [Test]
        public void Charge_CapsAtMaxAndBecomesReady()
        {
            var (_, trail, _, charge) = CreateSystem();
            var current = new GridCoordinate(0, 5);

            for (var i = 1; i <= 45; i++) // 45 * 2.5 = 112.5 — с запасом выше потолка 100
            {
                var next = new GridCoordinate(i, 5);
                trail.TryAdvanceTo(next);
                current = next;
            }

            Assert.AreEqual(100f, charge.Charge);
            Assert.IsTrue(charge.IsReady);
        }

        [Test]
        public void ChargeChanged_StopsFiringOnceCappedAtMax()
        {
            var (_, trail, _, charge) = CreateSystem();
            for (var i = 1; i <= 40; i++) trail.TryAdvanceTo(new GridCoordinate(i, 5)); // достигает потолка

            var fired = false;
            charge.ChargeChanged += _ => fired = true;
            trail.TryAdvanceTo(new GridCoordinate(41, 5)); // уже на потолке — без изменений

            Assert.IsFalse(fired);
        }

        [Test]
        public void TryActivate_WhenNotReady_ReturnsFalseAndDoesNotResetCharge()
        {
            var (_, trail, _, charge) = CreateSystem();
            trail.TryAdvanceTo(new GridCoordinate(1, 5));
            var chargeBefore = charge.Charge;

            var activated = charge.TryActivate();

            Assert.IsFalse(activated);
            Assert.AreEqual(chargeBefore, charge.Charge);
        }

        [Test]
        public void TryActivate_WhenReady_ResetsChargeAndFiresActivated()
        {
            var (_, trail, _, charge) = CreateSystem();
            for (var i = 1; i <= 40; i++) trail.TryAdvanceTo(new GridCoordinate(i, 5));
            Assume.That(charge.IsReady, Is.True);

            var activatedFired = false;
            charge.Activated += () => activatedFired = true;
            var activated = charge.TryActivate();

            Assert.IsTrue(activated);
            Assert.IsTrue(activatedFired);
            Assert.AreEqual(0f, charge.Charge);
            Assert.IsFalse(charge.IsReady);
        }

        [Test]
        public void Dispose_StopsAccumulatingChargeOnFurtherAdvances()
        {
            var (_, trail, _, charge) = CreateSystem();
            charge.Dispose();

            trail.TryAdvanceTo(new GridCoordinate(1, 5));

            Assert.AreEqual(0f, charge.Charge);
        }
    }
}
