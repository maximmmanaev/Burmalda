using System;
using Burmalda.Artifacts;

namespace Burmalda.Camp
{
    /// <summary>Цены прокачки Идола/Тотема в Лагере (PRD v10 §22.4): 3 уровня, собранный предмет начинает с 1. ЧЕРНОВЫЕ [CONFIG].</summary>
    public static class CampUpgrades
    {
        /// <summary>Цена перехода L1→L2 и L2→L3 для Редкого предмета, Монеты [CONFIG, черновик].</summary>
        public static int[] RarePrices = { 600, 1800 };

        /// <summary>Множители цены для Эпических и Легендарных предметов [CONFIG, черновик].</summary>
        public static float EpicFactor = 1.5f;

        public static float LegendaryFactor = 2f;

        /// <summary>Цена повышения предмета с уровня <paramref name="fromLevel"/> (1 или 2); 0 — повышать нельзя.</summary>
        public static int PriceFor(CollectibleItem item, int fromLevel)
        {
            if (fromLevel < 1 || fromLevel >= CollectionProgress.MaxLevel) return 0;
            var factor = item.Rarity == RelicRarity.Epic ? EpicFactor : item.Rarity == RelicRarity.Legendary ? LegendaryFactor : 1f;
            return (int)Math.Round(RarePrices[fromLevel - 1] * factor);
        }
    }
}
