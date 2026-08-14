using System;
using System.Collections.Generic;
using System.Linq;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Вычисление активных Созвучий (PRD v7 §6.1, issue #79): "одновременно
    /// активны не более двух Созвучий (приоритет — по порядку получения
    /// артефактов)". Чистая функция от последовательности наборов тегов
    /// артефактов активного билда, в порядке получения — не хранит
    /// состояние сама, вызывающая сторона (будущий <c>RunArtifactLoadout</c>-
    /// потребитель) передаёт актуальный список на момент запроса.
    ///
    /// <see cref="ResonanceType.Unity"/> — условие "прокачаны Рунами одной
    /// линии" структурно не связано с ПОРЯДКОМ ПОЛУЧЕНИЯ артефактов
    /// (прокачка Рунами — отдельное действие в Алтаре, Спринт 6), поэтому у
    /// него нет естественного "индекса получения". Решение агента: считать
    /// его выполненным с индексом 0 (высший приоритет), если условие
    /// истинно — простое и предсказуемое поведение, а не попытка
    /// притянуть order-based приоритет туда, где PRD его не описывает.
    /// </summary>
    public static class ResonanceCalculator
    {
        public const int VeinRequiredCount = 3;
        public const int KeyBondRequiredCount = 2;
        public const int WardRequiredCount = 2;
        public const int MaxActiveResonances = 2;

        public static IReadOnlyList<ResonanceType> Compute(IReadOnlyList<IReadOnlyList<ArtifactTag>> acquiredArtifactTags, bool unityConditionMet)
        {
            if (acquiredArtifactTags == null) throw new ArgumentNullException(nameof(acquiredArtifactTags));

            var candidates = new List<(ResonanceType type, int satisfiedAtIndex)>();

            var veinIndex = IndexWhenCountReaches(acquiredArtifactTags, ArtifactTag.Mana, VeinRequiredCount);
            if (veinIndex.HasValue) candidates.Add((ResonanceType.Vein, veinIndex.Value));

            var keyBondIndex = IndexWhenCountReaches(acquiredArtifactTags, ArtifactTag.Keys, KeyBondRequiredCount);
            if (keyBondIndex.HasValue) candidates.Add((ResonanceType.KeyBond, keyBondIndex.Value));

            var wardIndex = IndexWhenCountReaches(acquiredArtifactTags, ArtifactTag.Defense, WardRequiredCount);
            if (wardIndex.HasValue) candidates.Add((ResonanceType.Ward, wardIndex.Value));

            var greedIndex = FirstIndexWithTag(acquiredArtifactTags, ArtifactTag.Greed);
            var movementIndex = FirstIndexWithTag(acquiredArtifactTags, ArtifactTag.Movement);
            if (greedIndex.HasValue && movementIndex.HasValue)
                candidates.Add((ResonanceType.StingyMove, Math.Max(greedIndex.Value, movementIndex.Value)));

            if (unityConditionMet)
                candidates.Add((ResonanceType.Unity, 0));

            return candidates
                .OrderBy(c => c.satisfiedAtIndex) // OrderBy стабилен — при равных индексах порядок добавления выше сохраняется
                .Take(MaxActiveResonances)
                .Select(c => c.type)
                .ToList();
        }

        private static int? IndexWhenCountReaches(IReadOnlyList<IReadOnlyList<ArtifactTag>> acquired, ArtifactTag tag, int requiredCount)
        {
            var count = 0;
            for (var i = 0; i < acquired.Count; i++)
            {
                if (!acquired[i].Contains(tag)) continue;
                count++;
                if (count >= requiredCount) return i;
            }
            return null;
        }

        private static int? FirstIndexWithTag(IReadOnlyList<IReadOnlyList<ArtifactTag>> acquired, ArtifactTag tag)
        {
            for (var i = 0; i < acquired.Count; i++)
                if (acquired[i].Contains(tag)) return i;
            return null;
        }
    }
}
