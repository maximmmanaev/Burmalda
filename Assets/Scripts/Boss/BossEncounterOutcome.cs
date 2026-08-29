namespace Burmalda.Boss
{
    /// <summary>
    /// Результат встречи с Боссом (PRD раздел 8, issue #22) — см.
    /// <see cref="Boss.Resolve"/>. <c>CoinsFromOverflow</c> (Перелив энергии
    /// в Монеты, PRD v7 §8.2) удалён вместе с <c>Boss.OverflowToCoinsRate</c>
    /// (v9, задача по экономике "Мана как доход забега") — см. докстроку
    /// <see cref="Boss"/>.
    /// </summary>
    public sealed class BossEncounterOutcome
    {
        private BossEncounterOutcome(bool isVictory, bool hasBoostedRareRelicChance, int accumulatedMana, int requiredEnergy)
        {
            IsVictory = isVictory;
            HasBoostedRareRelicChance = hasBoostedRareRelicChance;
            AccumulatedMana = accumulatedMana;
            RequiredEnergy = requiredEnergy;
        }

        public bool IsVictory { get; }

        /// <summary>Накоплено сверх ×1.5 порога — повышенный шанс редкого исхода Реликвии (PRD v7 §8.2). Сама редкость исхода не резолвится здесь — вне текста issue #82.</summary>
        public bool HasBoostedRareRelicChance { get; }

        public int AccumulatedMana { get; }

        public int RequiredEnergy { get; }

        public static BossEncounterOutcome Victory(bool hasBoostedRareRelicChance, int accumulatedMana, int requiredEnergy) =>
            new BossEncounterOutcome(true, hasBoostedRareRelicChance, accumulatedMana, requiredEnergy);

        public static BossEncounterOutcome Defeat(int accumulatedMana, int requiredEnergy) =>
            new BossEncounterOutcome(false, false, accumulatedMana, requiredEnergy);
    }
}
