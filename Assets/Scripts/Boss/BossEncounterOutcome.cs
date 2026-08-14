namespace Burmalda.Boss
{
    /// <summary>Результат встречи с Боссом (PRD раздел 8, issue #22) — см. <see cref="Boss.Resolve"/>.</summary>
    public sealed class BossEncounterOutcome
    {
        private BossEncounterOutcome(bool isVictory, int coinsFromOverflow, bool hasBoostedRareRelicChance, int accumulatedMana, int requiredEnergy)
        {
            IsVictory = isVictory;
            CoinsFromOverflow = coinsFromOverflow;
            HasBoostedRareRelicChance = hasBoostedRareRelicChance;
            AccumulatedMana = accumulatedMana;
            RequiredEnergy = requiredEnergy;
        }

        public bool IsVictory { get; }

        /// <summary>Монеты от Перелива энергии сверх порога (PRD v7 §8.2). 0 при поражении.</summary>
        public int CoinsFromOverflow { get; }

        /// <summary>Накоплено сверх ×1.5 порога — повышенный шанс редкого исхода Реликвии (PRD v7 §8.2). Сама редкость исхода не резолвится здесь — вне текста issue #82.</summary>
        public bool HasBoostedRareRelicChance { get; }

        public int AccumulatedMana { get; }

        public int RequiredEnergy { get; }

        public static BossEncounterOutcome Victory(int coinsFromOverflow, bool hasBoostedRareRelicChance, int accumulatedMana, int requiredEnergy) =>
            new BossEncounterOutcome(true, coinsFromOverflow, hasBoostedRareRelicChance, accumulatedMana, requiredEnergy);

        public static BossEncounterOutcome Defeat(int accumulatedMana, int requiredEnergy) =>
            new BossEncounterOutcome(false, 0, false, accumulatedMana, requiredEnergy);
    }
}
