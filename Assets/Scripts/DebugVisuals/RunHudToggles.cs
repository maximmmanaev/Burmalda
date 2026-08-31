namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Тумблеры отладочного HUD забега (issue "Задача 2 — отладочный HUD") —
    /// текущее состояние, читаемое несколькими независимыми компонентами
    /// (<see cref="RunHudOverlay"/>, <see cref="TilePreviewController"/>),
    /// без протаскивания ссылки через конструкторы — тот же паттерн, что и
    /// <see cref="Movement.TrapInsight"/>. Управляются
    /// <see cref="RunHudTogglePanel"/>.
    /// </summary>
    public static class RunHudToggles
    {
        /// <summary>Числа валют/множитель/состав билда — по умолчанию ВКЛЮЧЁН (задача явно просит).</summary>
        public static bool ShowRunHud { get; set; } = true;

        /// <summary>
        /// Отладочный оверлей примериваемой плиты (координаты/% распада/тип
        /// ловушки, ранее всегда включённый снизу слева) — перенесён под
        /// свой тумблер, по умолчанию ВЫКЛЮЧЕН (задача явно просит).
        /// </summary>
        public static bool ShowTileDebugOverlay { get; set; }
    }
}
