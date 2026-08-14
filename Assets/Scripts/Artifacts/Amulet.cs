using System.Collections.Generic;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Амулет (PRD раздел 6): пассивная защита, временный билд забега —
    /// сбрасывается при смерти/выходе (см. <see cref="RunArtifactLoadout"/>).
    /// <see cref="EffectDescription"/> — человекочитаемое описание для
    /// витрины/тестов; числовое применение эффекта (например, иммунитет к
    /// ловушкам) не подключено — зависит от систем, которых ещё нет целиком
    /// (Алтарь как источник выдачи — Спринт 6, d20 — Спринт 7), см. issue #17.
    /// </summary>
    public sealed class Amulet : Artifact
    {
        public Amulet(string id, string name, string effectDescription, IReadOnlyList<ArtifactTag> tags)
            : base(id, name, ArtifactCategory.Amulet, tags)
        {
            EffectDescription = effectDescription;
        }

        public string EffectDescription { get; }
    }
}
