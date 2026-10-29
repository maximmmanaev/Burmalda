using Burmalda.Artifacts;

namespace Burmalda.Camp
{
    /// <summary>Ранг бога (PRD v10 §5.2): младший / средний / старший — определяет шансы редкости Саркофага (§21.1).</summary>
    public enum SarcophagusRank
    {
        Junior,
        Middle,
        Senior
    }

    /// <summary>Содержимое открытого Саркофага (PRD v10 §21.2): Монеты, редкость и то, что выпало сверх Монет.</summary>
    public readonly struct SarcophagusContents
    {
        public SarcophagusContents(SarcophagusLoot loot) => Loot = loot;

        public SarcophagusLoot Loot { get; }

        public int Coins => Loot.Coins;

        public RelicRarity Rarity => Loot.Rarity;
    }

    /// <summary>
    /// Саркофаг, вынесенный из Святилища (PRD v10 §15.1, §21): ранг и бог Святилища, редкость выпадает при взятии
    /// (<see cref="SarcophagusRarityRoller"/>) и скрыта до открытия в Лагере (§22.3).
    /// </summary>
    public readonly struct Sarcophagus
    {
        public Sarcophagus(SarcophagusRank rank, RelicRarity rarity = RelicRarity.Rare, God god = God.Leshy)
        {
            Rank = rank;
            Rarity = rarity;
            God = god;
        }

        public SarcophagusRank Rank { get; }

        public RelicRarity Rarity { get; }

        /// <summary>Бог Святилища, откуда вынесен Саркофаг (для предпочтения Реликвий его мифологии, §21.3).</summary>
        public God God { get; }
    }
}
