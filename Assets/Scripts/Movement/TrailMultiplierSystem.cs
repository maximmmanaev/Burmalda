using System;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Система множителя добычи (PRD 4.3, issue #11): «Множитель растёт по
    /// кривой в зависимости от количества уникальных собранных плит трейла,
    /// с бонусом за не-прямолинейные пути». Реагирует на
    /// <see cref="GridTraceTrail.Advanced"/> — срабатывает только на
    /// по-настоящему новых плитах (уникальных, #61), не на повторных шагах
    /// по уже пройденным — ровно то, что требует формулировка PRD
    /// («уникальных собранных»).
    ///
    /// Порт формулы из legacy/burmolda_demo.html: <c>effLen = trail.length +
    /// floor(turnCount * multiplierSpeedBonus())</c>, <c>multi = getM(effLen)</c>.
    /// Бонус за смену направления пути (talisman-множитель к скорости роста
    /// turnCount, "t2" в прототипе) здесь не применяется — соответствующего
    /// Талисмана в проекте ещё нет (Спринт 5); эквивалентно множителю ×1.
    /// </summary>
    public sealed class TrailMultiplierSystem : IDisposable
    {
        private readonly GridTraceTrail _trail;
        private int? _lastDirectionKey;
        private bool _disposed;

        public TrailMultiplierSystem(GridTraceTrail trail)
        {
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _trail.Advanced += OnAdvanced;

            RecomputeMultiplier();
        }

        /// <summary>
        /// Число смен направления пути (PRD 4.3: "бонус за не-прямолинейные
        /// пути"). Первый ход никогда не считается поворотом — не с чем
        /// сравнивать направление (см. legacy: lastDir изначально null).
        /// </summary>
        public int TurnCount { get; private set; }

        /// <summary>Текущий множитель добычи — <see cref="MultiplierCurve.GetMultiplier"/> от эффективной длины трейла.</summary>
        public int CurrentMultiplier { get; private set; }

        /// <summary>Срабатывает, когда <see cref="CurrentMultiplier"/> реально меняется (не на каждый шаг — только на переходе между "полками" кривой).</summary>
        public event Action<int> MultiplierChanged;

        /// <summary>Отписывается от трейла. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.Advanced -= OnAdvanced;
            _disposed = true;
        }

        private void OnAdvanced(GridCoordinate coordinate)
        {
            UpdateTurnCount();
            RecomputeMultiplier();
        }

        private void UpdateTurnCount()
        {
            var path = _trail.Path;
            if (path.Count < 2) return; // нужны минимум две уникальные плиты, чтобы определить направление шага

            var directionKey = DirectionKey(path[path.Count - 2], path[path.Count - 1]);
            if (_lastDirectionKey.HasValue && directionKey != _lastDirectionKey.Value)
                TurnCount++;

            _lastDirectionKey = directionKey;
        }

        // Кодирует направление шага одним числом (буквально как в
        // legacy/burmolda_demo.html: "dir=(r-lr)*100+(c-lc)") — соседние
        // координаты (см. GridCoordinate.IsAdjacentTo) дают дельты в [-1,1],
        // кодировка однозначна.
        private static int DirectionKey(GridCoordinate from, GridCoordinate to) =>
            (to.Row - from.Row) * 100 + (to.Column - from.Column);

        private void RecomputeMultiplier()
        {
            var effectiveLength = _trail.Path.Count + TurnCount;
            var newMultiplier = MultiplierCurve.GetMultiplier(effectiveLength);
            if (newMultiplier == CurrentMultiplier) return;

            CurrentMultiplier = newMultiplier;
            MultiplierChanged?.Invoke(newMultiplier);
        }
    }
}
