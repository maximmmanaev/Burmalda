using NUnit.Framework;

namespace Burmalda.BossRoom.Tests
{
    public class RoomMultiplierTests
    {
        [Test]
        public void NewRoomMultiplier_StartsAtOne()
        {
            var multiplier = new RoomMultiplier();

            Assert.AreEqual(1f, multiplier.CurrentMultiplier);
        }

        [Test]
        public void ApplyResonanceTile_AddsFlatBonus()
        {
            var multiplier = new RoomMultiplier();

            multiplier.ApplyResonanceTile();

            Assert.AreEqual(1.5f, multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void ApplyResonanceTile_CalledTwice_Stacks()
        {
            var multiplier = new RoomMultiplier();

            multiplier.ApplyResonanceTile();
            multiplier.ApplyResonanceTile();

            Assert.AreEqual(2f, multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void TickRiftResonance_AddsProportionalToElapsedTime()
        {
            var multiplier = new RoomMultiplier();

            multiplier.TickRiftResonance(3f);

            Assert.AreEqual(1f + 0.1f * 3f, multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void TickRiftResonance_ZeroOrNegative_IsNoOp()
        {
            var multiplier = new RoomMultiplier();

            multiplier.TickRiftResonance(0f);
            multiplier.TickRiftResonance(-1f);

            Assert.AreEqual(1f, multiplier.CurrentMultiplier);
        }

        [Test]
        public void ApplyToIncome_MultipliesAndTruncates()
        {
            var multiplier = new RoomMultiplier();
            multiplier.ApplyResonanceTile(); // 1.5x

            Assert.AreEqual(15, multiplier.ApplyToIncome(10)); // 10*1.5=15 exactly
            Assert.AreEqual(7, multiplier.ApplyToIncome(5)); // 5*1.5=7.5 -> truncated to 7
        }

        [Test]
        public void ApplyToIncome_AtStartingMultiplier_ReturnsBaseAmountUnchanged()
        {
            var multiplier = new RoomMultiplier();

            Assert.AreEqual(42, multiplier.ApplyToIncome(42));
        }
    }
}
