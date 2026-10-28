using System;

namespace Burmalda.FateRoll
{
    /// <summary>Исход Броска Судьбы (PRD v10 §16.2).</summary>
    public enum FateOutcome
    {
        /// <summary>Окончательная смерть (§15.3).</summary>
        Death,

        /// <summary>На последний пройденный Алтарь; разрушенный маршрут не восстанавливается.</summary>
        ToAltar,

        /// <summary>На месте смерти — продолжает с той же плиты.</summary>
        InPlace
    }

    /// <summary>Шансы трёх секторов в процентах (для экрана броска, PRD v10 §16.1).</summary>
    public readonly struct FateChances
    {
        public FateChances(double death, double altar, double inPlace)
        {
            DeathPercent = death;
            AltarPercent = altar;
            InPlacePercent = inPlace;
        }

        public double DeathPercent { get; }
        public double AltarPercent { get; }
        public double InPlacePercent { get; }
    }

    /// <summary>
    /// Бросок Судьбы 2D6 (PRD v10 §16): сумма = 2D6 + модификатор (пока всегда 0 —
    /// точка расширения под Тотем Мигизи). Таблица:
    /// I — смерть ≤ 7, на месте ≥ 12; II — ≤ 8 / ≥ 13; III — ≤ 9 / ≥ 14;
    /// иначе к последнему Алтарю. 6+6 — всегда «на месте» (натуральная
    /// двадцатка), 1+1 — всегда смерть. Максимум три броска за экспедицию.
    /// Чистая логика без UnityEngine: кубики подаёт вызывающая сторона.
    /// </summary>
    public static class FateRollRules
    {
        public const int MaxRolls = 3;

        /// <summary>
        /// Заглушка оплаты (задача v10-1): броски бесплатны. Реклама и Монеты — #35;
        /// пока флаг выключен, платить нечем и бросок невозможен.
        /// </summary>
        public static bool FateRollPaymentStub = true;

        /// <summary>Списывает плату за бросок <paramref name="rollIndex"/>; сейчас — заглушка.</summary>
        public static bool TryPay(int rollIndex)
        {
            ValidateRollIndex(rollIndex);
            return FateRollPaymentStub;
        }

        public static FateOutcome Evaluate(int rollIndex, int die1, int die2, int modifier = 0)
        {
            ValidateRollIndex(rollIndex);
            ValidateDie(die1);
            ValidateDie(die2);

            if (die1 == 6 && die2 == 6) return FateOutcome.InPlace;
            if (die1 == 1 && die2 == 1) return FateOutcome.Death;

            var sum = die1 + die2 + modifier;
            if (sum <= DeathMax(rollIndex)) return FateOutcome.Death;
            if (sum >= InPlaceMin(rollIndex)) return FateOutcome.InPlace;
            return FateOutcome.ToAltar;
        }

        /// <summary>Шансы секторов с учётом модификатора — считаются перебором 36 исходов, не дублируют таблицу.</summary>
        public static FateChances Chances(int rollIndex, int modifier = 0)
        {
            ValidateRollIndex(rollIndex);
            int death = 0, altar = 0, inPlace = 0;
            for (var a = 1; a <= 6; a++)
            for (var b = 1; b <= 6; b++)
            {
                switch (Evaluate(rollIndex, a, b, modifier))
                {
                    case FateOutcome.Death: death++; break;
                    case FateOutcome.ToAltar: altar++; break;
                    default: inPlace++; break;
                }
            }

            return new FateChances(death * 100.0 / 36, altar * 100.0 / 36, inPlace * 100.0 / 36);
        }

        private static int DeathMax(int rollIndex) => 6 + rollIndex;

        private static int InPlaceMin(int rollIndex) => 11 + rollIndex;

        private static void ValidateRollIndex(int rollIndex)
        {
            if (rollIndex < 1 || rollIndex > MaxRolls)
                throw new ArgumentOutOfRangeException(nameof(rollIndex), rollIndex, "Номер броска — 1..3.");
        }

        private static void ValidateDie(int die)
        {
            if (die < 1 || die > 6)
                throw new ArgumentOutOfRangeException(nameof(die), die, "Кубик — 1..6.");
        }
    }
}
