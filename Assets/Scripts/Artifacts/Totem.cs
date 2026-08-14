namespace Burmalda.Artifacts
{
    /// <summary>
    /// Тотем (PRD раздел 6, 12): постоянный держатель ОДНОЙ активной
    /// способности, вызываемой двойным тапом. Сама активация/тайминг/эффект
    /// — issue #28 (Спринт 9); здесь только хранение типа способности и
    /// уровня прокачки Руной.
    /// </summary>
    public sealed class Totem : Artifact
    {
        public Totem(string id, string name, TotemAbilityType activeAbility) : base(id, name, ArtifactCategory.Totem)
        {
            ActiveAbility = activeAbility;
        }

        public TotemAbilityType ActiveAbility { get; }

        public int Level { get; private set; }

        /// <summary>Улучшает активную способность Руной (см. issue #21, Сундук Рун — Спринт 6).</summary>
        public void UpgradeLevel() => Level++;
    }
}
