using System;
using System.Collections.Generic;
using System.Linq;

namespace Burmalda.Generation
{
    /// <summary>
    /// Селектор сегментов (PRD v7 §21, issue #78): "селектор выбирает
    /// сегменты по кривой сложности текущего Яруса". Сама кривая
    /// Ярус→целевая сложность — забота вызывающей стороны
    /// (<see cref="SegmentRowProvider"/>); здесь — окно вокруг этой цели и
    /// случайный выбор среди подходящих, детерминированный по <see cref="RunSeed"/>.
    ///
    /// <b>Задача «партии 1 и 2 + правила отбора» (плейтест владельца,
    /// 2026-08-31):</b> раньше фильтр был кумулятивным ("тир &lt;= цель") —
    /// самые пустые тир-1 шаблоны оставались вечно доступны и на глубоких
    /// Ярусах, разбавляя, но никогда не вытесняясь. Теперь окно
    /// <see cref="TierWindowBelow"/>/<see cref="TierWindowAbove"/> вокруг
    /// цели (по умолчанию [цель, цель+1] — совпадает с примером задачи
    /// "тиры 1–2 на первом Ярусе") — лёгкие раскладки естественно выходят
    /// из ротации по мере роста цели, тир 1 не сопровождает игрока до конца
    /// забега. Плюс не повторяем последний применённый шаблон подряд (см.
    /// <see cref="SelectNext"/>) — тот же диагноз, "игрок по кругу видит
    /// одни и те же пять раскладок".
    ///
    /// Оба параметра окна — mutable static, выведены на
    /// <see cref="DebugVisuals.TrapDensityDebugPanel"/> рядом с плотностью
    /// генерации — точные правила окна прямо названы в задаче предметом
    /// баланса, не догадкой агента.
    /// </summary>
    public sealed class SegmentSelector
    {
        /// <summary>Сколько тиров НИЖЕ цели остаются в окне отбора. Стартовое значение — задачей не запрошено, 0 (окно не расширяется вниз).</summary>
        public static int TierWindowBelow = 0;

        /// <summary>Сколько тиров ВЫШЕ цели остаются в окне отбора. Стартовое значение 1 — даёт [цель, цель+1], пример из задачи "тиры 1–2 на первом Ярусе".</summary>
        public static int TierWindowAbove = 1;

        private readonly IReadOnlyList<SegmentTemplate> _catalog;
        private readonly RunSeed _seed;
        private SegmentTemplate _lastSelected;

        public SegmentSelector(IReadOnlyList<SegmentTemplate> catalog, RunSeed seed)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (catalog.Count == 0) throw new ArgumentException("Каталог шаблонов сегментов не может быть пустым.", nameof(catalog));
            _catalog = catalog;
            _seed = seed ?? throw new ArgumentNullException(nameof(seed));
        }

        /// <summary>
        /// Выбирает следующий шаблон среди тех, чей <see cref="SegmentTemplate.DifficultyTier"/>
        /// попадает в окно [<paramref name="targetDifficultyTier"/> - <see cref="TierWindowBelow"/>,
        /// <paramref name="targetDifficultyTier"/> + <see cref="TierWindowAbove"/>], зажатое в
        /// [<see cref="SegmentTemplate.MinDifficultyTier"/>, <see cref="SegmentTemplate.MaxDifficultyTier"/>].
        /// Не повторяет последний возвращённый этим методом шаблон, если в окне
        /// есть другие кандидаты — если единственный кандидат и есть последний,
        /// повторяет (повтор лучше падения с исключением).
        /// </summary>
        public SegmentTemplate SelectNext(int targetDifficultyTier)
        {
            var minTier = Math.Max(SegmentTemplate.MinDifficultyTier, targetDifficultyTier - TierWindowBelow);
            var maxTier = Math.Min(SegmentTemplate.MaxDifficultyTier, targetDifficultyTier + TierWindowAbove);

            var candidates = _catalog.Where(t => t.DifficultyTier >= minTier && t.DifficultyTier <= maxTier).ToList();
            if (candidates.Count == 0)
                throw new InvalidOperationException($"Нет шаблонов сегментов с DifficultyTier в [{minTier}, {maxTier}] в каталоге.");

            var pool = _lastSelected != null
                ? candidates.Where(t => t != _lastSelected).ToList()
                : candidates;
            if (pool.Count == 0) pool = candidates; // единственный кандидат — он же последний; повтор неизбежен

            var index = _seed.NextInt(0, pool.Count);
            var selected = pool[index];
            _lastSelected = selected;
            return selected;
        }

        /// <summary>Шаблон выбран по правилам Яруса (контекст, шаблон) — для симуляций и отладки.</summary>
        public event Action<SegmentSelectionContext, SegmentTemplate> Selected;

