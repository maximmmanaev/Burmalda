using System.Collections.Generic;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Талисман (PRD раздел 6): пассивный бонус к добыче, временный билд
    /// забега — сбрасывается при смерти/выходе (см. <see cref="RunArtifactLoadout"/>).
    /// <see cref="EffectDescription"/> — человекочитаемое описание; числовое
    /// применение эффекта к Currencies не подключено, см. issue #17 (тот же
    /// пробел, что у Amulet — Алтарь как источник выдачи ещё не реализован).
    /// </summary>
    public sealed class Talisman : Artifact
    {
        public Talisman(string id, string name, string effectDescription, IReadOnlyList<ArtifactTag> tags)
            : base(id, name, ArtifactCategory.Talisman, tags)
        {
            EffectDescription = effectDescription;
        }

        public string EffectDescription { get; }
    }
}
