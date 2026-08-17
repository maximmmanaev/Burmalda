using System;
using Burmalda.RunModifiers;
using NUnit.Framework;

namespace Burmalda.DebugVisuals.Tests
{
    public class OmenCardTextTests
    {
        [TestCase(OmenId.FragileVault)]
        [TestCase(OmenId.HuntingPath)]
        [TestCase(OmenId.StingyAltar)]
        [TestCase(OmenId.HungryBoss)]
        [TestCase(OmenId.BlindDescent)]
        [TestCase(OmenId.RichVein)]
        public void Resolve_EveryOmenId_ReturnsNonEmptyCard(OmenId omenId)
        {
            var card = OmenCardText.Resolve(omenId);

            Assert.IsNotEmpty(card.Name);
            Assert.IsNotEmpty(card.Complication);
            Assert.IsNotEmpty(card.Reward);
        }

        [Test]
        public void Resolve_FragileVault_MatchesPrdV7Section20()
        {
            var card = OmenCardText.Resolve(OmenId.FragileVault);

            Assert.AreEqual("Хрупкий Свод", card.Name);
            StringAssert.Contains("−25%", card.Complication);
            StringAssert.Contains("×1.5", card.Reward);
        }

        [Test]
        public void BuildMessage_ContainsNameComplicationAndReward()
        {
            var message = OmenCardText.BuildMessage(OmenId.HuntingPath);

            StringAssert.Contains("Ловчая Тропа", message);
            StringAssert.Contains("ловушек ×2", message);
            StringAssert.Contains("Ключи ×2", message);
        }

        [Test]
        public void Resolve_UnknownValue_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => OmenCardText.Resolve((OmenId)999));
        }
    }
}
