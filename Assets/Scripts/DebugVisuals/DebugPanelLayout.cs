namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Единый владелец раскладки отладочных кнопок/панелей по углам экрана
    /// (задача «рычаг и ворота невидимы, HUD накладывается сам на себя»,
    /// плейтест владельца — «ЯРУС 0 налезает на кнопку ECO»). Раньше каждая
    /// панель считала свой отступ сама, ссылаясь на размеры соседей по
    /// имени прямо в формуле (<c>EconomyDebugPanel.TopOffset = Margin +
    /// RestartButtonHeight + Margin</c>, <c>ReturnToCampDebugButton</c> — то
    /// же самое ещё на шаг длиннее) — на устройстве разошлось:
    /// <c>HudDesign.RunHudDesignOverlay</c> считал резерв под кнопки на
    /// СВОЙ лад (жёстко "96 пикселей", не запрашивая ни у кого реальный
    /// размер) и промахнулся. Теперь размер/отступ каждой кнопки — здесь,
    /// один раз, остальные файлы читают отсюда, не пересчитывают сами.
    ///
    /// Единицы — "сырые" device-пиксели (<c>ScreenSpaceOverlay</c> +
    /// <c>CanvasScaler</c> по умолчанию = <c>ConstantPixelSize</c>), тот же
    /// режим, что использует каждая debug-панель. <c>RunHudDesignOverlay</c>
    /// — единственный потребитель на ДРУГОМ масштабе (<c>ScaleWithScreenSize</c>)
    /// — сам делит зарезервированную высоту на свой текущий
    /// <c>CanvasScaler.scaleFactor</c> при чтении (см. её doc-комментарий).
    /// </summary>
    public static class DebugPanelLayout
    {
        public const float Margin = 24f;

        // ---------- Верхний левый угол: CAM ----------
        public const float CamButtonSize = 72f;
        public const float CamTopOffset = Margin;

        // ---------- Верхний правый угол, сверху вниз: RESTART → ECO → TO CAMP ----------
        public const float RestartButtonWidth = 220f;
        public const float RestartButtonHeight = 90f;
        public const float RestartTopOffset = Margin;

        public const float EcoButtonSize = 72f;
        public const float EcoTopOffset = RestartTopOffset + RestartButtonHeight + Margin;

        public const float ToCampButtonWidth = 220f;
        public const float ToCampButtonHeight = 90f;
        public const float ToCampTopOffset = EcoTopOffset + EcoButtonSize + Margin;

        /// <summary>
        /// Суммарная высота, занятая сверху вниз в правом верхнем углу
        /// (RESTART+ECO+TO CAMP включительно) — читается
        /// <c>RunHudDesignOverlay</c> для отступа "ЯРУС", единственного
        /// player-facing элемента в том же углу.
        /// </summary>
        public const float TopRightReservedHeight = ToCampTopOffset + ToCampButtonHeight;

        // ---------- Верхний центр: HUD (одна кнопка, соседей по X нет) ----------
        public const float HudButtonWidth = 160f;
        public const float HudButtonHeight = 72f;
        public const float HudTopOffset = Margin;

        // ---------- Нижний левый угол: GEN ----------
        public const float GenButtonSize = 72f;
        public const float GenBottomOffset = Margin;
    }
}
