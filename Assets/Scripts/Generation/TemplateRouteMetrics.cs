using System;
using System.Collections.Generic;

namespace Burmalda.Generation
{
    /// <summary>
    /// Геометрия шаблона для давления сложности (PRD v10 §9, спринт v10-2): длина кратчайшего
    /// пути вход→выход без триггеров и с ними, «прямая» по столбцу, набор ловушек шаблона.
    /// Соседство — 8-направленное, как в <see cref="SegmentReachabilityValidator"/> и в игре.
    /// Статичная Лава непроходима в обоих путях (шаг на неё — смерть), триггеры — проходимы
    /// только в пути «с ловушками».
    /// </summary>
    public static class TemplateRouteMetrics
    {
        /// <summary>Триггеры и Лава: всё, что путь «без ловушек» обходит.</summary>
        public static bool IsTrap(SegmentTileType type) => type switch
        {
            SegmentTileType.Lava => true,
            SegmentTileType.ArrowWaveTrigger => true,
            SegmentTileType.BombTrigger => true,
            SegmentTileType.BladeTactTrigger => true,
            SegmentTileType.FallingRockTrigger => true,
            SegmentTileType.MovingWallBothTrigger => true,
            SegmentTileType.MovingWallLeftTrigger => true,
            SegmentTileType.MovingWallRightTrigger => true,
            _ => false,
        };

        /// <summary>Кратчайший путь (в шагах) от любой плиты входа до любой плиты выхода, не касаясь ловушек; -1, если пути нет.</summary>
        public static int SafePathSteps(SegmentTemplate template) => ShortestSteps(template, SafePassable, -1);

        /// <summary>Кратчайший путь, если триггеры можно задеть (Лава по-прежнему непроходима) — «соблазн»; -1, если пути нет.</summary>
        public static int FastPathSteps(SegmentTemplate template) => ShortestSteps(template, FastPassable, -1);

        /// <summary>
        /// Средняя по плитам входа длина безопасного пути до выхода, в шагах (игрок входит в сегмент в
        /// том столбце, где вышел из предыдущего, — любой из них равновероятен); плиты входа без
        /// безопасного пути не учитываются; -1, если нет ни одной.
        /// </summary>
        public static double AverageSafePathSteps(SegmentTemplate template)
        {
            var total = 0;
            var count = 0;
            for (var c = 0; c < template.Width; c++)
            {
                var steps = ShortestSteps(template, SafePassable, c);
                if (steps < 0) continue;
                total += steps;
                count++;
            }

            return count == 0 ? -1 : (double)total / count;
        }

        /// <summary>Коэффициент крюка: средняя длина безопасного пути / число рядов; 0, если безопасного пути нет.</summary>
        public static double DetourRatio(SegmentTemplate template)
        {
            var average = AverageSafePathSteps(template);
            return average < 0 ? 0 : average / template.RowCount;
        }

        /// <summary>Есть ли столбец, целиком состоящий из открытых плит без ловушек (прямая от входа до выхода).</summary>
        public static bool HasStraightColumn(SegmentTemplate template)
        {
            for (var c = 0; c < template.Width; c++)
            {
                var straight = true;
                for (var r = 0; r < template.RowCount && straight; r++)
                {
                    var type = template.TileAt(r, c);
                    straight = IsOpen(type) && !IsTrap(type);
                }

                if (straight) return true;
            }

            return false;
        }

        /// <summary>
        /// Есть соблазн: хотя бы для одной плиты входа путь через триггер короче безопасного не меньше чем на
        /// <paramref name="minSavedSteps"/> шагов.
        /// </summary>
        public static bool HasTemptation(SegmentTemplate template, int minSavedSteps = 2)
        {
            for (var c = 0; c < template.Width; c++)
            {
                var safe = ShortestSteps(template, SafePassable, c);
                var fast = ShortestSteps(template, FastPassable, c);
                if (safe >= 0 && fast >= 0 && safe - fast >= minSavedSteps) return true;
            }

            return false;
        }

        /// <summary>Типы ловушек, присутствующие в шаблоне (для открытия по Ярусам).</summary>
        public static IReadOnlyCollection<SegmentTileType> TrapTypes(SegmentTemplate template)
        {
            var result = new HashSet<SegmentTileType>();
            for (var r = 0; r < template.RowCount; r++)
                for (var c = 0; c < template.Width; c++)
                    if (IsTrap(template.TileAt(r, c))) result.Add(template.TileAt(r, c));
            return result;
        }

        private static bool SafePassable(SegmentTemplate t, int r, int c) => IsOpen(t.TileAt(r, c)) && !IsTrap(t.TileAt(r, c));

        private static bool FastPassable(SegmentTemplate t, int r, int c) => IsOpen(t.TileAt(r, c)) && t.TileAt(r, c) != SegmentTileType.Lava;

        private static bool IsOpen(SegmentTileType type) =>
            type != SegmentTileType.Blocked && type != SegmentTileType.LeverGate;

        // entryColumn < 0 — со всех плит входа сразу (минимум), иначе только с указанной.
        private static int ShortestSteps(SegmentTemplate template, Func<SegmentTemplate, int, int, bool> passable, int entryColumn)
        {
            var distance = new int[template.RowCount, template.Width];
            var queue = new Queue<(int row, int column)>();
            for (var r = 0; r < template.RowCount; r++)
                for (var c = 0; c < template.Width; c++)
                    distance[r, c] = -1;
            for (var c = 0; c < template.Width; c++)
            {
                if (entryColumn >= 0 && c != entryColumn) continue;
                if (!passable(template, 0, c)) continue;
                distance[0, c] = 0;
                queue.Enqueue((0, c));
            }

            while (queue.Count > 0)
            {
                var (row, column) = queue.Dequeue();
                if (row == template.RowCount - 1) return distance[row, column];
                for (var dr = -1; dr <= 1; dr++)
                for (var dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    var nr = row + dr;
                    var nc = column + dc;
                    if (nr < 0 || nr >= template.RowCount || nc < 0 || nc >= template.Width) continue;
                    if (distance[nr, nc] != -1 || !passable(template, nr, nc)) continue;
                    distance[nr, nc] = distance[row, column] + 1;
                    queue.Enqueue((nr, nc));
                }
            }

            return -1;
        }
    }
}
