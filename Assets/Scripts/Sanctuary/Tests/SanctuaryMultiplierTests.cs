using NUnit.Framework;

namespace Burmalda.Sanctuary.Tests
{
    public class RoomMultiplierTests
    {
        [Test]
        public void NewRoomMultiplier_StartsAtOne()
        {
            var multiplier = new SanctuaryMultiplier();

            Assert.AreEqual(1f, multiplier.CurrentMultiplier);
        }

        [Test]
        public void ApplyResonanceTile_AddsFlatBonus()
        {
            var multiplier = new SanctuaryMultiplier();

            multiplier.ApplyResonanceTile();

            Assert.AreEqual(1.5f, multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void ApplyResonanceTile_CalledTwice_Stacks()
        {
            var multiplier = new SanctuaryMultiplier();

            multiplier.ApplyResonanceTile();
            multiplier.ApplyResonanceTile();

            Assert.AreEqual(2f, multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void ApplyEchoTile_MultipliesCurrentMultiplier()
        {
            var multiplier = new SanctuaryMultiplier();

            multiplier.ApplyEchoTile();

            Assert.AreEqual(2f, multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void ApplyEchoTile_CalledTwice_Stacks()
        {
            var multiplier = new SanctuaryMultiplier();

            multiplier.ApplyEchoTile();
            multiplier.ApplyEchoTile();

            Assert.AreEqual(4f, multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void ApplyEchoTile_AfterResonance_MultipliesTheAccumulatedValue()
        {
            var multiplier = new SanctuaryMultiplier();
            multiplier.ApplyResonanceTile(); // 1.5

            multiplier.ApplyEchoTile();

            Assert.AreEqual(3f, multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void TickRiftResonance_AddsProportionalToElapsedTime()
        {
            var multiplier = new SanctuaryMultiplier();

            multiplier.TickRiftResonance(3f);

            Assert.AreEqual(1f + 0.1f * 3f, multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void TickRiftResonance_ZeroOrNegative_IsNoOp()
        {
            var multiplier = new SanctuaryMultiplier();

            multiplier.TickRiftResonance(0f);
            multiplier.TickRiftResonance(-1f);

            Assert.AreEqual(1f, multiplier.CurrentMultiplier);
        }

        [Test]
        public void ApplyToIncome_MultipliesAndTruncates()
        {
            var multiplier = new SanctuaryMultiplier();
            multiplier.ApplyResonanceTile(); // 1.5x

            Assert.AreEqual(15, multiplier.ApplyToIncome(10)); // 10*1.5=15 exactly
            Assert.AreEqual(7, multiplier.ApplyToIncome(5)); // 5*1.5=7.5 -> truncated to 7
        }

        [Test]
        public void ApplyToIncome_AtStartingMultiplier_ReturnsBaseAmountUnchanged()
        {
            var multiplier = new SanctuaryMultiplier();

            Assert.AreEqual(42, multiplier.ApplyToIncome(42));
        }
    }
}