        /// <summary>Окно ритма: «не больше 1 передышки на 3 сегмента» — смотрим два последних.</summary>
        public const int RhythmWindow = 2;

        /// <summary>Шанс передышки, когда она разрешена и сегмент не «лёгкий» и не перед ориентиром, 1 из N [CONFIG, черновик].</summary>
        public const int FreeBreatherOneIn = 3;

        private const string MirrorSuffix = SegmentTemplate.MirrorSuffix;
        private readonly Queue<bool> _recentBreather = new Queue<bool>();

        /// <summary>
        /// Выбор по правилам Яруса (спринт v10-2): допуск шаблона — <see cref="YarusTemplateRules.IsEligible"/>;
        /// ритм «передышка / напряжение» (<see cref="TemplateRouteMetrics.IsTension"/>): после Алтаря — передышка,
        /// дальше не больше 1 передышки на 3 сегмента, последние сегменты перед Алтарём/Святилищем — только
        /// напряжение (<see cref="SegmentSelectionContext.TensionOnly"/>). Если напряжения в полосе нет —
        /// лучший по крюку из допустимых. Два одинаковых шаблона подряд (зеркало считается тем же) нельзя,
        /// пока есть другие. Пустой пул — исключение.
        /// </summary>
        public SegmentTemplate SelectNext(SegmentSelectionContext context)
        {
            var pool = _catalog.Where(t => YarusTemplateRules.IsEligible(t, context.Yarus, context.AllowTraining)).ToList();
            if (pool.Count == 0)
                throw new InvalidOperationException($"Нет допустимых шаблонов для Яруса {context.Yarus} в каталоге.");

            var recentBreather = _recentBreather.Any(x => x);
            bool wantBreather;
            if (context.TensionOnly) wantBreather = false;
            else if (context.Easy) wantBreather = true;
            else wantBreather = !recentBreather && _seed.NextInt(0, FreeBreatherOneIn) == 0;

            List<SegmentTemplate> classPool;
            if (wantBreather)
            {
                classPool = pool.Where(t => !TemplateRouteMetrics.IsTension(t)).ToList();
                if (classPool.Count == 0) classPool = pool;
            }
            else
            {
                classPool = pool.Where(TemplateRouteMetrics.IsTension).ToList();
                if (classPool.Count == 0)
                {
                    var best = pool.Max(TemplateRouteMetrics.Detour);
                    classPool = pool.Where(t => TemplateRouteMetrics.Detour(t) == best).ToList();
                }
            }

            var minTier = classPool.Min(t => t.DifficultyTier);
            var tierCeiling = context.Easy ? minTier : (context.SectionProgress < 0.5 ? minTier + 1 : SegmentTemplate.MaxDifficultyTier);
            var byTier = classPool.Where(t => t.DifficultyTier <= tierCeiling || YarusTemplateRules.IsTraining(t)).ToList();
            if (byTier.Count > 0) classPool = byTier;

            var lastBase = _lastSelected == null ? null : BaseName(_lastSelected.Name);
            var withoutRepeat = classPool.Where(t => BaseName(t.Name) != lastBase).ToList();
            if (withoutRepeat.Count > 0) classPool = withoutRepeat;

            var selected = classPool[_seed.NextInt(0, classPool.Count)];
            _lastSelected = selected;
            _recentBreather.Enqueue(!TemplateRouteMetrics.IsTension(selected));
            while (_recentBreather.Count > RhythmWindow) _recentBreather.Dequeue();
            Selected?.Invoke(context, selected);
            return selected;
        }

        private static string BaseName(string name) =>
            name.EndsWith(MirrorSuffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - MirrorSuffix.Length) : name;
    }
}

namespace Burmalda.Generation
{
    /// <summary>Контекст выбора шаблона на Ярусе (спринт v10-2).</summary>
    public readonly struct SegmentSelectionContext
    {
        public SegmentSelectionContext(int yarus, bool easy, double sectionProgress, bool allowTraining, bool tensionOnly = false)
        {
            Yarus = yarus;
            Easy = easy;
            SectionProgress = sectionProgress;
            AllowTraining = allowTraining;
            TensionOnly = tensionOnly;
        }

        public int Yarus { get; }

        /// <summary>Первый сегмент сразу после Алтаря / начала Яруса — полегче.</summary>
        public bool Easy { get; }

        /// <summary>0..1: доля пути от последнего Алтаря до следующего Алтаря/Святилища.</summary>
        public double SectionProgress { get; }

        /// <summary>Можно ли выбрать «пустой» обучающий шаблон (первая треть Яруса 1, ещё не было).</summary>
        public bool AllowTraining { get; }

        /// <summary>Последние сегменты перед Алтарём/Святилищем — только «напряжение».</summary>
        public bool TensionOnly { get; }
    }
}
