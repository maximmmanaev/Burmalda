using System;
using System.Collections.Generic;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Общий термин для Идола/Тотема/Амулета/Талисмана/Руны/Реликвии (PRD
    /// раздел 6). Теги (PRD v7 §6.1) осмысленны только для Амулета/
    /// Талисмана — у остальных категорий пустой список.
    /// </summary>
    public abstract class Artifact
    {
        protected Artifact(string id, string name, ArtifactCategory category, IReadOnlyList<ArtifactTag> tags = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id артефакта не может быть пустым.", nameof(id));
            Id = id;
            Name = name;
            Category = category;
            Tags = tags ?? Array.Empty<ArtifactTag>();
        }

        /// <summary>Уникальный идентификатор вида артефакта — используется Коллекцией и Пулом (issue #18).</summary>
        public string Id { get; }

        public string Name { get; }

        public ArtifactCategory Category { get; }

        /// <summary>1–2 тега (PRD v7 §6.1). Пусто для Идола/Тотема/Руны/Реликвии.</summary>
        public IReadOnlyList<ArtifactTag> Tags { get; }
    }
}
