using System.Collections.Generic;

namespace Burmalda.Generation
{
    /// <summary>
    /// Проверка проходимости шаблона на этапе авторинга (PRD v7 §21, issue
    /// #78): "существует путь вход→выход, не проходящий через заблокированные
    /// плиты; проверяется на этапе авторинга, не в рантайме". Гарантия
    /// сильнее буквальной формулировки: путь должен существовать из КАЖДОЙ
    /// проходимой плиты ряда входа (row 0) — игрок может войти в сегмент в
    /// любом столбце, не только в одном заранее известном.
    ///
    /// Непроходимыми для этой проверки считаются <see cref="SegmentTileType.Blocked"/>
    /// и <see cref="SegmentTileType.LeverGate"/> (закрыта по умолчанию, issue
    /// #51 — "недоступный по основному маршруту": если единственный путь к
    /// выходу идёт через неё, основной маршрут на деле непроходим без
    /// рычага). Опасные, но не заблокированные, плиты (яма/лава/триггеры
    /// ловушек) проходимы для этой проверки — риск, а не физическая стена.
    /// </summary>
    public static class SegmentReachabilityValidator
    {
        public static bool IsTraversable(SegmentTemplate template)
        {
            var componentId = new int[template.RowCount, template.Width];
            for (var r = 0; r < template.RowCount; r++)
                for (var c = 0; c < template.Width; c++)
                    componentId[r, c] = -1;

            var nextComponentId = 0;
            for (var r = 0; r < template.RowCount; r++)
                for (var c = 0; c < template.Width; c++)
                {
                    if (!IsPassable(template, r, c)) continue;
                    if (componentId[r, c] != -1) continue;

                    FloodFill(template, componentId, r, c, nextComponentId);
                    nextComponentId++;
                }

            var exitComponents = new HashSet<int>();
            var exitRow = template.RowCount - 1;
            for (var c = 0; c < template.Width; c++)
                if (IsPassable(template, exitRow, c))
                    exitComponents.Add(componentId[exitRow, c]);

            if (exitComponents.Count == 0) return false; // выход целиком заблокирован

            var hasEntry = false;
            for (var c = 0; c < template.Width; c++)
            {
                if (!IsPassable(template, 0, c)) continue;
                hasEntry = true;
                if (!exitComponents.Contains(componentId[0, c])) return false; // эта плита входа не соединена ни с одним выходом
            }

            return hasEntry; // вход целиком заблокирован — тоже невалидно
        }

        private static bool IsPassable(SegmentTemplate template, int row, int column)
        {
            var type = template.TileAt(row, column);
            return type != SegmentTileType.Blocked && type != SegmentTileType.LeverGate;
        }

        // BFS, 8-направленное соседство — тот же принцип, что GridCoordinate.IsAdjacentTo/TunnelGrid.GetNeighbors.
        private static void FloodFill(SegmentTemplate template, int[,] componentId, int startRow, int startColumn, int id)
        {
            var queue = new Queue<(int row, int column)>();
            queue.Enqueue((startRow, startColumn));
            componentId[startRow, startColumn] = id;

            while (queue.Count > 0)
            {
                var (row, column) = queue.Dequeue();
                for (var deltaRow = -1; deltaRow <= 1; deltaRow++)
                for (var deltaColumn = -1; deltaColumn <= 1; deltaColumn++)
                {
                    if (deltaRow == 0 && deltaColumn == 0) continue;

                    var neighborRow = row + deltaRow;
                    var neighborColumn = column + deltaColumn;
                    if (neighborRow < 0 || neighborRow >= template.RowCount) continue;
                    if (neighborColumn < 0 || neighborColumn >= template.Width) continue;
                    if (componentId[neighborRow, neighborColumn] != -1) continue;
                    if (!IsPassable(template, neighborRow, neighborColumn)) continue;

                    componentId[neighborRow, neighborColumn] = id;
                    queue.Enqueue((neighborRow, neighborColumn));
                }
            }
        }
    }
}
