namespace Burmalda.Artifacts
{
    /// <summary>
    /// Руна (PRD раздел 6): расходуется в Алтаре (issue #21, Спринт 6),
    /// постоянно улучшает 1 из 2 пассивов Идола или активную способность
    /// Тотема. <see cref="Line"/> — линия прокачки, нужна для Созвучия
    /// «Единство» (PRD v7 §6.1, issue #79: "Идол и Тотем прокачаны Рунами
    /// одной линии"); конкретные названия линий не заданы в PRD — данные,
    /// не хардкод enum.
    /// </summary>
    public sealed class Rune : Artifact
    {
        public Rune(string id, string name, string line) : base(id, name, ArtifactCategory.Rune)
        {
            Line = line;
        }

        public string Line { get; }
    }
}
