using System.Text;
using Burmalda.Artifacts;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Camp.Tests
{
    /// <summary>Нелинейная конвертация Маны в Монеты (PRD v10 §26.1) и экономическая симуляция.</summary>
    public class ManaConversionTests
    {
        private const float Rate = 20f;

        [Test]
        public void FirstBracket_IsFullRate()
        {
            Assert.AreEqual(20, ManaConversion.CoinsFor(400, Rate));
            Assert.AreEqual(50, ManaConversion.CoinsFor(1000, Rate));
        }

        [Test]
        public void Brackets_AreAppliedInOrder_100_60_30_15()
        {
            // 1000 → 50; следующие 2000 при 60% → +60; следующие 4000 при 30% → +60; остаток 1000 при 15% → +7.5
            Assert.AreEqual(110, ManaConversion.CoinsFor(3000, Rate));
            Assert.AreEqual(170, ManaConversion.CoinsFor(7000, Rate));
            Assert.AreEqual((int)System.Math.Round(177.5), ManaConversion.CoinsFor(8000, Rate));
        }

        [Test]
        public void BiggerRun_AlwaysGivesMoreCoins_ButNotProportionally()
        {
            var previous = -1;
            for (var mana = 0; mana <= 60000; mana += 250)
            {
                var coins = ManaConversion.CoinsFor(mana, Rate);
                Assert.GreaterOrEqual(coins, previous, $"Мана {mana}");
                previous = coins;
            }

            Assert.Greater(ManaConversion.CoinsFor(20000, Rate), ManaConversion.CoinsFor(10000, Rate));
            Assert.Less(ManaConversion.CoinsFor(20000, Rate), 2 * ManaConversion.CoinsFor(10000, Rate), "вдвое больше Маны — меньше чем вдвое Монет");
        }

        [Test]
        public void MarginalEfficiency_FallsAlongBrackets()
        {
            Assert.AreEqual(1f, ManaConversion.MarginalEfficiency(500));
            Assert.AreEqual(0.6f, ManaConversion.MarginalEfficiency(2000));
            Assert.AreEqual(0.3f, ManaConversion.MarginalEfficiency(5000));
            Assert.AreEqual(0.15f, ManaConversion.MarginalEfficiency(50000));
        }

        [Test]
        public void ZeroOrNegative_GivesNothing_RateAndMultiplierScale()
        {
            Assert.AreEqual(0, ManaConversion.CoinsFor(0, Rate));
            Assert.AreEqual(0, ManaConversion.CoinsFor(-5, Rate));
            Assert.AreEqual(100, ManaConversion.CoinsFor(1000, 10f));
            Assert.AreEqual(100, ManaConversion.CoinsFor(1000, Rate, 2f));
        }

        /// <summary>
        /// Допущения симуляции (ЧЕРНОВЫЕ): короткий забег — Ярус 1 (~95 плит, ~35 Маны на плиту с серией, 4 источника, Святилище
        /// ~400 Маны), 4 минуты; длинный — Ярусы 1–3 (три Святилища с большим множителем ≈ 6000 Маны сверху), 12 минут.
        /// Саркофаги: короткий — один Редкий; длинный — Редкий, Эпический, Редкий.
        /// </summary>
        [Test]
        public void Simulation_CoinsPerRun_AndRunsToBuyIdolUpgrades()
        {
            var table = new StringBuilder("забег | Мана | Монеты из Маны | Монеты Саркофагов | всего Монет | минут\n");
            var shortMana = 95 * 35 + 4 * 100 + 400;
            var longMana = 3 * (95 * 35 + 4 * 100) + 6000;
            var shortCoins = ManaConversion.CoinsFor(shortMana, Rate);
            var longCoins = ManaConversion.CoinsFor(longMana, Rate);
            var shortSarc = SarcophagusConfig.CoinsFor(RelicRarity.Rare);
            var longSarc = SarcophagusConfig.CoinsFor(RelicRarity.Rare) + SarcophagusConfig.CoinsFor(RelicRarity.Epic) + SarcophagusConfig.CoinsFor(RelicRarity.Rare);
            table.AppendLine($"короткий (Ярус 1) | {shortMana} | {shortCoins} | {shortSarc} | {shortCoins + shortSarc} | 4");
            table.AppendLine($"длинный (Ярус 3) | {longMana} | {longCoins} | {longSarc} | {longCoins + longSarc} | 12");

            var rare = CollectibleCatalog.Get("idol-rush");
            foreach (var (name, perRun, minutes) in new[] { ("короткие", shortCoins + shortSarc, 4), ("длинные", longCoins + longSarc, 12) })
                foreach (var level in new[] { 1, 2 })
                {
                    var price = CampUpgrades.PriceFor(rare, level);
                    var runs = Mathf.CeilToInt(price / (float)perRun);
                    table.AppendLine($"Идол L{level + 1} ({price}): {name} забеги — {runs} забегов, ≈ {runs * minutes / 60f:F1} ч");
                }

            Debug.Log("Симуляция экономики Монет:\n" + table);

            Assert.Greater(longCoins, shortCoins);
            Assert.Less(longCoins, shortCoins * longMana / (float)shortMana, "длинный забег даёт больше, но не пропорционально");
            var firstUpgradeHours = Mathf.CeilToInt(CampUpgrades.PriceFor(rare, 1) / (float)(shortCoins + shortSarc)) * 4 / 60f;
            Assert.That(firstUpgradeHours, Is.InRange(0.5f, 2f), "первое улучшение — в пределах 1–2 часов игры короткими забегами");
        }
    }
}
