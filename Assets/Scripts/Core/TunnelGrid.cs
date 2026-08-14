using System;
using System.Collections.Generic;
using System.Linq;

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
        /// Возвращает уже материализованную плиту по координате без побочных
        /// эффектов — в отличие от <see cref="GetOrCreateTile"/>, ничего не
        /// создаёт и не поднимает <see cref="TileMaterialized"/>. Нужен
        /// проверкам вроде препятствий (#9): плита, на которую ещё никто не
        /// смотрел, по определению не может быть заблокирована.
        /// </summary>
        public bool TryGetTile(GridCoordinate coordinate, out Tile tile) => _tiles.TryGetValue(coordinate, out tile);

        /// <summary>
        /// Снимок уже материализованных плит на текущий момент — прямой
        /// доступ к состоянию, а не только через <see cref="TileMaterialized"/>.
        /// Нужен, чтобы догнать пропущенные события (2026-08-14, реальный
        /// баг, не гипотеза): <see cref="TileMaterialized"/> — идемпотентное
        /// событие (стреляет один раз за координату навсегда, см.
        /// <see cref="GetOrCreateTile"/>), а порядок <c>Update()</c> между
        /// независимыми MonoBehaviour-компонентами Unity не гарантирует —
        /// если что-то материализует плиты РАНЬШЕ, чем подписчик успел
        /// подписаться (см. <c>Burmalda.DebugVisuals.TunnelDebugVisual</c>),
        /// те события улетают в пустоту без второго шанса. Возвращает
        /// снимок (не live view) — безопасно перечислять, даже если
        /// вызывающий код сам параллельно вызывает <see cref="GetOrCreateTile"/>.
        /// </summary>
        public IReadOnlyCollection<Tile> MaterializedTiles => _tiles.Values.ToArray();

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
