using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Camp.Tests
{
    public class ReturnJourneySystemTests
    {
        private const int Width = 5;

        private sealed class Fixture
        {
            public TunnelGrid Grid;
            public GridTraceTrail Trail;
            public RunCurrencyAccumulator Coins;
            public RunCurrencyAccumulator Mana;
            public RunCurrencyAccumulator Keys;
            public PersistentWallet CoinWallet;
            public ReturnJourneySystem System;
        }

        private static Fixture CreateFixture()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            // Уводим трейл на глубину, прежде чем начинать возврат — иначе тест не отличит "уже на старте" от "вернулся".
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(new GridCoordinate(2, 2));

            var coins = new RunCurrencyAccumulator();
            var mana = new RunCurrencyAccumulator();
            var keys = new RunCurrencyAccumulator();
            var coinWallet = new PersistentWallet();
            var system = new ReturnJourneySystem(trail, coins, mana, keys, coinWallet);

            return new Fixture { Grid = grid, Trail = trail, Coins = coins, Mana = mana, Keys = keys, CoinWallet = coinWallet, System = system };
        }

        [Test]
        public void BeginReturn_SetsIsReturningTrue()
        {
            var f = CreateFixture();

            f.System.BeginReturn();

            Assert.IsTrue(f.System.IsReturning);
        }

        [Test]
        public void PositionChanged_ReachesRowZeroWhileReturning_CommitsCoinsAndFiresReturned()
        {
            var f = CreateFixture();
            f.Coins.Add(120);
            f.System.BeginReturn();
            int? committed = null;
            f.System.Returned += amount => committed = amount;

            f.Trail.TryAdvanceTo(new GridCoordinate(1, 2));
            f.Trail.TryAdvanceTo(new GridCoordinate(0, 2)); // row 0 — "лагерь"

            Assert.AreEqual(120, committed);
            Assert.AreEqual(120, f.CoinWallet.Balance);
            Assert.IsFalse(f.System.IsReturning);
        }

        [Test]
        public void PositionChanged_ReachesRowZeroWhileNotReturning_DoesNothing()
        {
            var f = CreateFixture();
            f.Coins.Add(120);
            var fired = false;
            f.System.Returned += _ => fired = true;

            f.Trail.TryAdvanceTo(new GridCoordinate(1, 2));
            f.Trail.TryAdvanceTo(new GridCoordinate(0, 2));

            Assert.IsFalse(fired);
            Assert.AreEqual(0, f.CoinWallet.Balance);
        }

        [Test]
        public void PositionChanged_StillAboveRowZeroWhileReturning_DoesNotCommit()
        {
            var f = CreateFixture();
            f.Coins.Add(120);
            f.System.BeginReturn();
            var fired = false;
            f.System.Returned += _ => fired = true;

            f.Trail.TryAdvanceTo(new GridCoordinate(1, 2)); // ещё не row 0

            Assert.IsFalse(fired);
        }

        [Test]
        public void HandleDeathDuringReturn_WhileReturning_RevertsAllAccumulatorsToCheckpoint()
        {
            var f = CreateFixture();
            f.Coins.Add(100);
            f.Mana.Add(200);
            f.Keys.Add(3);
            f.Coins.Checkpoint();
            f.Mana.Checkpoint();
            f.Keys.Checkpoint();
            f.Coins.Add(50);
            f.Mana.Add(70);
            f.Keys.Add(2);
            f.System.BeginReturn();

            f.System.HandleDeathDuringReturn();

            Assert.AreEqual(100, f.Coins.Total);
            Assert.AreEqual(200, f.Mana.Total);
            Assert.AreEqual(3, f.Keys.Total);
        }

        [Test]
        public void HandleDeathDuringReturn_WhileReturning_ClearsIsReturning()
        {
            var f = CreateFixture();
            f.System.BeginReturn();

            f.System.HandleDeathDuringReturn();

            Assert.IsFalse(f.System.IsReturning);
        }

        [Test]
        public void HandleDeathDuringReturn_WhileNotReturning_DoesNotRevertAnything()
        {
            // Обычная смерть в прямом пути (не в возврате) — прогресс не откатывается этим классом.
            var f = CreateFixture();
            f.Coins.Add(100);
            f.Coins.Checkpoint();
            f.Coins.Add(50);

            f.System.HandleDeathDuringReturn();

            Assert.AreEqual(150, f.Coins.Total);
        }

        [Test]
        public void Dispose_StopsReactingToFurtherPositionChanges()
        {
            var f = CreateFixture();
            f.Coins.Add(120);
            f.System.BeginReturn();
            f.System.Dispose();
            var fired = false;
            f.System.Returned += _ => fired = true;

            f.Trail.TryAdvanceTo(new GridCoordinate(1, 2));
            f.Trail.TryAdvanceTo(new GridCoordinate(0, 2));

            Assert.IsFalse(fired);
        }
    }
}
