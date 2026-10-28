using Burmalda.FateRoll;
using NUnit.Framework;

namespace Burmalda.DebugVisuals.Tests
{
    public class FateRollOutcomeTextTests
    {
        [Test]
        public void BuildMessage_InPlace_MentionsDiceAndContinuation()
        {
            var text = FateRollOutcomeText.BuildMessage(new FateRollResult(1, 6, 6, 0, FateOutcome.InPlace));

            StringAssert.Contains("6+6 = 12", text);
            StringAssert.Contains("на месте", text);
        }

        [Test]
        public void BuildMessage_ToAltar_MentionsAltar()
        {
            StringAssert.Contains("Алтар", FateRollOutcomeText.BuildMessage(new FateRollResult(2, 4, 5, 0, FateOutcome.ToAltar)));
        }

        [Test]
        public void BuildMessage_WithModifier_ShowsItInSum()
        {
            StringAssert.Contains("+2", FateRollOutcomeText.BuildMessage(new FateRollResult(1, 3, 3, 2, FateOutcome.Death)));
        }

        [Test]
        public void ResolveColor_EachOutcome_ReturnsDistinctColor()
        {
            var inPlace = FateRollOutcomeText.ResolveColor(FateOutcome.InPlace);
            var altar = FateRollOutcomeText.ResolveColor(FateOutcome.ToAltar);
            var death = FateRollOutcomeText.ResolveColor(FateOutcome.Death);

            Assert.AreNotEqual(inPlace, altar);
            Assert.AreNotEqual(altar, death);
            Assert.AreNotEqual(inPlace, death);
        }
    }
}
