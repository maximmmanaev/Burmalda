using Burmalda.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Каталог 30 Реликвий PRD v10 §20.2 (10 богов × Амулет, Талисман, Руна; у каждого бога одна Редкая, одна Эпическая,
    /// одна Легендарная). Названия и эффекты — черновик, утверждённый владельцем; числа эффектов — [CONFIG].
    /// Заменяет прежний <see cref="ArtifactCatalog"/> (Приложение Б PRD) на экране Алтаря.
    /// </summary>
    public static class RelicCatalog
    {
        /// <summary>С самого начала открыты три Редкие (§20.1): Амулет Лешего, Талисман Мокоши, Руна Перуна.</summary>
        public static readonly IReadOnlyList<string> StarterIds = new[] { "relic-leshy-amulet", "relic-mokosh-talisman", "relic-perun-rune" };

        /// <summary>Талисман «Ключ подземных врат» — с шансом даёт третий вариант на Алтаре (§20.2).</summary>
        public const string ThirdOptionTalismanId = "relic-ereshkigal-talisman";

        private const ArtifactCategory A = ArtifactCategory.Amulet;
        private const ArtifactCategory T = ArtifactCategory.Talisman;
        private const ArtifactCategory R = ArtifactCategory.Rune;

        public static IReadOnlyList<RelicDefinition> All { get; } = new List<RelicDefinition>
        {
            new RelicDefinition("relic-leshy-amulet", "Берестяной оберег", God.Leshy, A, RelicRarity.Rare, "Давилка и Стены закрываются медленнее", ThreatType.Blunt),
            new RelicDefinition("relic-leshy-talisman", "Лешачья тропа", God.Leshy, T, RelicRarity.Epic, "Бонус Маны за серию ×2"),
            new RelicDefinition("relic-leshy-rune", "Морок", God.Leshy, R, RelicRarity.Legendary, "5 с шаги не активируют ловушки"),

            new RelicDefinition("relic-mokosh-amulet", "Плат Мокоши", God.Mokosh, A, RelicRarity.Legendary, "Раз за Ярус переживаешь любое попадание ловушки",
                ThreatType.Blunt, ThreatType.Slashing, ThreatType.Explosion, ThreatType.Fire, ThreatType.Trap),
            new RelicDefinition("relic-mokosh-talisman", "Веретено", God.Mokosh, T, RelicRarity.Rare, "Каждая 5-я плита даёт бонус Маны"),
            new RelicDefinition("relic-mokosh-rune", "Распутье", God.Mokosh, R, RelicRarity.Epic, "Видны типы всех триггеров в 5 рядах впереди"),

            new RelicDefinition("relic-perun-amulet", "Перунов знак", God.Perun, A, RelicRarity.Epic, "Раз за Ярус переживаешь Огненную струю", ThreatType.Fire),
            new RelicDefinition("relic-perun-talisman", "Громовой знак", God.Perun, T, RelicRarity.Legendary, "Резонанс даёт +0.8 вместо +0.5"),
            new RelicDefinition("relic-perun-rune", "Удар Перуна", God.Perun, R, RelicRarity.Rare, "Разбивает соседнюю стену"),

            new RelicDefinition("relic-humbaba-amulet", "Кедровая маска", God.Humbaba, A, RelicRarity.Rare, "Раз за Ярус обезвреживает Бомбу или Капкан", ThreatType.Explosion, ThreatType.Trap),
            new RelicDefinition("relic-humbaba-talisman", "Кедровая смола", God.Humbaba, T, RelicRarity.Epic, "Плиты-источники дают ×2 Маны"),
            new RelicDefinition("relic-humbaba-rune", "Рёв стража", God.Humbaba, R, RelicRarity.Legendary, "Все ловушки в 3 рядах впереди срабатывают разом, пока ты стоишь в стороне"),

            new RelicDefinition("relic-ereshkigal-amulet", "Покров царицы", God.Ereshkigal, A, RelicRarity.Legendary, "Стрелы и Лезвия не убивают", ThreatType.Slashing),
            new RelicDefinition("relic-ereshkigal-talisman", "Ключ подземных врат", God.Ereshkigal, T, RelicRarity.Rare, "Каждый Алтарь с шансом 33% предлагает третий вариант"),
            new RelicDefinition("relic-ereshkigal-rune", "Семь врат", God.Ereshkigal, R, RelicRarity.Epic, "Открывает Ворота без рычага"),

            new RelicDefinition("relic-enlil-amulet", "Ветряной щит", God.Enlil, A, RelicRarity.Epic, "Раз за Ярус переживаешь Лезвия", ThreatType.Slashing),
            new RelicDefinition("relic-enlil-talisman", "Скрижаль Энлиля", God.Enlil, T, RelicRarity.Legendary, "Множитель Святилища стартует с ×2"),
            new RelicDefinition("relic-enlil-rune", "Порыв", God.Enlil, R, RelicRarity.Rare, "Останавливает одну активную ловушку рядом"),

            new RelicDefinition("relic-memegwesi-amulet", "Речной камешек", God.Memegwesi, A, RelicRarity.Rare, "Раз за Ярус переживаешь Камень или Валун", ThreatType.Blunt),
            new RelicDefinition("relic-memegwesi-talisman", "Дар речных духов", God.Memegwesi, T, RelicRarity.Epic, "Пока серия чистого прохода ≥ 10 плит, каждая плита даёт ×2 Маны"),
            new RelicDefinition("relic-memegwesi-rune", "Песня малых духов", God.Memegwesi, R, RelicRarity.Legendary, "Идол мгновенно полностью заряжается"),

            new RelicDefinition("relic-mishibijiu-amulet", "Медный обруч", God.Mishibijiu, A, RelicRarity.Legendary, "Капкан и Валун на тебе не срабатывают", ThreatType.Trap, ThreatType.Blunt),
            new RelicDefinition("relic-mishibijiu-talisman", "Медный самородок", God.Mishibijiu, T, RelicRarity.Rare, "Жилы дают +50% Маны"),
            new RelicDefinition("relic-mishibijiu-rune", "Тёмная вода", God.Mishibijiu, R, RelicRarity.Epic, "Гасит все триггеры ряда впереди"),

            new RelicDefinition("relic-animikii-amulet", "Громовой щит", God.Animikii, A, RelicRarity.Epic, "Раз за Ярус переживаешь Стрелу", ThreatType.Slashing),
            new RelicDefinition("relic-animikii-talisman", "Глаз грозы", God.Animikii, T, RelicRarity.Legendary, "Эхо даёт ×3 вместо ×2"),
            new RelicDefinition("relic-animikii-rune", "Перо грома", God.Animikii, R, RelicRarity.Rare, "Все активные ловушки замирают на 2 с"),

            new RelicDefinition("relic-chernobog-amulet", "Тень Чернобога", God.Chernobog, A, RelicRarity.Rare, "На уход с Капкана вдвое больше времени", ThreatType.Trap),
            new RelicDefinition("relic-chernobog-talisman", "Чёрное золото", God.Chernobog, T, RelicRarity.Epic, "Мана ×1.5, но пол распадается быстрее"),
            new RelicDefinition("relic-chernobog-rune", "Ночь Чернобога", God.Chernobog, R, RelicRarity.Legendary, "Обрушение Святилища останавливается на 3 с"),
        };

        private static readonly Dictionary<string, RelicDefinition> ById = All.ToDictionary(r => r.Id);

        public static bool TryGet(string id, out RelicDefinition relic) => ById.TryGetValue(id, out relic);

        public static RelicDefinition Get(string id) =>
            ById.TryGetValue(id, out var relic) ? relic : throw new ArgumentException($"Нет Реликвии с id '{id}'.", nameof(id));

        /// <summary>Мифология бога — из таблицы Ярусов (<see cref="GodTable"/>, PRD v10 §5.2).</summary>
        public static Mythology MythologyOf(God god) => GodTable.ForGod(god).Mythology;

        /// <summary>Бог Святилища Яруса — из таблицы <see cref="GodTable"/>.</summary>
        public static God GodOfYarus(int yarus) => GodTable.ForYarus(yarus).God;
    }
}
