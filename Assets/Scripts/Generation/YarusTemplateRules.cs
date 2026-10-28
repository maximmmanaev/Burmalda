using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Generation
{
    /// <summary>
    /// Допуск шаблона к выбору на Ярусе (PRD v10 §8.2, §9, спринт v10-2): ловушки открываются по
    /// Ярусам, «пустые» шаблоны — только обучение, а остальные обязаны иметь безопасный путь. Числа и списки — ЧЕРНОВЫЕ [CONFIG].
    /// </summary>
    public static class YarusTemplateRules
    {
        /// <summary>«Пустые» шаблоны обучения: только первая треть Яруса 1, не больше одного на Ярус.</summary>
        public static readonly HashSet<string> TrainingTemplateNames = new HashSet<string>
        {
            "пустой-перегон", "первая-стена", "длинный-перегон", "пустой-обман",
        };

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
        /// <paramref name="allowTraining"/>; остальные — есть безопасный путь. Почти прямые шаблоны не
        /// отбрасываются: это «передышки» (<see cref="TemplateRouteMetrics.IsTension"/>), ритм задаёт <see cref="SegmentSelector"/>.
        /// </summary>
        public static bool IsEligible(SegmentTemplate template, int yarus, bool allowTraining)
        {
            if (!AllTrapsOpen(template, yarus)) return false;
            if (IsTraining(template)) return allowTraining;
            return TemplateRouteMetrics.SafePathSteps(template) >= 0;
        }
    }
}
