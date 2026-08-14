namespace Burmalda.Artifacts
{
    /// <summary>
    /// Тип активной способности Тотема (PRD раздел 12), вызываемой двойным
    /// тапом. Сама активация/тайминг/эффект каждой способности — вне
    /// скоупа issue #16 (держатель), реализуется в issue #28 (Спринт 9).
    /// </summary>
    public enum TotemAbilityType
    {
        /// <summary>Резкий точный бросок через несколько плит разом.</summary>
        Dash,

        /// <summary>Проход сквозь заблокированную плиту.</summary>
        Breach,

        /// <summary>Трейл временно не распадается.</summary>
        Invulnerability,

        /// <summary>Одно бесплатное спасение за забег при провале возврата (разблокируется Руной).</summary>
        SecondWind
    }
}
