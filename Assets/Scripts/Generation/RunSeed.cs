using System;

namespace Burmalda.Generation
{
    /// <summary>
    /// Собственный seeded-RNG для сегментной генерации (PRD v7 §21, issue
    /// #78) — независимый от глобального <c>UnityEngine.Random</c>, иначе
    /// воспроизводимость (лидерборды, баг-репорты, общий seed Испытаний)
    /// сломается любым сторонним вызовом <c>UnityEngine.Random</c> где-то ещё
    /// в проекте. Оборачивает <see cref="System.Random"/> — тот же seed даёт
    /// ту же последовательность в пределах одного запуска рантайма Unity.
    /// </summary>
    public sealed class RunSeed
    {
        private readonly Random _random;

        public RunSeed(int seed)
        {
            Value = seed;
            _random = new Random(seed);
        }

        /// <summary>Исходное значение seed, с которым создан этот генератор.</summary>
        public int Value { get; }

        /// <summary>Следующее случайное значение в [0, 1) — по аналогии с <c>UnityEngine.Random.value</c>.</summary>
        public float NextFloat01() => (float)_random.NextDouble();

        /// <summary>Следующее случайное целое в [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
        public int NextInt(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

        /// <summary>
        /// Детерминированный seed для Испытания Шахты (PRD v7 §21: "seed,
        /// общий для всех игроков (f(дата, ярус))") — та же дата и Ярус
        /// всегда дают тот же seed, независимо от машины/сессии. Время суток
        /// не учитывается — Испытание общее на весь день.
        /// </summary>
        public static int ComputeTrialSeed(DateTime date, int tier)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + date.Year;
                hash = hash * 31 + date.Month;
                hash = hash * 31 + date.Day;
                hash = hash * 31 + tier;
                return hash;
            }
        }
    }
}
