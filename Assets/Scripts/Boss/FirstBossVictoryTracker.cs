using System;

namespace Burmalda.Boss
{
    /// <summary>
    /// Флаг "победа хотя бы один раз в этом прохождении" (PRD раздел 6, issue
    /// #22): "Талисманы и Амулеты становятся доступны в общем пуле... только
    /// после первой победы над Боссом". Постоянный, как
    /// <c>Artifacts.ArtifactPool</c> — не пересобирается между забегами.
    /// </summary>
    public sealed class FirstBossVictoryTracker
    {
        public bool HasWonBefore { get; private set; }

        /// <summary>Срабатывает один раз — при самой первой победе за прохождение.</summary>
        public event Action FirstVictory;

        public void RecordVictory()
        {
            if (HasWonBefore) return;
            HasWonBefore = true;
            FirstVictory?.Invoke();
        }
    }
}
