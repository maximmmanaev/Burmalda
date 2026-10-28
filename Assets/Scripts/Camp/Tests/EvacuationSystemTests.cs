using System.Collections.Generic;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Camp.Tests
{
    public class EvacuationSystemTests
    {
        private const int Width = 5;

        private sealed class Fixture
        {
            public TunnelGrid Grid;
            public GridTraceTrail Trail;
            public RunCurrencyAccumulator Mana;
            public PersistentWallet CoinWallet;
            public EvacuationSystem System;
            public RunSarcophagi Carried;
            public List<Sarcophagus> Delivered = new List<Sarcophagus>();
        }

        private static Fixture CreateFixture(bool standOnAltar = true, float multiplier = 1f)
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridCoordinate(0, 2);
            var t = new GridTraceTrail(grid, trail);
            var altar = new GridCoordinate(1, 2);
            if (standOnAltar) grid.GetOrCreateTile(altar).MarkAltar();
            t.TryAdvanceTo(altar);

            var mana = new RunCurrencyAccumulator();
            var wallet = new PersistentWallet();
            var carried = new RunSarcophagi();
            var f = new Fixture { Grid = grid, Trail = t, Mana = mana, CoinWallet = wallet, Carried = carried };
            f.System = new EvacuationSystem(grid, t, mana, wallet, carried, list => f.Delivered.AddRange(list), multiplier);
            return f;
        }

        [Test]
        public void Evacuate_OnAltar_ConvertsManaAtLinearRateAndFiresReturned()
        {
            // Курс по умолчанию: Мана 20:1 (линейно, нелинейность — позже).
            var f = CreateFixture();
            f.Mana.Add(400);
            ReturnConversionResult? result = null;
            f.System.Returned += r => result = r;

            var ok = f.System.Evacuate();

            Assert.IsTrue(ok);
            Assert.IsTrue(f.System.HasEvacuated);
            Assert.AreEqual(20, f.CoinWallet.Balance);
            Assert.AreEqual(20, result.Value.ManaCoins);
        }

        [Test]
        public void Evacuate_OnAltar_ClearsCarriedMana()
        {
            var f = CreateFixture();
            f.Mana.Add(400);

            f.System.Evacuate();

            Assert.AreEqual(0, f.Mana.Total);
        }

        [Test]
        public void Evacuate_NotOnAltar_DoesNothing()
        {
            var f = CreateFixture(standOnAltar: false);
            f.Mana.Add(400);

            var ok = f.System.Evacuate();

            Assert.IsFalse(ok);
            Assert.IsFalse(f.System.HasEvacuated);
            Assert.AreEqual(0, f.CoinWallet.Balance);
            Assert.AreEqual(400, f.Mana.Total);
        }

        [Test]
        public void Evacuate_SecondCall_IsNoOp()
        {
            var f = CreateFixture();
            f.Mana.Add(400);
            f.System.Evacuate();
            f.Mana.Add(400);

            var ok = f.System.Evacuate();

            Assert.IsFalse(ok);
            Assert.AreEqual(20, f.CoinWallet.Balance);
        }

        [Test]
        public void Evacuate_WithCoinsOnReturnMultiplier_ScalesCoins()
        {
            var f = CreateFixture(multiplier: 1.5f);
            f.Mana.Add(400); // 20 * 1.5

            f.System.Evacuate();

            Assert.AreEqual(30, f.CoinWallet.Balance);
        }

        [Test]
        public void Evacuate_WithCustomStaticRates_UsesCurrentRates()
        {
            var oldMana = EvacuationSystem.ManaToCoinsRate;
            try
            {
                EvacuationSystem.ManaToCoinsRate = 10f;
                var f = CreateFixture();
                f.Mana.Add(100); // 10

                f.System.Evacuate();

                Assert.AreEqual(10, f.CoinWallet.Balance);
            }
            finally
            {
                EvacuationSystem.ManaToCoinsRate = oldMana;
            }
        }

        [Test]
        public void LoseAll_ClearsManaAndPaysNoCoins()
        {
            var f = CreateFixture();
            f.Mana.Add(400);

            f.System.LoseAll();

            Assert.AreEqual(0, f.Mana.Total);
            Assert.AreEqual(0, f.CoinWallet.Balance);
            Assert.IsFalse(f.System.HasEvacuated);
        }

        [Test]
        public void Reaching_Altar_DoesNotCheckpointMana_DeathStillLosesEverything()
        {
            // PRD v10 §11: Алтарь Ману не фиксирует — ставка похода всё, что несёшь.
            var f = CreateFixture();
            f.Mana.Add(400);

            f.System.LoseAll();

            Assert.AreEqual(0, f.Mana.Total);
        }

        [Test]
        public void Evacuate_CarriedSarcophagi_AreDeliveredToCampAndLeaveTheRun()
        {
            var f = CreateFixture();
            f.Carried.Add(new Sarcophagus(SarcophagusRank.Junior));
            f.Carried.Add(new Sarcophagus(SarcophagusRank.Junior));

            f.System.Evacuate();

            Assert.AreEqual(2, f.Delivered.Count);
            Assert.AreEqual(0, f.Carried.Count);
        }

        [Test]
        public void Evacuate_NotOnAltar_KeepsSarcophagiCarried()
        {
            var f = CreateFixture(standOnAltar: false);
            f.Carried.Add(new Sarcophagus(SarcophagusRank.Junior));

            f.System.Evacuate();

            Assert.AreEqual(1, f.Carried.Count);
            Assert.AreEqual(0, f.Delivered.Count);
        }

        [Test]
        public void LoseAll_CarriedSarcophagi_AreLostNothingDelivered()
        {
            var f = CreateFixture();
            f.Carried.Add(new Sarcophagus(SarcophagusRank.Junior));

            f.System.LoseAll();

            Assert.AreEqual(0, f.Carried.Count);
            Assert.AreEqual(0, f.Delivered.Count);
        }
    }
}
