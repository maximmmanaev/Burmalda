using Burmalda.Artifacts;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>Подписи и цвета экрана выбора Реликвии (PRD v10 §13.2): минимум текста, редкость — рамкой.</summary>
    public static class AltarPickText
    {
        public static readonly Color RareColor = new Color(0.30f, 0.55f, 0.95f);
        public static readonly Color EpicColor = new Color(0.65f, 0.35f, 0.90f);
        public static readonly Color LegendaryColor = new Color(0.98f, 0.80f, 0.25f);

        public static Color RarityColor(RelicRarity rarity) => rarity switch
        {
            RelicRarity.Rare => RareColor,
            RelicRarity.Epic => EpicColor,
            _ => LegendaryColor,
        };

        /// <summary>Пиктограмма типа (временная, буква в круге до финального арта): А — Амулет, Т — Талисман, Р — Руна.</summary>
        public static string KindGlyph(ArtifactCategory category) => category switch
        {
            ArtifactCategory.Amulet => "А",
            ArtifactCategory.Talisman => "Т",
            _ => "Р",
        };

        public static string KindName(ArtifactCategory category) => category switch
        {
            ArtifactCategory.Amulet => "Амулет",
            ArtifactCategory.Talisman => "Талисман",
            _ => "Руна",
        };

        public static string Title(int optionCount) => optionCount >= 3 ? "Выбери одну из трёх" : "Выбери одну из двух";

        public static string ReplaceTitle(ArtifactCategory category) => $"Слоты заполнены — что заменить? ({KindName(category)})";

        public static string VaultMarker(int pendingExtraOptions) => pendingExtraOptions > 0 ? $"Тайник: +{pendingExtraOptions} вариант на Алтаре" : "";
    }
}
