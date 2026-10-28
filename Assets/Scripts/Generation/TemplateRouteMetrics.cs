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
        public static int SafePathSteps(SegmentTemplate template) => ShortestSteps(template, SafePassable);

        /// <summary>Кратчайший путь, если триггеры можно задеть (Лава по-прежнему непроходима) — «соблазн»; -1, если пути нет.</summary>
        public static int FastPathSteps(SegmentTemplate template) => ShortestSteps(template, FastPassable);

        /// <summary>Минимальный крюк класса «напряжение» [CONFIG, черновик].</summary>
        public const int TensionMinDetour = 3;

        /// <summary>Минимальный соблазн класса «напряжение» [CONFIG, черновик].</summary>
        public const int TensionMinTemptation = 2;

        /// <summary>Крюк: длина кратчайшего пути без триггеров − (рядов − 1); 0, если безопасного пути нет.</summary>
        public static int Detour(SegmentTemplate template)
        {
            var safe = SafePathSteps(template);
            return safe < 0 ? 0 : safe - (template.RowCount - 1);
        }

        /// <summary>Соблазн: длина пути без триггеров − длина кратчайшего пути с триггерами; 0, если безопасного пути нет.</summary>
        public static int Temptation(SegmentTemplate template)
        {
            var safe = SafePathSteps(template);
            var fast = FastPathSteps(template);
            return safe < 0 || fast < 0 ? 0 : safe - fast;
        }

        /// <summary>«Напряжение»: крюк ≥ <see cref="TensionMinDetour"/> и соблазн ≥ <see cref="TensionMinTemptation"/>; всё остальное — «передышка».</summary>
        public static bool IsTension(SegmentTemplate template) =>
            Detour(template) >= TensionMinDetour && Temptation(template) >= TensionMinTemptation;

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

        // Со всех плит входа сразу (минимум).
        private static int ShortestSteps(SegmentTemplate template, Func<SegmentTemplate, int, int, bool> passable)
        {
            var distance = new int[template.RowCount, template.Width];
            var queue = new Queue<(int row, int column)>();
            for (var r = 0; r < template.RowCount; r++)
                for (var c = 0; c < template.Width; c++)
                    distance[r, c] = -1;
            for (var c = 0; c < template.Width; c++)
            {
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
