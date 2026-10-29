using Burmalda.Core;
using Burmalda.Decay;
using Burmalda.FateRoll;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.RunLifecycle.Tests
{
    /// <summary>Амулеты Реликвий переживают попадание ловушки раз за Ярус, но не защищают от Лавы (PRD v10 §32, инвариант 5).</summary>
    public class RunEffectsRunStateTests
    {
        [TearDown]
        public void ResetEffects() => RunEffects.Current = RunEffects.Neutral;

        private static (TunnelGrid grid, GridTraceTrail trail, RunState runState) CreateRun()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var decay = new TrailDecaySystem(grid, trail);
            return (grid, trail, new RunState(grid, trail, decay, () => 6, FateRollRules.MaxRolls));
        }

        private static ConfigurableRunEffects AbsorbOnce(System.Collections.Generic.List<ThreatType> seen)
        {
            var used = false;
            return new ConfigurableRunEffects { Absorb = t => { seen.Add(t); if (used) return false; used = true; return true; } };
        }

        [Test]
        public void ArrowHit_IsAbsorbed_OnceThenKills_ReportsSlashing()
        {
            var seen = new System.Collections.Generic.List<ThreatType>();
            RunEffects.Current = AbsorbOnce(seen);
            var (grid, trail, run) = CreateRun();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkLethalTrap(LethalTrapType.ArrowWave);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            Assert.IsTrue(run.IsAlive, "первое попадание поглощено");

            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            Assert.IsFalse(run.IsAlive, "заряд израсходован — попадание убивает");
            CollectionAssert.AreEqual(new[] { ThreatType.Slashing, ThreatType.Slashing }, seen);
        }

        [Test]
        public void Lava_IsNeverAbsorbed_AndNeverEvenAsked()
        {
            var seen = new System.Collections.Generic.List<ThreatType>();
            RunEffects.Current = AbsorbOnce(seen);
            var (grid, trail, run) = CreateRun();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkLethalTrap(LethalTrapType.Lava);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsFalse(run.IsAlive, "от Лавы иммунитета нет");
            CollectionAssert.IsEmpty(seen);
        }

        [Test]
        public void FallingRockCrush_ReportsBlunt_AndCanBeAbsorbed()
        {
            var seen = new System.Collections.Generic.List<ThreatType>();
            RunEffects.Current = AbsorbOnce(seen);
            var (_, _, run) = CreateRun();

            run.ReportFallingRockCrush();

            Assert.IsTrue(run.IsAlive);
            CollectionAssert.AreEqual(new[] { ThreatType.Blunt }, seen);
        }

        [Test]
        public void ThreatHitHook_FireJet_IsCalledWithFireType_ForFutureTrap()
        {
            var seen = new System.Collections.Generic.List<ThreatType>();
            RunEffects.Current = AbsorbOnce(seen);
            var (_, _, run) = CreateRun();

            run.ReportThreatHit(ThreatType.Fire);
            Assert.IsTrue(run.IsAlive);
            run.ReportThreatHit(ThreatType.Fire);

            Assert.IsFalse(run.IsAlive);
            CollectionAssert.AreEqual(new[] { ThreatType.Fire, ThreatType.Fire }, seen);
        }

        [Test]
        public void TileDecayUnderFeet_IsNotAbsorbed()
        {
            var seen = new System.Collections.Generic.List<ThreatType>();
            RunEffects.Current = AbsorbOnce(seen);
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var decay = new TrailDecaySystem(grid, trail);
            var run = new RunState(grid, trail, decay, () => 6, FateRollRules.MaxRolls);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            decay.Tick(1000f);

            Assert.IsFalse(run.IsAlive, "распад пола — не попадание ловушки");
            CollectionAssert.IsEmpty(seen);
        }
    }
}
