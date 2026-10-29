using System;
using System.Collections.Generic;
using System.Linq;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Постоянный прогресс сбора и защита от невезения (PRD v10 §21.2–21.4, §29): части Идолов/Тотемов по предметам,
    /// счётчики «пустых» Саркофагов по редкости (для части и для Реликвии) и флаг выданной первой части.
    /// </summary>
    public sealed class CollectionProgress
    {
        private readonly Dictionary<string, int> _parts = new Dictionary<string, int>();
        private readonly int[] _partMisses = new int[3];
        private readonly int[] _relicMisses = new int[3];

        /// <summary>Первая вынесенная часть уже выдана (первый Саркофаг всегда даёт часть Редкого, §21.2).</summary>
        public bool FirstPartGranted { get; set; }

        public IReadOnlyDictionary<string, int> Parts => _parts;

        public int PartsOf(string itemId) => _parts.TryGetValue(itemId, out var n) ? n : 0;

        public bool IsAssembled(CollectibleItem item) => PartsOf(item.Id) >= item.PartsRequired;

        /// <summary>Несобранные предметы редкости (порядок каталога).</summary>
        public IReadOnlyList<CollectibleItem> Unassembled(RelicRarity rarity) =>
            CollectibleCatalog.All.Where(i => i.Rarity == rarity && !IsAssembled(i)).ToList();

        /// <summary>Уже начатый несобранный предмет редкости (есть хотя бы одна часть); null, если такого нет.</summary>
        public CollectibleItem Started(RelicRarity rarity) => Unassembled(rarity).FirstOrDefault(i => PartsOf(i.Id) > 0);

        /// <summary>Кладёт часть в предмет. False, если предмет уже собран.</summary>
        public bool AddPart(string itemId)
        {
            var item = CollectibleCatalog.Get(itemId);
            if (IsAssembled(item)) return false;
            _parts[itemId] = PartsOf(itemId) + 1;
            return true;
        }

        public void SetParts(string itemId, int count)
        {
            if (!CollectibleCatalog.TryGet(itemId, out var item)) return;
            _parts[itemId] = Math.Max(0, Math.Min(count, item.PartsRequired));
        }

        public int PartMisses(RelicRarity rarity) => _partMisses[(int)rarity];

        public int RelicMisses(RelicRarity rarity) => _relicMisses[(int)rarity];

        public void SetPartMisses(RelicRarity rarity, int value) => _partMisses[(int)rarity] = Math.Max(0, value);

        public void SetRelicMisses(RelicRarity rarity, int value) => _relicMisses[(int)rarity] = Math.Max(0, value);
    }
}
