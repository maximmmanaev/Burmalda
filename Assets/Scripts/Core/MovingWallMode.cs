namespace Burmalda.Core
{
    /// <summary>
    /// Режим закрытия ловушки «Давилка»/«Стена слева»/«Стена справа»
    /// (BURMALDA Trap System Spec v0.1, TR-05/06/07, <c>Movement.MovingWallTrap</c>) —
    /// «для gameplay-каталога это три разные ловушки, для кода — один
    /// reusable-компонент» (владелец). Задаётся ОДИН раз на плите-триггере
    /// (<see cref="Tile.MarkMovingWallTrigger"/>), тот же приём, что
    /// <see cref="RowWaveDirection"/> у «Стрелы».
    /// </summary>
    public enum MovingWallMode
    {
        /// <summary>
        /// «Давилка» (TR-05): стены смыкаются с обоих краёв к центру
        /// симметричными парами — тот же порядок колонок, что
        /// <c>Movement.BladeTactTrapSystem.ComputeRingColumns</c> (крайняя
        /// пара → следующая пара внутрь → ... → центр), но БЕЗ обратного
        /// хода — закрытие необратимо, не такт.
        /// </summary>
        Both,

        /// <summary>«Стена слева» (TR-06): столбцы закрываются по очереди от левого края (0) к правому (Width-1).</summary>
        FromLeft,

        /// <summary>«Стена справа» (TR-07): столбцы закрываются по очереди от правого края (Width-1) к левому (0).</summary>
        FromRight
    }
}
