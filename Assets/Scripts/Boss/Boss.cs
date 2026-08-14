using System;

namespace Burmalda.Boss
{
    /// <summary>
    /// Босс (PRD раздел 8, issue #22): "луч энергии наносит Боссу урон
    /// пропорционально накопленным у игрока Кристаллам Маны... Хватило
    /// энергии — победа... Не хватило — поражение." Порог
    /// (<see cref="RequiredEnergy"/>) растёт с глубиной — кривая по Ярусам
    /// ещё не существует (Спринт 8), внедряется вызывающей стороной как
    /// готовое число (см. <c>BossEncounterSystem</c>), как и у
    /// <c>Altar.ManaToBossIndicator</c>.
    ///
    /// <see cref="OverflowToCoinsRate"/>/<see cref="OverflowRelicBonusMultiplier"/>
    /// (PRD v7 §8.2, issue #82) — черновые значения, предмет баланса
    /// (Спринт 10).
    /// </summary>
    public sealed class Boss
    {
        public const double OverflowToCoinsRate = 0.1;
        public const double OverflowRelicBonusMultiplier = 1.5;

        public Boss(int requiredEnergy)
        {
            if (requiredEnergy <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredEnergy), requiredEnergy, "Требуемая энергия должна быть положительной.");
            RequiredEnergy = requiredEnergy;
        }

        public int RequiredEnergy { get; }

        /// <summary>Разрешает встречу по накопленным Кристаллам Маны игрока на момент встречи.</summary>
        public BossEncounterOutcome Resolve(int accumulatedMana)
        {
            if (accumulatedMana < RequiredEnergy)
                return BossEncounterOutcome.Defeat(accumulatedMana, RequiredEnergy);

            var overflow = accumulatedMana - RequiredEnergy;
            var coinsFromOverflow = (int)(overflow * OverflowToCoinsRate);
            var hasBoostedRareRelicChance = accumulatedMana >= RequiredEnergy * OverflowRelicBonusMultiplier;
            return BossEncounterOutcome.Victory(coinsFromOverflow, hasBoostedRareRelicChance, accumulatedMana, RequiredEnergy);
        }
    }
}
