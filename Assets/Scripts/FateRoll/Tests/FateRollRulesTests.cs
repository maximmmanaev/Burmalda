using System;
using NUnit.Framework;

namespace Burmalda.FateRoll.Tests
{
    public class FateRollRulesTests
    {
        // PRD v10 §16.1: I — смерть ≤7, на месте ≥12; II — ≤8 / ≥13; III — ≤9 / ≥14; иначе к Алтарю.
        [TestCase(1, 3, 4, FateOutcome.Death)]    // 7
        [TestCase(1, 3, 5, FateOutcome.ToAltar)]  // 8
        [TestCase(1, 5, 6, FateOutcome.ToAltar)]  // 11
        [TestCase(1, 6, 6, FateOutcome.InPlace)]  // 12
        [TestCase(2, 4, 4, FateOutcome.Death)]    // 8
        [TestCase(2, 4, 5, FateOutcome.ToAltar)]  // 9
        [TestCase(2, 6, 6, FateOutcome.InPlace)]  // 6+6 всегда на месте
        [TestCase(3, 4, 5, FateOutcome.Death)]    // 9
        [TestCase(3, 4, 6, FateOutcome.ToAltar)]  // 10
        [TestCase(3, 5, 6, FateOutcome.ToAltar)]  // 11
        [TestCase(3, 6, 6, FateOutcome.InPlace)]
        public void Evaluate_NoModifier_FollowsTable(int rollIndex, int d1, int d2, FateOutcome expected)
        {
            Assert.AreEqual(expected, FateRollRules.Evaluate(rollIndex, d1, d2));
        }

        [TestCase(1, 5, 5, 2, FateOutcome.InPlace)]   // 10 + 2 = 12
        [TestCase(2, 5, 5, 2, FateOutcome.ToAltar)]   // 12 < 13
        [TestCase(2, 5, 5, 3, FateOutcome.InPlace)]   // 13
        [TestCase(3, 5, 5, 3, FateOutcome.ToAltar)]   // 13 < 14
        [TestCase(1, 3, 4, 1, FateOutcome.ToAltar)]   // 7 + 1 = 8
        public void Evaluate_WithModifier_ShiftsSum(int rollIndex, int d1, int d2, int modifier, FateOutcome expected)
        {
            Assert.AreEqual(expected, FateRollRules.Evaluate(rollIndex, d1, d2, modifier));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Evaluate_SnakeEyes_IsAlwaysDeathEvenWithMaxModifier(int rollIndex)
        {
            Assert.AreEqual(FateOutcome.Death, FateRollRules.Evaluate(rollIndex, 1, 1, 3));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Evaluate_DoubleSix_IsAlwaysInPlaceEvenWithNegativeModifier(int rollIndex)
        {
            Assert.AreEqual(FateOutcome.InPlace, FateRollRules.Evaluate(rollIndex, 6, 6, -5));
        }

        [TestCase(0)]
        [TestCase(4)]
        public void Evaluate_RollIndexOutOfRange_Throws(int rollIndex)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FateRollRules.Evaluate(rollIndex, 3, 3));
        }

        [TestCase(0)]
        [TestCase(7)]
        public void Evaluate_DieOutOfRange_Throws(int die)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FateRollRules.Evaluate(1, die, 3));
        }

        // Baseline PRD v10 §16.1: I — 58/39/3 %, II — 72/25/3 %, III — 83/14/3 %.
        [TestCase(1, 58, 39, 3)]
        [TestCase(2, 72, 25, 3)]
        [TestCase(3, 83, 14, 3)]
        public void Chances_NoModifier_MatchPrdBaseline(int rollIndex, int death, int altar, int inPlace)
        {
            var c = FateRollRules.Chances(rollIndex);

            Assert.AreEqual(death, Math.Round(c.DeathPercent));
            Assert.AreEqual(altar, Math.Round(c.AltarPercent));
            Assert.AreEqual(inPlace, Math.Round(c.InPlacePercent));
        }

        [Test]
        public void Chances_WithModifier_ImprovesInPlaceChanceAndSumsToHundred()
        {
            var baseline = FateRollRules.Chances(1);
            var boosted = FateRollRules.Chances(1, 3);

            Assert.Greater(boosted.InPlacePercent, baseline.InPlacePercent);
            Assert.Less(boosted.DeathPercent, baseline.DeathPercent);
            Assert.AreEqual(100.0, boosted.DeathPercent + boosted.AltarPercent + boosted.InPlacePercent, 1e-9);
        }

        [Test]
        public void MaxRolls_IsThree()
        {
            Assert.AreEqual(3, FateRollRules.MaxRolls);
        }

        [Test]
        public void TryPay_WithStubEnabled_IsFree()
        {
            Assert.IsTrue(FateRollRules.FateRollPaymentStub);
            Assert.IsTrue(FateRollRules.TryPay(1));
        }
    }
}
