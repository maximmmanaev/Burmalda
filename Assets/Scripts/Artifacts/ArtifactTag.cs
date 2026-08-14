namespace Burmalda.Artifacts
{
    /// <summary>
    /// Тег артефакта (PRD v7 §6.1, issue #79): каждый Амулет/Талисман
    /// получает 1–2 тега; совпадение тегов в активном билде даёт Созвучие
    /// (см. <see cref="ResonanceType"/>).
    /// </summary>
    public enum ArtifactTag
    {
        /// <summary>Всё, что связано с Кристаллами Маны.</summary>
        Mana,

        /// <summary>Добыча и трата Ключей.</summary>
        Keys,

        /// <summary>Монеты, множитель, длина маршрута.</summary>
        Greed,

        /// <summary>Ловушки, d20, спасения.</summary>
        Defense,

        /// <summary>Перемещение, распад, доступ к труднодоступным плитам.</summary>
        Movement
    }
}
