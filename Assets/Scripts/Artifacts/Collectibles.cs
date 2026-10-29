using System;
using System.Collections.Generic;
using System.Linq;

namespace Burmalda.Artifacts
{
    public enum CollectibleKind
    {
        Idol,
        Totem,
    }

    /// <summary>Идол или Тотем как собираемый из частей предмет (PRD v10 §18.2, §19, §21.4). Только данные: способности — позже.</summary>
    public sealed class CollectibleItem
    {
        public CollectibleItem(string id, string name, CollectibleKind kind, RelicRarity rarity)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Rarity = rarity;
        }

        public string Id { get; }

        public string Name { get; }

        public CollectibleKind Kind { get; }

        public RelicRarity Rarity { get; }

        /// <summary>Частей до сборки: Редкий — 4, Эпический — 5, Легендарный — 6 (§21.4).</summary>
        public int PartsRequired => CollectibleCatalog.PartsRequired(Rarity);
    }

    /// <summary>5 Идолов и 5 Тотемов (рабочие названия §18.2, §19): по 3 Редких, 1 Эпическому, 1 Легендарному.</summary>
    public static class CollectibleCatalog
    {
        public static IReadOnlyList<CollectibleItem> All { get; } = new List<CollectibleItem>
        {
            new CollectibleItem("idol-rush", "Рывок", CollectibleKind.Idol, RelicRarity.Rare),
            new CollectibleItem("idol-shield", "Щит", CollectibleKind.Idol, RelicRarity.Rare),
            new CollectibleItem("idol-breach", "Пробой", CollectibleKind.Idol, RelicRarity.Rare),
            new CollectibleItem("idol-time", "Время", CollectibleKind.Idol, RelicRarity.Epic),
            new CollectibleItem("idol-passage", "Переход", CollectibleKind.Idol, RelicRarity.Legendary),

            new CollectibleItem("totem-maang", "Маанг", CollectibleKind.Totem, RelicRarity.Rare),
            new CollectibleItem("totem-amik", "Амик", CollectibleKind.Totem, RelicRarity.Rare),
            new CollectibleItem("totem-waagosh", "Ваагош", CollectibleKind.Totem, RelicRarity.Rare),
            new CollectibleItem("totem-waabizheshi", "Ваабижеши", CollectibleKind.Totem, RelicRarity.Epic),
            new CollectibleItem("totem-migizi", "Мигизи", CollectibleKind.Totem, RelicRarity.Legendary),
        };

        public static int PartsRequired(RelicRarity rarity) => rarity switch
        {
            RelicRarity.Rare => 4,
            RelicRarity.Epic => 5,
            RelicRarity.Legendary => 6,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, null),
        };

        public static CollectibleItem Get(string id) =>
            All.FirstOrDefault(i => i.Id == id) ?? throw new ArgumentException($"Нет предмета '{id}'.", nameof(id));

        public static bool TryGet(string id, out CollectibleItem item)
        {
            item = All.FirstOrDefault(i => i.Id == id);
            return item != null;
        }
    }
}
