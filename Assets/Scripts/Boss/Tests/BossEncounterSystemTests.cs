using Burmalda.Artifacts;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.D20;
using Burmalda.Decay;
using Burmalda.Movement;
using Burmalda.Progression;
using Burmalda.RunLifecycle;
using NUnit.Framework;

namespace Burmalda.Boss.Tests
{
    public class BossEncounterSystemTests
    {
        private const int Width = 5;

        private sealed class Fixture
        {
            public TunnelGrid Grid;
            public GridTraceTrail Trail;
            public RunCurrencyAccumulator Mana;
            public RunCurrencyAccumulator Coins;
            public ArtifactPool Pool;
            public FirstBossVictoryTracker Tracker;
            public RunDepthTier DepthTier;
            public RunState RunState;
            public BossEncounterSystem System;
        }

        // requiredEnergyForTier по умолчанию — фиксированный порог независимо от Яруса, чтобы не усложнять тесты, где сама кривая не проверяется.
        private static Fixture CreateFixture(int requiredEnergy, int d20Roll = 5)
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var decay = new TrailDecaySystem(grid, trail);
            var d20 = new D20Trial(() => d20Roll);
            var runState = new RunState(grid, trail, decay, d20);
            var mana = new RunCurrencyAccumulator();
            var coins = new RunCurrencyAccumulator();
            var pool = new ArtifactPool();
            var tracker = new FirstBossVictoryTracker();
            var depthTier = new RunDepthTier();
            var system = new BossEncounterSystem(grid, trail, mana, coins, pool, tracker, depthTier, reason => runState.ReportBossDefeat(reason), _ => requiredEnergy);

            return new Fixture
            {
                Grid = grid, Trail = trail, Mana = mana, Coins = coins,
                Pool = pool, Tracker = tracker, DepthTier = depthTier, RunState = runState, System = system
            };
        }

        [Test]
        public void Advanced_ReachesBossWithEnoughMana_ResolvesVictory()
        {
            var f = CreateFixture(requiredEnergy: 5000);
            f.Grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkBoss();
            f.Mana.Add(6000);
            BossEncounterOutcome result = null;
            f.System.EncounterResolved += (outcome, _) => result = outcome;

            f.Trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsTrue(result.IsVictory);
            Assert.IsTrue(f.RunState.IsAlive);
        }

        [Test]
        public void Advanced_ReachesBossWithEnoughMana_GrantsRelic()
        {
            var f = CreateFixture(requiredEnergy: 5000);
            f.Grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkBoss();
            f.Mana.Add(6000);
            Relic relic = null;
            f.System.EncounterResolved += (_, r) => relic = r;

            f.Trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsNotNull(relic);
        }

        [Test]
        public void Advanced_Victory_ConvertsOverflowToCoins()
        {
            var f = CreateFixture(requiredEnergy: 5000);
            f.Grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkBoss();
            f.Mana.Add(6000); // overflow 1000

            f.Trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.AreEqual((int)(1000 * Boss.OverflowToCoinsRate), f.Coins.Total);
        }

        [Test]
        public void Advanced_FirstVictory_UnlocksAllAmuletsAndTalismansInPool()
        {
            var f = CreateFixture(requiredEnergy: 5000);
            f.Grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkBoss();
            f.Mana.Add(5000);

            f.Trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsTrue(f.Tracker.HasWonBefore);
            foreach (var amulet in ArtifactCatalog.Amulets) Assert.IsTrue(f.Pool.IsUnlocked(amulet.Id));
            foreach (var talisman in ArtifactCatalog.Talismans) Assert.IsTrue(f.Pool.IsUnlocked(talisman.Id));
        }

        [Test]
        public void Advanced_ReachesBossWithInsufficientMana_ResolvesDefeatAndKillsRunStateWithoutD20()
        {
            var f = CreateFixture(requiredEnergy: 5000, d20Roll: 20); // Fortune, если бы d20 всё же бросился
            f.Grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkBoss();
            f.Mana.Add(4000);
            var d20Fired = false;
            f.RunState.D20Resolved += _ => d20Fired = true;

            f.Trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsFalse(f.RunState.IsAlive);
            Assert.IsFalse(d20Fired, "поражение от Босса детерминировано (#82) — d20 бросаться не должен");
        }

        [Test]
        public void Advanced_Defeat_DoesNotGrantRelicOrUnlockPool()
        {
            var f = CreateFixture(requiredEnergy: 5000);
            f.Grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkBoss();
            f.Mana.Add(1000);
            Relic relic = new Relic("placeholder", "placeholder"); // не null изначально — проверяем, что перезапишется на null
            f.System.EncounterResolved += (_, r) => relic = r;

            f.Trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsNull(relic);
            Assert.IsFalse(f.Tracker.HasWonBefore);
        }

        [Test]
        public void Advanced_NonBossTile_DoesNothing()
        {
            var f = CreateFixture(requiredEnergy: 5000);
            f.Mana.Add(6000);
            var fired = false;
            f.System.EncounterResolved += (_, _) => fired = true;

            f.Trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsFalse(fired);
        }

        [Test]
        public void Advanced_Victory_AdvancesDepthTier()
        {
            var f = CreateFixture(requiredEnergy: 5000);
            f.Grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkBoss();
            f.Mana.Add(5000);

            f.Trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.AreEqual(1, f.DepthTier.CurrentTier);
        }

        [Test]
        public void Advanced_Defeat_DoesNotAdvanceDepthTier()
        {
            var f = CreateFixture(requiredEnergy: 5000);
            f.Grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkBoss();
            f.Mana.Add(1000);

            f.Trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.AreEqual(0, f.DepthTier.CurrentTier);
        }

        [Test]
        public void Advanced_UsesRequiredEnergyForCurrentTierBeforeThisVictory()
        {
            // Первый Босс встречается на Ярусе 0 (ещё не побеждён ни один) — requiredEnergyForTier(0).
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var decay = new TrailDecaySystem(grid, trail);
            var runState = new RunState(grid, trail, decay, new D20Trial(() => 5));
            var mana = new RunCurrencyAccumulator();
            var depthTier = new RunDepthTier();
            var seenTiers = new System.Collections.Generic.List<int>();
            var system = new BossEncounterSystem(grid, trail, mana, new RunCurrencyAccumulator(), new ArtifactPool(),
                new FirstBossVictoryTracker(), depthTier, reason => runState.ReportBossDefeat(reason),
                tier => { seenTiers.Add(tier); return 1000; });
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkBoss();
            mana.Add(1000);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            CollectionAssert.AreEqual(new[] { 0 }, seenTiers);
        }

        [Test]
        public void Dispose_StopsReactingToFurtherAdvances()
        {
            var f = CreateFixture(requiredEnergy: 5000);
            f.Grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkBoss();
            f.Mana.Add(6000);
            f.System.Dispose();
            var fired = false;
            f.System.EncounterResolved += (_, _) => fired = true;

            f.Trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsFalse(fired);
        }
    }
}
