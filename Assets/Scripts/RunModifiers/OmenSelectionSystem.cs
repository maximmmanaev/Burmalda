using System;
using System.Collections.Generic;

namespace Burmalda.RunModifiers
{
    /// <summary>
    /// Предложение Знамений перед спуском (PRD v7 §20, issue #84): "Перед
    /// спуском в Лагере предлагаются 3 Знамения (из разблокированного
    /// пула). Игрок берёт одно или уходит «Чистым спуском» — без Знамения,
    /// без штрафа". «Чистый спуск» здесь отдельным методом не выделен —
    /// это просто <see cref="RunModifiers.None"/> у вызывающей стороны,
    /// не требует отдельного API.
    ///
    /// RNG — <c>Func&lt;float&gt;</c> (0 включительно, 1 исключительно), как
    /// и <see cref="Altar.Ritual"/> (тот же контекст — выбор в Лагере/на
    /// Алтаре, не детерминированная сегментная генерация трассы, где
    /// используется <c>Generation.RunSeed</c>).
    /// </summary>
    public sealed class OmenSelectionSystem
    {
        public const int OfferedCount = 3;

        private readonly OmenPool _pool;
        private readonly Func<float> _random01;

        public OmenSelectionSystem(OmenPool pool, Func<float> random01)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _random01 = random01 ?? throw new ArgumentNullException(nameof(random01));
        }

        /// <summary>
        /// До <see cref="OfferedCount"/> случайных РАЗЛИЧНЫХ Знамений из
        /// разблокированного пула, без повторов. Меньше, если в пуле
        /// разблокировано меньше — PRD такой случай не описывает (в раннем
        /// прогрессе пул может быть почти пуст), возвращаем сколько есть,
        /// а не бросаем исключение.
        /// </summary>
        public IReadOnlyList<OmenId> OfferOmens()
        {
            var remaining = new List<OmenId>(_pool.UnlockedIds);
            var offered = new List<OmenId>();
            var count = Math.Min(OfferedCount, remaining.Count);

            for (var i = 0; i < count; i++)
            {
                var index = Math.Min((int)(_random01() * remaining.Count), remaining.Count - 1);
                offered.Add(remaining[index]);
                remaining.RemoveAt(index);
            }

            return offered;
        }
    }
}
