using System;

namespace Burmalda.Progression
{
    /// <summary>
    /// Текущий Ярус Глубины забега (PRD v7 §22, issue #83): "Каждый
    /// побеждённый Босс = граница Яруса." Временный (в забеге) — новый
    /// экземпляр создаётся заново на каждый забег, как
    /// <c>Currencies.RunCurrencyAccumulator</c>. Постоянный рекорд — см.
    /// <see cref="DepthRecord"/>.
    /// </summary>
    public sealed class RunDepthTier
    {
        public int CurrentTier { get; private set; }

        /// <summary>Срабатывает при каждой победе над Боссом — с новым текущим Ярусом.</summary>
        public event Action<int> Advanced;

        public void RecordBossVictory()
        {
            CurrentTier++;
            Advanced?.Invoke(CurrentTier);
        }
    }
}
