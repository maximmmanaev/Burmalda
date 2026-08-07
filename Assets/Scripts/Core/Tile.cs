namespace Burmalda.Core
{
    /// <summary>
    /// Плита сетки тоннеля (PRD 4.1). Минимальные данные, нужные grid-trace
    /// движению; тип плиты, распад и ловушки добавят Traps и Decay поверх
    /// той же координаты в следующих спринтах.
    /// </summary>
    public sealed class Tile
    {
        public Tile(GridCoordinate coordinate)
        {
            Coordinate = coordinate;
        }

        public GridCoordinate Coordinate { get; }
    }
}
