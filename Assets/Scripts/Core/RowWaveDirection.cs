namespace Burmalda.Core
{
    /// <summary>
    /// Направление волны ловушки «Стрела» вдоль ряда (docs/wiki/traps.md,
    /// issue #213): «направление задаётся при генерации» — задаётся ОДИН
    /// раз на плите-триггере (<see cref="Tile.MarkArrowWaveTrigger"/>), не
    /// выбирается заново каждый раз, когда триггер срабатывает.
    /// </summary>
    public enum RowWaveDirection
    {
        /// <summary>Столбец 0 → <c>TunnelGrid.Width - 1</c> (владелец: «слева направо, столбцы 1→5»).</summary>
        LeftToRight,

        /// <summary>Столбец <c>TunnelGrid.Width - 1</c> → 0 (владелец: «справа налево, 5→1»).</summary>
        RightToLeft
    }
}
