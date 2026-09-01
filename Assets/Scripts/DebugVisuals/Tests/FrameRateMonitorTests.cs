using NUnit.Framework;

namespace Burmalda.DebugVisuals.Tests
{
    public class FrameRateMonitorTests
    {
        [Test]
        public void BeforeAnyTick_SmoothedFps_IsZero()
        {
            var monitor = new FrameRateMonitor();

            Assert.AreEqual(0f, monitor.SmoothedFps);
            Assert.AreEqual(0f, monitor.SmoothedFrameTimeSeconds);
            Assert.AreEqual(0f, monitor.LastFrameTimeSeconds);
        }

        [Test]
        public void FirstTick_SetsSmoothedFrameTime_ToExactSample_NotAveragedWithNothing()
        {
            var monitor = new FrameRateMonitor();

            monitor.Tick(0.02f); // 50 FPS

            Assert.AreEqual(0.02f, monitor.SmoothedFrameTimeSeconds, 1e-6f);
            Assert.AreEqual(50f, monitor.SmoothedFps, 1e-3f);
        }

        [Test]
        public void FirstTick_SetsLastFrameTime_ToExactSample()
        {
            var monitor = new FrameRateMonitor();

            monitor.Tick(0.02f);

            Assert.AreEqual(0.02f, monitor.LastFrameTimeSeconds, 1e-6f);
        }

        [Test]
        public void SecondTick_MovesSmoothedValue_OnlyPartwayTowardNewSample()
        {
            // EMA: smoothed += (new - smoothed) * 0.1 — не мгновенный скачок
            // на новое значение (иначе это был бы обычный 1/deltaTime, не
            // сглаживание, задача прямо просит "не мгновенный шум").
            var monitor = new FrameRateMonitor();
            monitor.Tick(0.02f); // smoothed = 0.02

            monitor.Tick(0.01f); // 100 FPS — резкий скачок

            var expected = 0.02f + (0.01f - 0.02f) * 0.1f; // 0.019
            Assert.AreEqual(expected, monitor.SmoothedFrameTimeSeconds, 1e-6f);
            Assert.Less(monitor.SmoothedFrameTimeSeconds, 0.02f, "Должно было сдвинуться к новому сэмплу...");
            Assert.Greater(monitor.SmoothedFrameTimeSeconds, 0.01f, "...но не мгновенно до него.");
        }

        [Test]
        public void SecondTick_UpdatesLastFrameTime_ToNewRawSample_Unsmoothed()
        {
            var monitor = new FrameRateMonitor();
            monitor.Tick(0.02f);

            monitor.Tick(0.01f);

            Assert.AreEqual(0.01f, monitor.LastFrameTimeSeconds, 1e-6f, "LastFrameTimeSeconds — сырое значение, не сглаженное.");
        }

        [Test]
        public void ManyIdenticalTicks_ConvergesToThatFrameTime()
        {
            var monitor = new FrameRateMonitor();
            monitor.Tick(0.05f); // резкая просадка — 20 FPS

            for (var i = 0; i < 200; i++) monitor.Tick(1f / 60f); // затем устойчиво 60 FPS

            Assert.AreEqual(1f / 60f, monitor.SmoothedFrameTimeSeconds, 1e-4f, "После достаточного числа тиков сглаженное значение должно сойтись к устойчивому кадру.");
            Assert.AreEqual(60f, monitor.SmoothedFps, 0.1f);
        }

        [Test]
        public void ZeroDeltaSeconds_IsIgnored_DoesNotResetOrCorruptState()
        {
            var monitor = new FrameRateMonitor();
            monitor.Tick(0.02f);

            monitor.Tick(0f);

            Assert.AreEqual(0.02f, monitor.SmoothedFrameTimeSeconds, 1e-6f);
            Assert.AreEqual(0.02f, monitor.LastFrameTimeSeconds, 1e-6f, "Нулевой тик не должен был перезаписать последний валидный кадр.");
        }

        [Test]
        public void NegativeDeltaSeconds_IsIgnored()
        {
            var monitor = new FrameRateMonitor();
            monitor.Tick(0.02f);

            monitor.Tick(-1f);

            Assert.AreEqual(0.02f, monitor.SmoothedFrameTimeSeconds, 1e-6f);
        }

        [Test]
        public void ZeroDeltaSeconds_AsFirstTick_LeavesMonitorWithoutASample()
        {
            var monitor = new FrameRateMonitor();

            monitor.Tick(0f);

            Assert.AreEqual(0f, monitor.SmoothedFps);
            // Следующий валидный тик должен вести себя как ПЕРВЫЙ сэмпл (точное значение, не EMA с нулём).
            monitor.Tick(0.02f);
            Assert.AreEqual(0.02f, monitor.SmoothedFrameTimeSeconds, 1e-6f);
        }
    }
}
