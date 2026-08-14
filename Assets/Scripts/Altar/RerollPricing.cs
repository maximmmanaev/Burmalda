using System;

namespace Burmalda.Altar
{
    /// <summary>
    /// Цена реролла витрины Ритуала (PRD v7 §7, issue #81): "первый реролл
    /// на Алтаре — по базовой цене, каждый следующий дороже ×1.5".
    /// <see cref="BaseCost"/> — черновое значение (в Ключах), точная цифра —
    /// предмет баланса (Спринт 10, docs/rules/forbidden-actions.md).
    /// </summary>
    public static class RerollPricing
    {
        public const int BaseCost = 50;
        public const double EscalationFactor = 1.5;

        /// <summary>Цена реролла с номером <paramref name="rerollNumber"/> (1 — первый реролл в этом посещении Алтаря).</summary>
        public static int CostForRerollNumber(int rerollNumber)
        {
            if (rerollNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(rerollNumber), rerollNumber, "Номер реролла должен быть >= 1.");

            return (int)Math.Round(BaseCost * Math.Pow(EscalationFactor, rerollNumber - 1));
        }
    }
}
