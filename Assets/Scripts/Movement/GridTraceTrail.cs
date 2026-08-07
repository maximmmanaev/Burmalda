using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Трейл grid-trace движения (PRD 4.1): игрок тянет палец по соседним
    /// плитам, продвигаясь вперёд по тоннелю. Ход валиден на плиту, соседнюю
    /// текущей позиции и ещё не пройденную трейлом — повторный шаг на уже
    /// пройденную плиту (в том числе прямой возврат назад) невалиден.
    /// </summary>
    public sealed class GridTraceTrail
    {
        private readonly TunnelGrid _grid;
        private readonly List<GridCoordinate> _path = new List<GridCoordinate>();
        private readonly HashSet<GridCoordinate> _visited = new HashSet<GridCoordinate>();

        public GridTraceTrail(TunnelGrid grid, GridCoordinate startCoordinate)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (!grid.Contains(startCoordinate))
                throw new ArgumentOutOfRangeException(nameof(startCoordinate), startCoordinate, "Стартовая координата вне сетки тоннеля.");

            grid.GetOrCreateTile(startCoordinate);
            _path.Add(startCoordinate);
            _visited.Add(startCoordinate);
        }

        /// <summary>Пройденные плиты трейла по порядку, от старта до текущей позиции.</summary>
        public IReadOnlyList<GridCoordinate> Path => _path;

        /// <summary>Текущая позиция игрока — последняя плита трейла.</summary>
        public GridCoordinate CurrentPosition => _path[_path.Count - 1];

        /// <summary>
        /// Ход на <paramref name="target"/> валиден, если плита в пределах
        /// сетки, соседняя текущей позиции и ещё не пройдена трейлом.
        /// </summary>
        public bool CanAdvanceTo(GridCoordinate target) =>
            _grid.Contains(target) && CurrentPosition.IsAdjacentTo(target) && !_visited.Contains(target);

        /// <summary>Продвигает трейл на <paramref name="target"/>, если ход валиден.</summary>
        public bool TryAdvanceTo(GridCoordinate target)
        {
            if (!CanAdvanceTo(target)) return false;

            _grid.GetOrCreateTile(target);
            _path.Add(target);
            _visited.Add(target);
            return true;
        }
    }
}
