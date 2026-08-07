using System;
using Burmalda.Core;
using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Перевод мировой точки (например, точки пересечения луча от пальца с
    /// полом тоннеля) в координату сетки плит и обратно. Ряд (Row) —
    /// продвижение вперёд по тоннелю (мировая ось Z), столбец (Column) —
    /// положение поперёк тоннеля (мировая ось X), см. PRD 4.1.
    /// </summary>
    public readonly struct WorldGridProjection
    {
        public WorldGridProjection(float tileSize, int width)
        {
            if (tileSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(tileSize), tileSize, "Размер плиты должен быть положительным.");
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Ширина сетки должна быть положительной.");

            TileSize = tileSize;
            Width = width;
        }

        public float TileSize { get; }
        public int Width { get; }

        public GridCoordinate ToGridCoordinate(Vector3 worldPosition)
        {
            var row = Mathf.FloorToInt(worldPosition.z / TileSize);
            var column = Mathf.FloorToInt(worldPosition.x / TileSize + Width * 0.5f);
            return new GridCoordinate(row, column);
        }

        public Vector3 ToWorldPosition(GridCoordinate coordinate)
        {
            var x = (coordinate.Column - Width * 0.5f + 0.5f) * TileSize;
            var z = (coordinate.Row + 0.5f) * TileSize;
            return new Vector3(x, 0f, z);
        }
    }
}
