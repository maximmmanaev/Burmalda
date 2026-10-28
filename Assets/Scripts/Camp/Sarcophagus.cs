using System;

namespace Burmalda.Camp
{
    /// <summary>Ранг бога, чьё Святилище пройдено (PRD v10 §5.2): определяет ценность Саркофага.</summary>
    public enum SarcophagusRank
    {
        Junior,
        Middle,
        Senior
    }

    /// <summary>Содержимое открытого Саркофага. Сейчас только Монеты (PRD v10 §21.2, п.1); часть Идола/Тотема и Реликвия добавятся сюда.</summary>
    public readonly struct SarcophagusContents
    {
        public SarcophagusContents(int coins) => Coins = coins;

        public int Coins { get; }
    }

    /// <summary>
    /// Саркофаг (PRD v10 §15, §22.3). В забеге все Саркофаги выглядят одинаково; ценность
    /// определяет ранг бога. Редкость, часть Идола/Тотема, Реликвия, защита от невезения —
    /// позже: всё это решается в <see cref="Open"/>, единственном месте, где рождается содержимое.
    /// </summary>
    public readonly struct Sarcophagus
    {
        /// <summary>ЧЕРНОВЫЕ значения [CONFIG]: Монеты в Саркофаге по рангу бога.</summary>
        public static int JuniorCoins = 80;
        public static int MiddleCoins = 140;
        public static int SeniorCoins = 220;

        public Sarcophagus(SarcophagusRank rank) => Rank = rank;

        public SarcophagusRank Rank { get; }

        public static int CoinsForRank(SarcophagusRank rank) => rank switch
        {
            SarcophagusRank.Junior => JuniorCoins,
            SarcophagusRank.Middle => MiddleCoins,
            SarcophagusRank.Senior => SeniorCoins,
            _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, null)
        };

        /// <summary>Открывает Саркофаг в Лагере: всегда Монеты (§21.2).</summary>
        public SarcophagusContents Open() => new SarcophagusContents(CoinsForRank(Rank));
    }
}
