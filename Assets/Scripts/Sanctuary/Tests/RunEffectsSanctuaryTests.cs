using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Sanctuary.Tests
{
    public class RunEffectsSanctuaryTests
    {
        [TearDown]
        public void ResetEffects() => RunEffects.Current = RunEffects.Neutral;

        [Test]
        public void ThunderTalisman_ResonanceTile_Adds0Point8InsteadOf0Point5()
        {
            var plain = new SanctuaryMultiplier();
            plain.ApplyResonanceTile();
            Assert.AreEqual(1.5f, plain.CurrentMultiplier, 1e-5f);

            RunEffects.Current = new ConfigurableRunEffects { ResonanceBonus = 0.8f };
            var thunder = new SanctuaryMultiplier();
            thunder.ApplyResonanceTile();
            Assert.AreEqual(1.8f, thunder.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void NightRune_CollapseHeld_FrontStopsThenResumes()
        {
            var sanctuary = new Sanctuary(0, 20, 1f);
            sanctuary.Tick(1f);
            var before = sanctuary.Collapse.RowPosition;

            var effects = new ConfigurableRunEffects { CollapseHeld = true };
            RunEffects.Current = effects;
            sanctuary.Tick(2f);
            Assert.AreEqual(before, sanctuary.Collapse.RowPosition, 1e-5f, "обрушение остановлено");

            effects.CollapseHeld = false;
            sanctuary.Tick(1f);
            Assert.Greater(sanctuary.Collapse.RowPosition, before);
        }
    }
}
