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

            var mana = new RunCurrencyAccumulator();
            var keys = new RunCurrencyAccumulator();
            var coinWallet = new PersistentWallet();
            var system = new ReturnJourneySystem(trail, mana, keys, coinWallet);

            return new Fixture { Grid = grid, Trail = trail, Mana = mana, Keys = keys, CoinWallet = coinWallet, System = system };
        }

        [Test]
        public void BeginReturn_SetsIsReturningTrue()
        {
            var f = CreateFixture();

            f.System.BeginReturn();

            Assert.IsTrue(f.System.IsReturning);
        }

        [Test]
        public void PositionChanged_ReachesRowZeroWhileReturning_ConvertsManaAndKeysAndFiresReturned()
        {
            // Курсы по умолчанию: Мана 20:1, Ключи 1:1 (PRD v9 §5).
            var f = CreateFixture();
            f.Mana.Add(400); // -> 20 Монет
            f.Keys.Add(20);  // -> 20 Монет
            f.System.BeginReturn();
            ReturnConversionResult? result = null;
            f.System.Returned += r => result = r;

            f.Trail.TryAdvanceTo(new GridCoordinate(1, 2));
            f.Trail.TryAdvanceTo(new GridCoordinate(0, 2)); // row 0 — "лагерь"

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(20, result.Value.ManaCoins);
            Assert.AreEqual(20, result.Value.KeysCoins);
            Assert.AreEqual(40, result.Value.TotalCoins);
            Assert.AreEqual(40, f.CoinWallet.Balance);
            Assert.IsFalse(f.System.IsReturning);
        }

        [Test]
        public void PositionChanged_WithCoinsOnReturnMultiplier_ScalesCommittedCoins()
        {
            // PRD v7 §20, Знамение «Голодный Босс»: "Монеты ×2 при возврате в Лагерь".
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(new GridCoordinate(2, 2));
            var mana = new RunCurrencyAccumulator();
            mana.Add(400); // -> 20 Монет до множителя
            var coinWallet = new PersistentWallet();
            var system = new ReturnJourneySystem(trail, mana, new RunCurrencyAccumulator(), coinWallet, coinsOnReturnMultiplier: 2f);
            system.BeginReturn();
            ReturnConversionResult? result = null;
            system.Returned += r => result = r;

            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(new GridCoordinate(0, 2)); // row 0 — "лагерь"

            Assert.AreEqual(40, result.Value.TotalCoins);
            Assert.AreEqual(40, coinWallet.Balance);
        }

        [Test]
        public void PositionChanged_WithCustomStaticRates_UsesCurrentRatesInsteadOfDefaults()
        {
            // Курсы — изменяемые статические поля (debug-панель, живой
            // рычаг, PRD v9 §5) — читаются напрямую в момент возврата, не
            // захватываются конструктором. Сохраняем/восстанавливаем, чтобы
            // не протекать в другие тесты.
            var savedManaRate = ReturnJourneySystem.ManaToCoinsRate;
            var savedKeysRate = ReturnJourneySystem.KeysToCoinsRate;
            try
            {
                ReturnJourneySystem.ManaToCoinsRate = 10f;
                ReturnJourneySystem.KeysToCoinsRate = 5f;

                var grid = new TunnelGrid(Width);
                var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
                trail.TryAdvanceTo(new GridCoordinate(1, 2));
                trail.TryAdvanceTo(new GridCoordinate(2, 2));
                var mana = new RunCurrencyAccumulator();
                var keys = new RunCurrencyAccumulator();
                mana.Add(100);
                keys.Add(10);
                var coinWallet = new PersistentWallet();
                var system = new ReturnJourneySystem(trail, mana, keys, coinWallet);
                system.BeginReturn();
                ReturnConversionResult? result = null;
                system.Returned += r => result = r;

                trail.TryAdvanceTo(new GridCoordinate(1, 2));
                trail.TryAdvanceTo(new GridCoordinate(0, 2));

                Assert.AreEqual(10, result.Value.ManaCoins); // 100 / 10
                Assert.AreEqual(2, result.Value.KeysCoins);  // 10 / 5
            }
            finally
            {
                ReturnJourneySystem.ManaToCoinsRate = savedManaRate;
                ReturnJourneySystem.KeysToCoinsRate = savedKeysRate;
            }
        }

        [Test]
        public void PositionChanged_ReachesRowZeroWhileNotReturning_DoesNothing()
        {
            var f = CreateFixture();
            f.Mana.Add(400);
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
            f.Mana.Add(400);
            f.System.BeginReturn();
            var fired = false;
            f.System.Returned += _ => fired = true;

            f.Trail.TryAdvanceTo(new GridCoordinate(1, 2)); // ещё не row 0

            Assert.IsFalse(fired);
        }

        [Test]
        public void HandleDeathDuringReturn_WhileReturning_RevertsManaAndKeysToCheckpoint()
        {
            var f = CreateFixture();
            f.Mana.Add(200);
            f.Keys.Add(3);
            f.Mana.Checkpoint();
            f.Keys.Checkpoint();
            f.Mana.Add(70);
            f.Keys.Add(2);
            f.System.BeginReturn();

            f.System.HandleDeathDuringReturn();

            Assert.AreEqual(200, f.Mana.Total);
            Assert.AreEqual(3, f.Keys.Total);
            Assert.AreEqual(0, f.CoinWallet.Balance, "смерть в процессе возврата не конвертирует ничего в Монеты");
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
            f.Mana.Add(100);
            f.Mana.Checkpoint();
            f.Mana.Add(50);

            f.System.HandleDeathDuringReturn();

            Assert.AreEqual(150, f.Mana.Total);
        }

        [Test]
        public void Dispose_StopsReactingToFurtherPositionChanges()
        {
            var f = CreateFixture();
            f.Mana.Add(400);
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
