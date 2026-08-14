using System;
using System.Collections.Generic;
using System.Linq;

namespace Burmalda.Generation
{
    /// <summary>
    /// Селектор сегментов (PRD v7 §21, issue #78): "селектор выбирает
    /// сегменты по кривой сложности текущего Яруса". Сама кривая
    /// Ярус→максимальная сложность — забота вызывающей стороны
    /// (<see cref="SegmentRowProvider"/>); здесь только фильтрация каталога
    /// по верхней границе сложности и случайный выбор среди подходящих,
    /// детерминированный по <see cref="RunSeed"/>.
    /// </summary>
    public sealed class SegmentSelector
    {
        private readonly IReadOnlyList<SegmentTemplate> _catalog;
        private readonly RunSeed _seed;

        public SegmentSelector(IReadOnlyList<SegmentTemplate> catalog, RunSeed seed)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (catalog.Count == 0) throw new ArgumentException("Каталог шаблонов сегментов не может быть пустым.", nameof(catalog));
            _catalog = catalog;
            _seed = seed ?? throw new ArgumentNullException(nameof(seed));
        }

        /// <summary>
        /// Выбирает следующий шаблон среди тех, чей <see cref="SegmentTemplate.DifficultyTier"/>
        /// не превышает <paramref name="maxDifficultyTier"/>.
        /// </summary>
        public SegmentTemplate SelectNext(int maxDifficultyTier)
        {
            var candidates = _catalog.Where(t => t.DifficultyTier <= maxDifficultyTier).ToList();
            if (candidates.Count == 0)
                throw new InvalidOperationException($"Нет шаблонов сегментов с DifficultyTier <= {maxDifficultyTier} в каталоге.");

            var index = _seed.NextInt(0, candidates.Count);
            return candidates[index];
        }
    }
}
