namespace Burmalda.Artifacts
{
    /// <summary>
    /// Идол (PRD раздел 6): постоянная прокачиваемая пассивная способность
    /// (2 слота — см. <see cref="IdolLoadout"/>). Руна улучшает 1 из 2
    /// пассивов (PRD v6 §7) — конкретный игровой эффект каждого пассива не
    /// в скоупе этой задачи (нужна система эффектов/статов поверх Currencies/
    /// Boss, которых частично ещё нет), только уровень прокачки.
    /// </summary>
    public sealed class Idol : Artifact
    {
        public Idol(string id, string name) : base(id, name, ArtifactCategory.Idol)
        {
        }

        public int PassiveALevel { get; private set; }

        public int PassiveBLevel { get; private set; }

        /// <summary>Улучшает первый пассив Руной (см. issue #21, Сундук Рун — Спринт 6).</summary>
        public void UpgradePassiveA() => PassiveALevel++;

        /// <summary>Улучшает второй пассив Руной.</summary>
        public void UpgradePassiveB() => PassiveBLevel++;
    }
}
