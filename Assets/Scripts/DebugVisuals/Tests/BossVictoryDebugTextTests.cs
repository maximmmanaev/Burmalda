using Burmalda.Artifacts;
using Burmalda.Boss;
using NUnit.Framework;

namespace Burmalda.DebugVisuals.Tests
{
    public class BossVictoryDebugTextTests
    {
        [Test]
        public void BuildMessage_Victory_MentionsTierAndRelicName_NotEnergyThreshold()
        {
            var outcome = BossEncounterOutcome.Victory(coinsFromOverflow: 0, hasBoostedRareRelicChance: false, accumulatedMana: 12345, requiredEnergy: 6789);
            var relic = new Relic("relic-1", "Идол Терпения");

            var message = BossVictoryDebugText.BuildMessage(outcome, relic, tier: 2);

            StringAssert.Contains("Ярус 2", message);
            StringAssert.Contains("Идол Терпения", message);
            // Требование задачи: текст не должен зависеть от формулировок порога энергии — не в тексте.
            StringAssert.DoesNotContain("12345", message);
            StringAssert.DoesNotContain("6789", message);
        }

        [Test]
        public void BuildMessage_Defeat_MentionsTierOnly()
        {
            var outcome = BossEncounterOutcome.Defeat(accumulatedMana: 100, requiredEnergy: 500);

            var message = BossVictoryDebugText.BuildMessage(outcome, relic: null, tier: 1);

            StringAssert.Contains("поражение", message);
            StringAssert.Contains("Ярус 1", message);
        }

        [Test]
        public void BuildMessage_VictoryWithoutRelic_DoesNotThrow()
        {
            var outcome = BossEncounterOutcome.Victory(0, false, 100, 100);
            Assert.DoesNotThrow(() => BossVictoryDebugText.BuildMessage(outcome, relic: null, tier: 0));
        }

        [Test]
        public void BuildMessage_NullOutcome_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, BossVictoryDebugText.BuildMessage(null, null, 0));
        }
    }
}
