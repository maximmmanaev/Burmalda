using System;
using System.Collections.Generic;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Описание Реликвии из каталога PRD v10 §20.2: Амулет, Талисман или Руна конкретного бога. Эффект здесь —
    /// короткое описание для экрана Алтаря (у игры описания визуальные, §20.1); сами эффекты подключаются отдельно.
    /// </summary>
    public sealed class RelicDefinition
    {
        public RelicDefinition(string id, string name, God god, ArtifactCategory category, RelicRarity rarity, string effect, params ThreatType[] protects)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id Реликвии не может быть пустым.", nameof(id));
            if (category != ArtifactCategory.Amulet && category != ArtifactCategory.Talisman && category != ArtifactCategory.Rune)
                throw new ArgumentOutOfRangeException(nameof(category), category, "Реликвия — Амулет, Талисман или Руна (§20).");
            if (protects.Length > 0 && category != ArtifactCategory.Amulet)
                throw new ArgumentException("Защита от угроз — только у Амулетов.", nameof(protects));
            Id = id;
            Name = name;
            God = god;
            Category = category;
            Rarity = rarity;
            Effect = effect;
            Protects = protects;
        }

        public string Id { get; }

        public string Name { get; }

        public God God { get; }

        public ArtifactCategory Category { get; }

        public RelicRarity Rarity { get; }

        /// <summary>Короткое описание эффекта (черновик из §20.2).</summary>
        public string Effect { get; }

        /// <summary>От каких типов угроз защищает (только Амулеты; пусто у остальных).</summary>
        public IReadOnlyList<ThreatType> Protects { get; }

        public Mythology Mythology => RelicCatalog.MythologyOf(God);
    }
}
