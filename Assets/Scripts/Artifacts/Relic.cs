namespace Burmalda.Artifacts
{
    /// <summary>
    /// Реликвия (PRD раздел 6, 8): награда за победу над Боссом (Спринт 7,
    /// ещё не реализован) — обмен на Монеты ИЛИ разлок нового Идола/Тотема в
    /// общий пул. Минимальный shell — вручение и выбор реализуются в Спринте 7.
    /// </summary>
    public sealed class Relic : Artifact
    {
        public Relic(string id, string name) : base(id, name, ArtifactCategory.Relic)
        {
        }
    }
}
