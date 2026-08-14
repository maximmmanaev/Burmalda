using System;

namespace Burmalda.Progression
{
    /// <summary>
    /// Рекорд глубины (PRD v7 §22, issue #83): "Ярус фиксируется навсегда
    /// как рекорд глубины." Постоянный, как <c>Currencies.PersistentWallet</c>
    /// — не пересобирается между забегами, только растёт.
    /// </summary>
    public sealed class DepthRecord
    {
        public int BestTier { get; private set; }

        /// <summary>Срабатывает, когда рекорд обновляется — с новым лучшим Ярусом.</summary>
        public event Action<int> RecordBroken;

        public void ReportTier(int tier)
        {
            if (tier <= BestTier) return;
            BestTier = tier;
            RecordBroken?.Invoke(tier);
        }
    }
}
