using NUnit.Framework;

namespace Burmalda.RunLifecycle.Tests
{
    public class SecondWindChargeTests
    {
        [Test]
        public void Constructor_Available_IsAvailableTrue()
        {
            var charge = new SecondWindCharge(isAvailable: true);

            Assert.IsTrue(charge.IsAvailable);
        }

        [Test]
        public void Constructor_NotAvailable_IsAvailableFalse()
        {
            var charge = new SecondWindCharge(isAvailable: false);

            Assert.IsFalse(charge.IsAvailable);
        }

        [Test]
        public void TryConsume_WhenAvailable_ConsumesAndReturnsTrue()
        {
            var charge = new SecondWindCharge(isAvailable: true);

            var consumed = charge.TryConsume();

            Assert.IsTrue(consumed);
            Assert.IsFalse(charge.IsAvailable);
        }

        [Test]
        public void TryConsume_WhenNotAvailable_ReturnsFalse()
        {
            var charge = new SecondWindCharge(isAvailable: false);

            Assert.IsFalse(charge.TryConsume());
        }

        [Test]
        public void TryConsume_SecondCallInSameRun_ReturnsFalse()
        {
            // "Одно бесплатное спасение за забег" — не дважды.
            var charge = new SecondWindCharge(isAvailable: true);
            charge.TryConsume();

            Assert.IsFalse(charge.TryConsume());
        }

        [Test]
        public void TryConsume_Successful_RaisesConsumed()
        {
            var charge = new SecondWindCharge(isAvailable: true);
            var fired = false;
            charge.Consumed += () => fired = true;

            charge.TryConsume();

            Assert.IsTrue(fired);
        }
    }
}
