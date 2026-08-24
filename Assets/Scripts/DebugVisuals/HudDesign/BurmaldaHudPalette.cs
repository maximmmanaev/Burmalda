using UnityEngine;

namespace Burmalda.DebugVisuals.HudDesign
{
    /// <summary>
    /// Палитра дизайн-системы HUD из Claude Design (issue "Собрать Unity UI
    /// для HUD забега"), см. <c>docs/design/burmalda-hud-design-spec.md</c>.
    /// Значения — буквальная копия таблицы OKLCH→sRGB из спеки (честная
    /// конвертация дизайнера, не пересчитано и не подобрано на глаз здесь).
    /// </summary>
    public static class BurmaldaHudPalette
    {
        public static readonly Color32 DungeonBackground = new Color32(13, 6, 3, 255);
        public static readonly Color32 SurfaceCard = new Color32(35, 20, 11, 255);
        public static readonly Color32 StoneBorder = new Color32(69, 52, 42, 255);
        public static readonly Color32 GoldAccent = new Color32(234, 181, 50, 255);
        public static readonly Color32 ManaPurple = new Color32(180, 141, 244, 255);
        public static readonly Color32 CopperCoin = new Color32(231, 147, 99, 255);
        public static readonly Color32 Danger = new Color32(226, 73, 71, 255);
        public static readonly Color32 LegendaryPink = new Color32(239, 130, 205, 255);
        public static readonly Color32 TextPrimary = new Color32(245, 241, 234, 255);
        public static readonly Color32 TextSecondary = new Color32(173, 163, 151, 255);
        public static readonly Color32 TextTertiary = new Color32(136, 126, 116, 255);
        public static readonly Color32 GoldButtonTop = new Color32(244, 191, 63, 255);
        public static readonly Color32 GoldButtonBottom = new Color32(205, 136, 0, 255);
        public static readonly Color32 GoldButtonShadow = new Color32(151, 88, 0, 255);
        public static readonly Color32 DangerButtonTop = new Color32(247, 93, 89, 255);
        public static readonly Color32 DangerButtonBottom = new Color32(186, 43, 46, 255);
        public static readonly Color32 EmptySlotDash = new Color32(85, 67, 57, 255);
        public static readonly Color32 BossRoomBackgroundTop = new Color32(25, 12, 7, 255);
        public static readonly Color32 BossRoomBackgroundBottom = new Color32(12, 4, 2, 255);
        public static readonly Color32 GoldButtonText = new Color32(20, 11, 6, 255);
    }
}
