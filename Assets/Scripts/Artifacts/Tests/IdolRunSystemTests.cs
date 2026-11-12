using System.Collections.Generic;
using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    /// <summary>Применение Идола (PRD v10 §18.1): двойной тап, заряд, режим выбора цели.</summary>
    public class IdolRunSystemTests
    {
        private sealed class FakeAbility : IIdolAbility
        {
            public FakeAbility(string id, IdolTargeting targeting) { IdolId = id; Targeting = targeting; }
            public string IdolId { get; }
            public IdolTargeting Targeting { get; }
            public bool Succeed = true;
            public List<(int level, GridCoordinate? aim)> Calls = new List<(int, GridCoordinate?)>();
            public bool Execute(IdolAbilityContext context, int level, GridCoordinate? aim)
            {
                Calls.Add((level, aim));
                return Succeed;
            }
        }

        private static (IdolRunSystem run, CollectionProgress progress, FakeAbility ability, int[] charges) Make(string idolId, IdolTargeting targeting, bool charged, bool equip = true)
        {
            var progress = new CollectionProgress();
            var item = CollectibleCatalog.Get(idolId);
            progress.SetParts(idolId, item.PartsRequired);
            if (equip) progress.TrySelect(idolId);
            var grid = new TunnelGrid(5);
            var ctx = new IdolAbilityContext(grid, new GridTraceTrail(grid, new GridCoordinate(0, 2)), new IdolEffects(() => 0f), () => 0f);
            var ability = new FakeAbility(idolId, targeting);
            var consumed = new[] { 0 };
            var run = new IdolRunSystem(progress, () => charged, () => { consumed[0]++; return true; }, ctx, new[] { ability });
            return (run, progress, ability, consumed);
        }

        [Test]
        public void DoubleTap_WhenNotCharged_DoesNothing()
        {
            var (run, _, ability, consumed) = Make("idol-shield", IdolTargeting.Immediate, charged: false);

            Assert.IsFalse(run.OnDoubleTap());
            Assert.AreEqual(0, ability.Calls.Count);
            Assert.AreEqual(0, consumed[0]);
            Assert.IsFalse(run.IsTargeting);
        }

        [Test]
        public void DoubleTap_NoIdolEquipped_DoesNothing()
        {
            var (run, _, ability, _) = Make("idol-shield", IdolTargeting.Immediate, charged: true, equip: false);

            Assert.IsFalse(run.OnDoubleTap());
            Assert.AreEqual(0, ability.Calls.Count);
        }

        [Test]
        public void ImmediateIdol_FiresAtOnce_AndSpendsCharge()
        {
            var (run, _, ability, consumed) = Make("idol-time", IdolTargeting.Immediate, charged: true);

            Assert.IsTrue(run.OnDoubleTap());

            Assert.AreEqual(1, ability.Calls.Count);
            Assert.IsNull(ability.Calls[0].aim);
            Assert.AreEqual(1, consumed[0]);
            Assert.IsFalse(run.IsTargeting);
        }

        [Test]
        public void ImmediateIdol_AbilityFailed_ChargeKept()
        {
            var (run, _, ability, consumed) = Make("idol-shield", IdolTargeting.Immediate, charged: true);
            ability.Succeed = false;

            Assert.IsFalse(run.OnDoubleTap());
            Assert.AreEqual(0, consumed[0]);
        }

        [Test]
        public void AimedIdol_DoubleTapEntersTargeting_ReleasePerformsAbility_ThenSpendsCharge()
        {
            var (run, _, ability, consumed) = Make("idol-rush", IdolTargeting.Aim, charged: true);

            Assert.IsTrue(run.OnDoubleTap());
            Assert.IsTrue(run.IsTargeting);
            Assert.AreEqual("idol-rush", run.TargetingIdolId);
            Assert.AreEqual(0, ability.Calls.Count, "сама по себе активация цели не выполняет способность");

            Assert.IsTrue(run.OnTargetReleased(new GridCoordinate(2, 2)));

            Assert.AreEqual(new GridCoordinate(2, 2), ability.Calls[0].aim);
            Assert.AreEqual(1, consumed[0]);
            Assert.IsFalse(run.IsTargeting);
        }

        [Test]
        public void AimedIdol_InvalidTarget_StaysInTargeting_ChargeKept()
        {
            var (run, _, ability, consumed) = Make("idol-rush", IdolTargeting.Aim, charged: true);
            run.OnDoubleTap();
            ability.Succeed = false;

            Assert.IsFalse(run.OnTargetReleased(new GridCoordinate(1, 2)));

            Assert.IsTrue(run.IsTargeting);
            Assert.AreEqual(0, consumed[0]);
        }

        [Test]
        public void DoubleTapWhileTargeting_Cancels_WithoutSpendingCharge()
        {
            var (run, _, _, consumed) = Make("idol-passage", IdolTargeting.Aim, charged: true);
            run.OnDoubleTap();

            Assert.IsTrue(run.OnDoubleTap());

            Assert.IsFalse(run.IsTargeting);
            Assert.AreEqual(0, consumed[0]);
        }

        [Test]
        public void ReleaseWithoutTargeting_IsIgnored()
        {
            var (run, _, ability, _) = Make("idol-rush", IdolTargeting.Aim, charged: true);

            Assert.IsFalse(run.OnTargetReleased(new GridCoordinate(1, 2)));
            Assert.AreEqual(0, ability.Calls.Count);
        }

        [Test]
        public void AbilityReceivesItemLevel_AtLeastOne()
        {
            var (run, _, ability, _) = Make("idol-time", IdolTargeting.Immediate, charged: true);

            run.OnDoubleTap();

            Assert.GreaterOrEqual(ability.Calls[0].level, 1);
        }
    }

    public class CollectionProgressLoadoutTests
    {
        [Test]
        public void Select_OnlyAssembledItems_OneIdolAndOneTotem()
        {
            var p = new CollectionProgress();
            p.SetParts("idol-rush", 3);
            Assert.IsFalse(p.TrySelect("idol-rush"), "не собран");
            Assert.IsFalse(p.TrySelect("no-such"));

            p.SetParts("idol-rush", 4);
            p.SetParts("totem-amik", 4);
            Assert.IsTrue(p.TrySelect("idol-rush"));
            Assert.IsTrue(p.TrySelect("totem-amik"));
            Assert.AreEqual("idol-rush", p.ActiveIdolId);
            Assert.AreEqual("totem-amik", p.ActiveTotemId);
        }

        [Test]
        public void NakedStart_SlotsEmpty_UntilSomethingAssembled_ThenAutoEquipped()
        {
            var p = new CollectionProgress();
            Assert.IsNull(p.ActiveIdolId);
            Assert.IsNull(p.ActiveTotemId);

            for (var i = 0; i < 4; i++) p.AddPart("idol-shield");

            Assert.AreEqual("idol-shield", p.ActiveIdolId, "собранный Идол встаёт в пустой слот");
            Assert.IsNull(p.ActiveTotemId);
        }

        [Test]
        public void AutoEquip_DoesNotReplaceChosenItem()
        {
            var p = new CollectionProgress();
            p.SetParts("idol-rush", 4);
            p.TrySelect("idol-rush");
            for (var i = 0; i < 5; i++) p.AddPart("idol-time");

            Assert.AreEqual("idol-rush", p.ActiveIdolId);
        }

        [Test]
        public void RestoreActive_IgnoresUnassembled()
        {
            var p = new CollectionProgress();
            p.SetParts("idol-shield", 4);
            p.RestoreActive("idol-shield", "totem-migizi");

            Assert.AreEqual("idol-shield", p.ActiveIdolId);
            Assert.IsNull(p.ActiveTotemId);
        }
    }
}
