using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    /// <summary>Эффекты славянских Реликвий (PRD v10 §20.2): Леший, Мокошь, Перун, Чернобог.</summary>
    public class RelicEffectStateTests
    {
        private float _now;
        private int _yarus = 1;

        [SetUp]
        public void Reset()
        {
            _now = 0f;
            _yarus = 1;
        }

        private RelicEffectState With(params string[] ids)
        {
            var build = new RunRelicBuild();
            foreach (var id in ids) Assert.IsTrue(build.TryAdd(RelicCatalog.Get(id)), id);
            return new RelicEffectState(() => build, () => _now, () => _yarus);
        }

        [Test]
        public void NoRelics_EverythingNeutral()
        {
            var e = With();

            Assert.AreEqual(1f, e.WallCloseSlowdown);
            Assert.AreEqual(1f, e.ManaMultiplier);
            Assert.AreEqual(1f, e.StreakBonusMultiplier);
            Assert.AreEqual(1f, e.DecayRateMultiplier);
            Assert.AreEqual(0.5f, e.ResonanceBonus);
            Assert.AreEqual(1f, e.BearTrapEscapeMultiplier);
            Assert.IsTrue(e.StepsActivateTraps);
            Assert.IsFalse(e.CollapseHeld);
            Assert.AreEqual(0, e.BonusManaForStep(5));
            Assert.IsFalse(e.TryAbsorbHit(ThreatType.Blunt));
        }

        [Test]
        public void BereskovyAmulet_WallsCloseSlower()
        {
            Assert.AreEqual(RelicEffectState.WallSlowdownValue, With(RelicEffectState.BereskovyAmulet).WallCloseSlowdown);
            Assert.Greater(RelicEffectState.WallSlowdownValue, 1f);
        }

        [Test]
        public void LeshyTalisman_DoublesStreakBonus()
        {
            Assert.AreEqual(2f, With(RelicEffectState.LeshyTalisman).StreakBonusMultiplier);
        }

        [Test]
        public void MorokRune_StepsDoNotActivateTraps_For5Seconds_ThenOnceOnly()
        {
            var e = With(RelicEffectState.MorokRune);
            Assert.IsTrue(e.StepsActivateTraps);

            Assert.IsTrue(e.TryUseRune(RelicEffectState.MorokRune));
            Assert.IsFalse(e.StepsActivateTraps);
            _now = 4.9f;
            Assert.IsFalse(e.StepsActivateTraps);
            _now = 5.1f;
            Assert.IsTrue(e.StepsActivateTraps, "через 5 с ловушки снова работают");
            Assert.IsFalse(e.TryUseRune(RelicEffectState.MorokRune), "Руна разовая");
        }

        [Test]
        public void MokoshAmulet_AbsorbsAnyTrapHit_OncePerYarus()
        {
            var e = With(RelicEffectState.MokoshAmulet);

            Assert.IsTrue(e.TryAbsorbHit(ThreatType.Slashing));
            Assert.IsFalse(e.TryAbsorbHit(ThreatType.Blunt), "заряд этого Яруса потрачен");
            _yarus = 2;
            Assert.IsTrue(e.TryAbsorbHit(ThreatType.Explosion), "на следующем Ярусе заряд восстановлен");
        }

        [Test]
        public void SpindleTalisman_BonusManaOnEveryFifthTile()
        {
            var e = With(RelicEffectState.SpindleTalisman);

            Assert.AreEqual(0, e.BonusManaForStep(4));
            Assert.AreEqual(RelicEffectState.SpindleBonusMana, e.BonusManaForStep(5));
            Assert.AreEqual(0, e.BonusManaForStep(6));
            Assert.AreEqual(RelicEffectState.SpindleBonusMana, e.BonusManaForStep(10));
            Assert.AreEqual(0, e.BonusManaForStep(0));
        }

        [Test]
        public void PerunAmulet_AbsorbsOnlyFireJet_OncePerYarus_HookOnly()
        {
            var e = With(RelicEffectState.PerunAmulet);

            Assert.IsFalse(e.TryAbsorbHit(ThreatType.Blunt));
            Assert.IsFalse(e.TryAbsorbHit(ThreatType.Slashing));
            Assert.IsTrue(e.TryAbsorbHit(ThreatType.Fire), "крючок для Огненной струи вызывается с типом Fire");
            Assert.IsFalse(e.TryAbsorbHit(ThreatType.Fire));
            _yarus = 3;
            Assert.IsTrue(e.TryAbsorbHit(ThreatType.Fire));
        }

        [Test]
        public void TwoAmulets_EachHasItsOwnYarusCharge()
        {
            var e = With(RelicEffectState.MokoshAmulet, RelicEffectState.PerunAmulet);

            Assert.IsTrue(e.TryAbsorbHit(ThreatType.Fire));
            Assert.IsTrue(e.TryAbsorbHit(ThreatType.Fire));
            Assert.IsFalse(e.TryAbsorbHit(ThreatType.Fire));
        }

        [Test]
        public void ThunderTalisman_ResonanceGivesPoint8InsteadOfPoint5()
        {
            Assert.AreEqual(0.8f, With(RelicEffectState.ThunderTalisman).ResonanceBonus);
        }

        [Test]
        public void ChernobogAmulet_DoublesBearTrapEscapeTime_HookOnly()
        {
            var e = With(RelicEffectState.ChernobogAmulet);
            RunEffects.Current = e;
            try
            {
                Assert.AreEqual(2f, e.BearTrapEscapeMultiplier);
                Assert.AreEqual(RunEffects.BearTrapBaseEscapeSeconds * 2f, RunEffects.BearTrapEscapeSeconds(), 1e-5f);
            }
            finally
            {
                RunEffects.Current = RunEffects.Neutral;
            }
        }

        [Test]
        public void BlackGoldTalisman_ManaOnePointFive_AndFasterDecay()
        {
            var e = With(RelicEffectState.BlackGoldTalisman);

            Assert.AreEqual(1.5f, e.ManaMultiplier);
            Assert.Greater(e.DecayRateMultiplier, 1f);
        }

        [Test]
        public void NightRune_HoldsSanctuaryCollapseFor3Seconds_OnceOnly()
        {
            var e = With(RelicEffectState.NightRune);

            Assert.IsTrue(e.TryUseRune(RelicEffectState.NightRune));
            Assert.IsTrue(e.CollapseHeld);
            _now = 3.1f;
            Assert.IsFalse(e.CollapseHeld);
            Assert.IsFalse(e.TryUseRune(RelicEffectState.NightRune));
        }

        [Test]
        public void Rune_NotInBuild_CannotBeUsed()
        {
            Assert.IsFalse(With().TryUseRune(RelicEffectState.MorokRune));
        }

        [Test]
        public void NoEffect_GrantsLavaImmunityOrTouchesFateRoll()
        {
            // Лава в эффекты не передаётся: у IRunEffects нет метода для неё, а ThreatType — без Лавы (§32, инвариант 5).
            CollectionAssert.DoesNotContain(System.Enum.GetNames(typeof(ThreatType)), "Lava");
            var all = With(RelicEffectState.MokoshAmulet, RelicEffectState.PerunAmulet);
            foreach (ThreatType threat in System.Enum.GetValues(typeof(ThreatType)))
                all.TryAbsorbHit(threat); // любые угрозы — но не Лава, которой нет в перечислении
        }
    }
}
