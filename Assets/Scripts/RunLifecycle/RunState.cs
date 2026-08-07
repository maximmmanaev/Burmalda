using System;
using Burmalda.Core;
using Burmalda.Decay;
using Burmalda.Movement;

namespace Burmalda.RunLifecycle
{
    /// <summary>
    /// Жизнь/смерть текущего забега (PRD 9) — временная упрощённая замена
    /// d20-испытания: по прямому запросу владельца продукта, пока в проекте
    /// нет системы d20 (Спринт 7, см. terminology.md — <c>D20Trial</c>/
    /// <c>D20Outcome</c> зарезервированы под неё), смерть наступает сразу,
    /// без броска. Слушает два источника смерти — попытку шагнуть на
    /// смертельную ловушку (<see cref="GridTraceTrail.LethalTrapTriggered"/>,
    /// PRD 4.2) и обрушение плиты под ногами игрока
    /// (<see cref="TrailDecaySystem.TileDestroyed"/> для текущей позиции) —
    /// по аналогии с legacy/burmolda_demo.html, attemptDeath(), вызываемым из
    /// tryAct() (ловушка) и updateDecay() (плита под ногами).
    /// Ничего не знает про рестарт — при новом забеге создаётся новый
    /// экземпляр (мир перегенерируется заново, см. GridTraceInputController.Restart).
    /// </summary>
    public sealed class RunState : IDisposable
    {
        private readonly GridTraceTrail _trail;
        private readonly TrailDecaySystem _decay;
        private bool _disposed;

        public RunState(GridTraceTrail trail, TrailDecaySystem decay)
        {
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _decay = decay ?? throw new ArgumentNullException(nameof(decay));
            IsAlive = true;

            _trail.LethalTrapTriggered += OnLethalTrapTriggered;
            _decay.TileDestroyed += OnTileDestroyed;
        }

        /// <summary>Жив ли игрок в этом забеге. Становится false ровно один раз — далее не меняется.</summary>
        public bool IsAlive { get; private set; }

        /// <summary>Срабатывает один раз, когда игрок умирает — с человекочитаемой причиной для будущего UI.</summary>
        public event Action<string> Died;

        /// <summary>Отписывается от трейла и распада. Вызывать при завершении забега/пересборке на рестарте.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.LethalTrapTriggered -= OnLethalTrapTriggered;
            _decay.TileDestroyed -= OnTileDestroyed;
            _disposed = true;
        }

        private void OnLethalTrapTriggered(GridCoordinate coordinate, LethalTrapType trapType)
        {
            // legacy/burmolda_demo.html, tryAct(): attemptDeath('Провалился в яму'/'Сработала ловушка', ...)
            Die(trapType == LethalTrapType.Pit ? "Провалился в яму" : "Сгорел в лаве");
        }

        private void OnTileDestroyed(GridCoordinate coordinate)
        {
            // legacy/burmolda_demo.html, updateDecay(): проверяется только ПОСЛЕДНЯЯ (текущая) плита трейла.
            if (coordinate == _trail.CurrentPosition)
                Die("Плита под ногами обрушилась");
        }

        private void Die(string reason)
        {
            if (!IsAlive) return;
            IsAlive = false;
            Died?.Invoke(reason);
        }
    }
}
