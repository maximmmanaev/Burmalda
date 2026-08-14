using System;
using System.Collections.Generic;
using System.Linq;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Временный (в забеге) билд из Амулетов/Талисманов (PRD раздел 6) —
    /// сбрасывается при смерти/выходе: новый экземпляр создаётся заново на
    /// каждый забег, как <c>Currencies.RunCurrencyAccumulator</c>, не
    /// переиспользуется. Каждое получение фиксируется в
    /// <see cref="ArtifactCollection"/> ("применённый хотя бы раз
    /// фиксируется навсегда, даже если сам инстанс исчезнет") и участвует в
    /// вычислении Созвучий (issue #79) в порядке получения.
    /// </summary>
    public sealed class RunArtifactLoadout
    {
        private readonly List<Artifact> _acquired = new List<Artifact>();
        private readonly ArtifactCollection _collection;

        public RunArtifactLoadout(ArtifactCollection collection)
        {
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        /// <summary>Амулеты/Талисманы, полученные за этот забег, в порядке получения.</summary>
        public IReadOnlyList<Artifact> Acquired => _acquired;

        /// <summary>Добавляет Амулет или Талисман в билд забега и фиксирует его в Коллекции.</summary>
        public void Acquire(Artifact artifact)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (artifact.Category != ArtifactCategory.Amulet && artifact.Category != ArtifactCategory.Talisman)
                throw new ArgumentException("Временный билд забега принимает только Амулеты и Талисманы (PRD раздел 6).", nameof(artifact));

            _acquired.Add(artifact);
            _collection.Record(artifact.Id);
        }

        /// <summary>Активные Созвучия для текущего билда (см. <see cref="ResonanceCalculator"/>).</summary>
        public IReadOnlyList<ResonanceType> ActiveResonances(bool unityConditionMet = false) =>
            ResonanceCalculator.Compute(_acquired.Select(a => a.Tags).ToList(), unityConditionMet);
    }
}
