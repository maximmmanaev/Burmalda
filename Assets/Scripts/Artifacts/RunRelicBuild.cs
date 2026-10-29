using Burmalda.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Билд забега из Реликвий (PRD v10 §13.4): 2 Амулета + 2 Талисмана; Рун — <see cref="RuneSlots"/> (старт 2).
    /// Пятый предмет типа — только вытеснением. Сбрасывается после похода (новый экземпляр на забег).
    /// </summary>
    public sealed class RunRelicBuild
    {
        public const int AmuletSlots = 2;
        public const int TalismanSlots = 2;

        /// <summary>Слотов Рун [CONFIG, черновик: старт 2, §13.4].</summary>
        public static int RuneSlots = 2;

        private readonly List<RelicDefinition> _items = new List<RelicDefinition>();

        public IReadOnlyList<RelicDefinition> Items => _items;

        public static int SlotsFor(ArtifactCategory category) => category switch
        {
            ArtifactCategory.Amulet => AmuletSlots,
            ArtifactCategory.Talisman => TalismanSlots,
            ArtifactCategory.Rune => RuneSlots,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "У Реликвии нет такого типа."),
        };

        public bool Contains(string id) => _items.Any(r => r.Id == id);

        public IReadOnlyList<RelicDefinition> OfType(ArtifactCategory category) => _items.Where(r => r.Category == category).ToList();

        public bool HasFreeSlot(ArtifactCategory category) => OfType(category).Count < SlotsFor(category);

        /// <summary>Кладёт Реликвию в свободный слот. False, если слоты типа заняты или такая уже есть (дубликатов нет, §32).</summary>
        public bool TryAdd(RelicDefinition relic)
        {
            if (relic == null) throw new ArgumentNullException(nameof(relic));
            if (Contains(relic.Id) || !HasFreeSlot(relic.Category)) return false;
            _items.Add(relic);
            return true;
        }

        /// <summary>Заменяет принадлежащий билду предмет того же типа новым. False, если условия не выполнены.</summary>
        public bool TryReplace(string dropId, RelicDefinition incoming)
        {
            if (incoming == null) throw new ArgumentNullException(nameof(incoming));
            if (Contains(incoming.Id)) return false;
            var drop = _items.FirstOrDefault(r => r.Id == dropId);
            if (drop == null || drop.Category != incoming.Category) return false;
            _items[_items.IndexOf(drop)] = incoming;
            return true;
        }

        /// <summary>Защищён ли билд от хотя бы одной из <paramref name="threats"/> Амулетом.</summary>
        public bool ProtectsAgainstAny(IEnumerable<ThreatType> threats) =>
            _items.Any(r => r.Category == ArtifactCategory.Amulet && r.Protects.Any(threats.Contains));
    }
}
