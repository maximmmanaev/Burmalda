using Burmalda.Core;
using Burmalda.Decay;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class TotemAbilityActivationSystemTests
    {
        private static (TunnelGrid grid, GridTraceTrail trail, TrailDecaySystem decay, Totem totem, TotemChargeSystem charge, TotemAbilityActivationSystem activation)
            CreateSystem(TotemAbilityType ability)
        {
            var grid = new TunnelGrid(10);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 5));
            var decay = new TrailDecaySystem(grid, trail);
            var totem = new Totem("t1", "Тестовый Тотем", ability);
            var charge = new TotemChargeSystem(trail, totem);
            var activation = new TotemAbilityActivationSystem(trail, decay, totem, charge);
            return (grid, trail, decay, totem, charge, activation);
        }

        private static void FillCharge((TunnelGrid grid, GridTraceTrail trail, TrailDecaySystem decay, Totem totem, TotemChargeSystem charge, TotemAbilityActivationSystem activation) system, GridCoordinate from)
        {
            var current = from;
            for (var i = 0; i < 40; i++) // с запасом выше потолка 100 (40*2.5=100)
            {
                var next = new GridCoordinate(current.Row + 1, current.Column);
                system.trail.TryAdvanceTo(next);
                current = next;
            }
        }

        [Test]
        public void TryActivate_ChargeNotReady_ReturnsFalseAndAppliesNoEffect()
        {
            var system = CreateSystem(TotemAbilityType.Breach);

            var activated = system.activation.TryActivate();

            Assert.IsFalse(activated);
            Assert.AreEqual(0f, system.charge.Charge); // не тронут — заряда и так не было
        }

        [Test]
        public void TryActivate_Breach_PrimesBreachAndPassesBlockedTile()
        {
            var system = CreateSystem(TotemAbilityType.Breach);
            FillCharge(system, new GridCoordinate(0, 5));
            var afterFill = system.trail.CurrentPosition;
            var blocked = new GridCoordinate(afterFill.Row + 1, afterFill.Column);
            system.grid.GetOrCreateTile(blocked).MarkBlocked();

            var activated = system.activation.TryActivate();
            var advanced = system.trail.TryAdvanceTo(blocked);

            Assert.IsTrue(activated);
            Assert.IsTrue(advanced);
            Assert.AreEqual(blocked, system.trail.CurrentPosition);
        }

        [Test]
        public void TryActivate_Invulnerability_SuspendsDecay()
        {
            var system = CreateSystem(TotemAbilityType.Invulnerability);
            FillCharge(system, new GridCoordinate(0, 5));

            var activated = system.activation.TryActivate();

            Assert.IsTrue(activated);
            Assert.IsTrue(system.decay.IsSuspended);
        }

        [Test]
        public void TryActivate_Dash_AdvancesTrailInLastMovedDirection()
        {
            var system = CreateSystem(TotemAbilityType.Dash);
            FillCharge(system, new GridCoordinate(0, 5)); // движение вниз по Row, direction=(1,0)
            var beforeDash = system.trail.CurrentPosition;

            var activated = system.activation.TryActivate();

            Assert.IsTrue(activated);
            Assert.AreEqual(new GridCoordinate(beforeDash.Row + 3, beforeDash.Column), system.trail.CurrentPosition);
        }

        [Test]
        public void TryActivate_Dash_StopsEarlyAtBlockedTileButStillConsumesCharge()
        {
            var system = CreateSystem(TotemAbilityType.Dash);
            FillCharge(system, new GridCoordinate(0, 5));
            var beforeDash = system.trail.CurrentPosition;
            var oneAhead = new GridCoordinate(beforeDash.Row + 1, beforeDash.Column);
            system.grid.GetOrCreateTile(oneAhead).MarkBlocked();

            var activated = system.activation.TryActivate();

            Assert.IsTrue(activated);
            Assert.AreEqual(beforeDash, system.trail.CurrentPosition); // упёрлись сразу — не сдвинулись
            Assert.AreEqual(0f, system.charge.Charge); // но заряд всё равно потрачен
        }

        [Test]
        public void TryActivate_SecondWind_ReturnsTrueButAppliesNoEffect()
        {
            // Вне скоупа этого диспетчера (см. doc-комментарий класса) —
            // активация "успешна" (заряд тратится), но никакого эффекта
            // здесь не применяется.
            var system = CreateSystem(TotemAbilityType.SecondWind);
            FillCharge(system, new GridCoordinate(0, 5));
            var beforeActivation = system.trail.CurrentPosition;

            var activated = system.activation.TryActivate();

            Assert.IsTrue(activated);
            Assert.AreEqual(beforeActivation, system.trail.CurrentPosition);
            Assert.IsFalse(system.decay.IsSuspended);
        }

        [Test]
        public void TryActivate_ResetsChargeSoImmediateSecondActivationFails()
        {
            var system = CreateSystem(TotemAbilityType.Breach);
            FillCharge(system, new GridCoordinate(0, 5));

            system.activation.TryActivate();
            var secondActivation = system.activation.TryActivate();

            Assert.IsFalse(secondActivation);
        }
    }
}
