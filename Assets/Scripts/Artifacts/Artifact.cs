using System;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Общий термин для Идола/Тотема/Руны/Реликвии (PRD
    /// v10 §17). Амулеты и Талисманы — <see cref="RelicDefinition"/>; Теги и Созвучия убраны (v10-4).
    /// </summary>
    public abstract class Artifact
    {
        protected Artifact(string id, string name, ArtifactCategory category)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id артефакта не может быть пустым.", nameof(id));
            Id = id;
            Name = name;
            Category = category;
        }

        /// <summary>Уникальный идентификатор вида артефакта — используется Коллекцией и Пулом (issue #18).</summary>
        public string Id { get; }

        public string Name { get; }

        public ArtifactCategory Category { get; }
    }
}
