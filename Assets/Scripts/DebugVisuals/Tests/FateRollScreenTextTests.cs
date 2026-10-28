using Burmalda.FateRoll;
using NUnit.Framework;

namespace Burmalda.DebugVisuals.Tests
{
    public class FateRollScreenTextTests
    {
        [Test]
        public void Labels_FirstRollBaseline_ShowRoundedPercents()
        {
            var c = FateRollRules.Chances(1);

            Assert.AreEqual("Смерть 58%", FateRollScreenText.Death(c));
            Assert.AreEqual("К Алтарю 39%", FateRollScreenText.Altar(c));
            Assert.AreEqual("На месте 3%", FateRollScreenText.InPlace(c));
        }

        [TestCase(1, "Бросок Судьбы I")]
        [TestCase(2, "Бросок Судьбы II")]
        [TestCase(3, "Бросок Судьбы III")]
        public void Title_UsesRomanNumeral(int index, string expected)
        {
            Assert.AreEqual(expected, FateRollScreenText.Title(index));
        }

        [Test]
        public void Stake_WithoutSarcophagi_OmitsThem()
        {
            Assert.AreEqual("На кону: Мана 120, Ключи 3", FateRollScreenText.Stake(120, 3, 0));
        }

        [Test]
        public void Stake_WithSarcophagi_ListsThem()
        {
            StringAssert.Contains("Саркофагов 2", FateRollScreenText.Stake(120, 3, 2));
        }
    }
}
