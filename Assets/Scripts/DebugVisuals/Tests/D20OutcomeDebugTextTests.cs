using Burmalda.D20;
using NUnit.Framework;

namespace Burmalda.DebugVisuals.Tests
{
    public class D20OutcomeDebugTextTests
    {
        [Test]
        public void BuildMessage_Fortune_MentionsContinuationWithoutLoss()
        {
            StringAssert.Contains("Fortune", D20OutcomeDebugText.BuildMessage(D20Outcome.Fortune));
        }

        [Test]
        public void BuildMessage_Knockback_MentionsAltarRollback()
        {
            StringAssert.Contains("Алтар", D20OutcomeDebugText.BuildMessage(D20Outcome.Knockback));
        }

        [Test]
        public void BuildMessage_Death_MentionsLoss()
        {
            StringAssert.Contains("Death", D20OutcomeDebugText.BuildMessage(D20Outcome.Death));
        }

        [Test]
        public void ResolveColor_EachOutcome_ReturnsDistinctColor()
        {
            var fortune = D20OutcomeDebugText.ResolveColor(D20Outcome.Fortune);
            var knockback = D20OutcomeDebugText.ResolveColor(D20Outcome.Knockback);
            var death = D20OutcomeDebugText.ResolveColor(D20Outcome.Death);

            Assert.AreNotEqual(fortune, knockback);
            Assert.AreNotEqual(knockback, death);
            Assert.AreNotEqual(fortune, death);
        }
    }
}
