using NUnit.Framework;

namespace Burmalda.RunModifiers.Tests
{
    public class RunModifiersTests
    {
        [Test]
        public void None_HasNoActiveOmenAndAllNeutralValues()
        {
            var modifiers = RunModifiers.None;

            Assert.IsNull(modifiers.ActiveOmen);
            Assert.AreEqual(1f, modifiers.DecayThresholdMultiplier);
            Assert.AreEqual(1f, modifiers.ManaCrystalRewardMultiplier);
            Assert.AreEqual(1f, modifiers.TrapDensityMultiplier);
            Assert.AreEqual(1f, modifiers.KeyRewardMultiplier);
            Assert.AreEqual(2, modifiers.AltarsBeforeBossCount);
            Assert.IsFalse(modifiers.RelicGuaranteesIdol);
            Assert.AreEqual(1f, modifiers.BossRequiredEnergyMultiplier);
            Assert.AreEqual(1f, modifiers.CoinsOnReturnMultiplier);
            Assert.IsFalse(modifiers.TimedTrapsHiddenUntilTriggered);
            Assert.AreEqual(0, modifiers.BonusFreeRerollsPerAltar);
            Assert.AreEqual(1f, modifiers.ManaSourceTileDensityMultiplier);
        }

        [Test]
        public void FragileVault_HalvesDecayThresholdAndBoostsManaCrystalReward()
        {
            var modifiers = new RunModifiers(OmenId.FragileVault);

            Assert.AreEqual(0.75f, modifiers.DecayThresholdMultiplier, 1e-5f);
            Assert.AreEqual(1.5f, modifiers.ManaCrystalRewardMultiplier, 1e-5f);
            // Не относящиеся к этому Знамению эффекты — нейтральны.
            Assert.AreEqual(1f, modifiers.KeyRewardMultiplier);
            Assert.AreEqual(1f, modifiers.BossRequiredEnergyMultiplier);
        }

        [Test]
        public void HuntingPath_DoublesTrapDensityAndKeyReward()
        {
            var modifiers = new RunModifiers(OmenId.HuntingPath);

            Assert.AreEqual(2f, modifiers.TrapDensityMultiplier);
            Assert.AreEqual(2f, modifiers.KeyRewardMultiplier);
            Assert.AreEqual(1f, modifiers.DecayThresholdMultiplier);
        }

        [Test]
        public void StingyAltar_ReducesAltarsAndGuaranteesIdol()
        {
            var modifiers = new RunModifiers(OmenId.StingyAltar);

            Assert.AreEqual(1, modifiers.AltarsBeforeBossCount);
            Assert.IsTrue(modifiers.RelicGuaranteesIdol);
        }

        [Test]
        public void HungryBoss_IncreasesBossEnergyAndDoublesReturnCoins()
        {
            var modifiers = new RunModifiers(OmenId.HungryBoss);

            Assert.AreEqual(1.3f, modifiers.BossRequiredEnergyMultiplier, 1e-5f);
            Assert.AreEqual(2f, modifiers.CoinsOnReturnMultiplier);
        }

        [Test]
        public void BlindDescent_HidesTimedTrapsAndGrantsFreeReroll()
        {
            var modifiers = new RunModifiers(OmenId.BlindDescent);

            Assert.IsTrue(modifiers.TimedTrapsHiddenUntilTriggered);
            Assert.AreEqual(1, modifiers.BonusFreeRerollsPerAltar);
        }

        [Test]
        public void RichVein_IncreasesBossEnergyAndDoublesManaSourceDensity()
        {
            var modifiers = new RunModifiers(OmenId.RichVein);

            Assert.AreEqual(1.5f, modifiers.BossRequiredEnergyMultiplier, 1e-5f);
            Assert.AreEqual(2f, modifiers.ManaSourceTileDensityMultiplier);
        }
    }
}
