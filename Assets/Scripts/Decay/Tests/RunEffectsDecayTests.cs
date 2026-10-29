using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Decay.Tests
{
    public class RunEffectsDecayTests
    {
        [TearDown]
        public void ResetEffects() => RunEffects.Current = RunEffects.Neutral;

        private static float Elapsed()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var decay = new TrailDecaySystem(grid, trail);
            var target = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(target);
            decay.Tick(1f);
            return grid.GetOrCreateTile(target).DecayElapsedSeconds;
        }

        [Test]
        public void BlackGold_DecayRunsFaster()
        {
            var normal = Elapsed();
            RunEffects.Current = new ConfigurableRunEffects { DecayRateMultiplier = 1.25f };

            Assert.AreEqual(normal * 1.25f, Elapsed(), 1e-4f);
        }
    }
}
