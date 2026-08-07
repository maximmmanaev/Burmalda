using System;

namespace Burmalda.Core
{
    /// <summary>
    /// Координата плиты в сетке тоннеля (PRD 4.1): Row — продвижение вглубь
    /// тоннеля, Column — положение поперёк тоннеля.
    /// </summary>
    public readonly struct GridCoordinate : IEquatable<GridCoordinate>
    {
        public GridCoordinate(int row, int column)
        {
            Row = row;
            Column = column;
        }

        public int Row { get; }
        public int Column { get; }

        /// <summary>
        /// Соседняя плита — любая из 8 окружающих ячеек, включая диагонали
        /// (см. исходный прототип legacy/burmolda_demo.html, adjKey).
        /// </summary>
        public bool IsAdjacentTo(GridCoordinate other)
        {
            if (Equals(other)) return false;
            return Math.Abs(Row - other.Row) <= 1 && Math.Abs(Column - other.Column) <= 1;
        }

        public bool Equals(GridCoordinate other) => Row == other.Row && Column == other.Column;
        public override bool Equals(object obj) => obj is GridCoordinate other && Equals(other);
        public override int GetHashCode() => (Row * 397) ^ Column;
        public override string ToString() => $"({Row}, {Column})";

        public static bool operator ==(GridCoordinate left, GridCoordinate right) => left.Equals(right);
        public static bool operator !=(GridCoordinate left, GridCoordinate right) => !left.Equals(right);
    }
}
