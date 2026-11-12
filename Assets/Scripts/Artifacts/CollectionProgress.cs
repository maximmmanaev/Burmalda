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

        /// <summary>Активный Идол (слот 1, §17); null — «голый» старт, пока нечего поставить.</summary>
        public string ActiveIdolId { get; private set; }

        /// <summary>Активный Тотем (слот 1, §17); null — слот пуст.</summary>
        public string ActiveTotemId { get; private set; }

        /// <summary>Ставит собранный предмет в слот своего вида. False, если предмета нет или он не собран.</summary>
        public bool TrySelect(string itemId)
        {
            if (!CollectibleCatalog.TryGet(itemId, out var item) || !IsAssembled(item)) return false;
            if (item.Kind == CollectibleKind.Idol) ActiveIdolId = itemId;
            else ActiveTotemId = itemId;
            return true;
        }

        /// <summary>Восстановление из сохранения: активный предмет принимается, только если он собран.</summary>
        public void RestoreActive(string idolId, string totemId)
        {
            if (!string.IsNullOrEmpty(idolId)) TrySelect(idolId);
            if (!string.IsNullOrEmpty(totemId)) TrySelect(totemId);
        }

        /// <summary>Пустой слот занимает первый собранный предмет высшей редкости (без выбора игрока Идол/Тотем не стоял бы вовсе).</summary>
        public void AutoEquipIfEmpty()
        {
            if (ActiveIdolId == null) Best(CollectibleKind.Idol);
            if (ActiveTotemId == null) Best(CollectibleKind.Totem);
        }

        private void Best(CollectibleKind kind)
        {
            var best = CollectibleCatalog.All.Where(i => i.Kind == kind && IsAssembled(i)).OrderByDescending(i => (int)i.Rarity).FirstOrDefault();
            if (best != null) TrySelect(best.Id);
        }

        /// <summary>Уровень собранного предмета (1–3); 0, если не собран. Прокачка — спринт v10-5, задача 5.</summary>
        public int LevelOf(string itemId) => CollectibleCatalog.TryGet(itemId, out var item) && IsAssembled(item) ? 1 : 0;

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
            AutoEquipIfEmpty();
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
