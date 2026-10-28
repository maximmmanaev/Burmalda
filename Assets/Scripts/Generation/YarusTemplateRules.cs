using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Generation
{
    /// <summary>
    /// Допуск шаблона к выбору на Ярусе (PRD v10 §8.2, §9, спринт v10-2): ловушки открываются по
    /// Ярусам, «пустые» шаблоны — только обучение, а остальные обязаны иметь безопасный путь и не
    /// иметь прямой. Числа и списки — ЧЕРНОВЫЕ [CONFIG].
    /// </summary>
    public static class YarusTemplateRules
    {
        /// <summary>«Пустые» шаблоны обучения: только первая треть Яруса 1, не больше одного на Ярус.</summary>
        public static readonly HashSet<string> TrainingTemplateNames = new HashSet<string>
        {
            "пустой-перегон", "первая-стена", "длинный-перегон", "пустой-обман",
        };

        /// <summary>Целевой средний коэффициент крюка по полосе (длина безопасного пути / число рядов) [CONFIG].</summary>
        public static double DetourTarget(int band) => band <= 1 ? 1.6 : (band == 2 ? 1.9 : 2.2);

        /// <summary>
        /// Жёсткий минимум коэффициента крюка для выбора шаблона в полосе [CONFIG]. 0 — выключен: ни один
        /// шаблон каталога (и ни один из утверждённых владельцем) пока не достигает целевых значений
        /// <see cref="DetourTarget"/>, жёсткий порог выбил бы пул целиком.
        /// </summary>
        public static double DetourFloor = 0.0;

        public static bool IsTraining(SegmentTemplate template) => TrainingTemplateNames.Contains(template.Name);

        /// <summary>Открыта ли ловушка на Ярусе: 1–3 — Камень, Давилка, Стены, Стрелы; 4–6 — + Лезвия, Бомба, Лава; 7+ — всё.</summary>
        public static bool IsTrapOpen(SegmentTileType type, int yarus)
        {
            switch (type)
            {
                case SegmentTileType.BladeTactTrigger:
                case SegmentTileType.BombTrigger:
                case SegmentTileType.Lava:
                    return yarus >= 4;
                default:
                    return true;
            }
        }

        public static bool AllTrapsOpen(SegmentTemplate template, int yarus)
        {
            foreach (var type in TemplateRouteMetrics.TrapTypes(template))
                if (!IsTrapOpen(type, yarus)) return false;
            return true;
        }

        /// <summary>
        /// Шаблон можно выбирать на Ярусе <paramref name="yarus"/>: ловушки открыты; обучающий — только если
        /// <paramref name="allowTraining"/>; остальные — есть безопасный путь, нет прямой по столбцу и
        /// коэффициент крюка не ниже <see cref="DetourFloor"/>.
        /// </summary>
        public static bool IsEligible(SegmentTemplate template, int yarus, bool allowTraining)
        {
            if (!AllTrapsOpen(template, yarus)) return false;
            if (IsTraining(template)) return allowTraining;
            if (TemplateRouteMetrics.SafePathSteps(template) < 0) return false;
            if (TemplateRouteMetrics.HasStraightColumn(template)) return false;
            return TemplateRouteMetrics.DetourRatio(template) >= DetourFloor;
        }
    }
}
