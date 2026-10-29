using System;

namespace Burmalda.Camp
{
    /// <summary>
    /// Нелинейная конвертация Маны в Монеты при эвакуации (PRD v10 §26.1): убывающие диапазоны — ранние выгоднее. Базовый курс
    /// — <see cref="EvacuationSystem.ManaToCoinsRate"/> Маны за Монету; каждый следующий диапазон забирает меньшую долю
    /// базового курса. Больший забег всегда даёт больше Монет, но не пропорционально. ЧЕРНОВЫЕ значения [CONFIG]; форма
    /// <c>100% → 60% → 30% → 15%</c> — иллюстрация PRD, не баланс.
    /// </summary>
    public static class ManaConversion
    {
        /// <summary>Накопленные верхние границы диапазонов в Мане: первый — до 1000, второй — до 3000, третий — до 7000; дальше — остаток.</summary>
        public static int[] BracketLimits = { 1000, 3000, 7000 };

        /// <summary>Доля базового курса в каждом диапазоне (на один больше, чем границ).</summary>
        public static float[] Efficiency = { 1.00f, 0.60f, 0.30f, 0.15f };

        /// <summary>Монеты за <paramref name="mana"/> Маны при базовом курсе <paramref name="baseRate"/> (Маны за Монету).</summary>
        public static int CoinsFor(int mana, float baseRate, float multiplier = 1f)
        {
            if (mana <= 0 || baseRate <= 0f) return 0;
            double coins = 0;
            var from = 0;
            for (var i = 0; i < Efficiency.Length; i++)
            {
                var to = i < BracketLimits.Length ? Math.Min(BracketLimits[i], mana) : mana;
                if (to > from) coins += (to - from) / (double)baseRate * Efficiency[i];
                from = to;
                if (from >= mana) break;
            }

            return (int)Math.Round(coins * multiplier);
        }

        /// <summary>Эффективность последнего задействованного диапазона — для подсказки «каждая следующая Мана дешевле».</summary>
        public static float MarginalEfficiency(int mana)
        {
            for (var i = 0; i < BracketLimits.Length; i++)
                if (mana <= BracketLimits[i]) return Efficiency[i];
            return Efficiency[Efficiency.Length - 1];
        }
    }
}
