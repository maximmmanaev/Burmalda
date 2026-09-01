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
        /// <summary>
        /// Плоский текстовый OnGUI-оверлей <see cref="RunHudOverlay"/> —
        /// числа валют/результат возврата/состав билда/блок Комнаты Босса.
        /// Задача «HUD накладывается сам на себя» (плейтест владельца:
        /// «Кристаллы Маны: 6280» перечёркнута крупным «6280» с
        /// Canvas-счётчика <see cref="HudDesign.RunHudDesignOverlay"/>) —
        /// два оверлея рисовали дублирующие числа валют одновременно, оба
        /// default-ВКЛ. <see cref="HudDesign.RunHudDesignOverlay"/> остаётся
        /// player-facing (дизайн-система, у него нет тумблера — это не
        /// debug-инструмент), а этот — чисто отладочный (Amulets/Talismans
        /// текстом, разбивка конвертации, множитель Комнаты) — дефолт
        /// ВЫКЛЮЧЕН (было ВКЛЮЧЕН до этой задачи), включается тем же
        /// тумблером в <see cref="RunHudTogglePanel"/>, когда нужны именно
        /// эти данные текстом.
        /// </summary>
        public static bool ShowRunHud { get; set; }

        /// <summary>
        /// Отладочный оверлей примериваемой плиты (координаты/% распада/тип
        /// ловушки, ранее всегда включённый снизу слева) — перенесён под
        /// свой тумблер, по умолчанию ВЫКЛЮЧЕН (задача явно просит).
        /// </summary>
        public static bool ShowTileDebugOverlay { get; set; }

        /// <summary>
        /// Каркас Комнаты Босса (<c>HudDesign.RunHudDesignOverlay</c>,
        /// временный экран для скриншотов на ревью, не связан с игровыми
        /// данными) — задача «на игровом поле не должно быть ни одной
        /// отладочной кнопки»: раньше кнопка "Preview Boss Room" висела
        /// посреди экрана поверх зоны тапа хода. Теперь тумблер здесь,
        /// дефолт ВЫКЛЮЧЕН.
        /// </summary>
        public static bool ShowBossRoomPreview { get; set; }
    }
}
