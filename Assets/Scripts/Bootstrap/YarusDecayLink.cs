using Burmalda.Core;
using UnityEngine;

namespace Burmalda.Bootstrap
{
    /// <summary>
    /// Связывает Ярус под игроком с временем жизни плит (PRD v10 §7.6, спринт v10-2): каждый кадр
    /// ставит <see cref="Decay.TrailDecaySystem.YarusLifetimeMultiplier"/> по полосе текущего Яруса.
    /// Отдельный компонент, потому что Decay и Generation друг о друге не знают.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class YarusDecayLink : MonoBehaviour
    {
        private void Update()
        {
            var bootstrap = RunBootstrap.Instance;
            var decay = bootstrap?.Decay?.Decay;
            var segments = bootstrap?.Segments;
            if (decay == null || segments == null) return;
            decay.YarusLifetimeMultiplier = YarusProgression.DecayLifetimeMultiplier(segments.CurrentYarus);
        }
    }
}
