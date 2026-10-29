using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class DoubleTapDetectorTests
    {
        [Test]
        public void TwoTapsWithinWindow_AreDoubleTap_ThenSeriesResets()
        {
            var d = new DoubleTapDetector();

            Assert.IsFalse(d.Register(1.00f));
            Assert.IsTrue(d.Register(1.20f));
            Assert.IsFalse(d.Register(1.30f), "третий тап начинает новую серию");
            Assert.IsTrue(d.Register(1.50f));
        }

        [Test]
        public void SlowSecondTap_IsNotDoubleTap()
        {
            var d = new DoubleTapDetector();

            Assert.IsFalse(d.Register(1f));
            Assert.IsFalse(d.Register(1f + DoubleTapDetector.WindowSeconds + 0.1f));
        }

        [Test]
        public void Reset_ForgetsFirstTap()
        {
            var d = new DoubleTapDetector();
            d.Register(1f);
            d.Reset();

            Assert.IsFalse(d.Register(1.1f));
        }
    }
}
