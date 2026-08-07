using System;
using System.Collections.Generic;

namespace Burmalda.Core
{
    /// <summary>
    /// Сетка плит тоннеля (PRD 4.1): фиксированная ширина, ряды плит
    /// материализуются по мере продвижения игрока вперёд.
    /// </summary>
    public sealed class TunnelGrid
    {
        private readonly Dictionary<GridCoordinate, Tile> _tiles = new Dictionary<GridCoordinate, Tile>();

        public TunnelGrid(int width)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Ширина сетки должна быть положительной.");
            Width = width;
        }

        /// <summary>Ширина тоннеля в плитах (число столбцов).</summary>
        public int Width { get; }

        /// <summary>
        /// Срабатывает, когда <see cref="GetOrCreateTile"/> материализует новую
        /// плиту (не срабатывает при повторном обращении к уже существующей).
        /// Используется, например, debug-визуалом (#58) для создания геометрии.
        /// </summary>
        public event Action<Tile> TileMaterialized;

        /// <summary>Координата принадлежит сетке: столбец в пределах ширины, ряд не отрицательный.</summary>
        public bool Contains(GridCoordinate coordinate) =>
            coordinate.Row >= 0 && coordinate.Column >= 0 && coordinate.Column < Width;

        /// <summary>Возвращает существующую плиту по координате или создаёт новую.</summary>
        public Tile GetOrCreateTile(GridCoordinate coordinate)
        {
            if (!Contains(coordinate))
                throw new ArgumentOutOfRangeException(nameof(coordinate), coordinate, "Координата вне сетки тоннеля.");

            if (!_tiles.TryGetValue(coordinate, out var tile))
            {
                tile = new Tile(coordinate);
                _tiles[coordinate] = tile;
                TileMaterialized?.Invoke(tile);
            }

            return tile;
        }

        /// <summary>
        /// Соседние плиты для позиции — 8-направленно (PRD 4.1: "тянет палец
        /// по соседним плитам"), отфильтрованные по границам сетки.
        /// </summary>
        public IEnumerable<GridCoordinate> GetNeighbors(GridCoordinate origin)
        {
            for (var deltaRow = -1; deltaRow <= 1; deltaRow++)
            {
                for (var deltaColumn = -1; deltaColumn <= 1; deltaColumn++)
                {
                    if (deltaRow == 0 && deltaColumn == 0) continue;

                    var candidate = new GridCoordinate(origin.Row + deltaRow, origin.Column + deltaColumn);
                    if (Contains(candidate)) yield return candidate;
                }
            }
        }
    }
}
