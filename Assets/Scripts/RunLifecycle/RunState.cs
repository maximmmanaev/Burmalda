using System;
using Burmalda.Core;
using Burmalda.D20;
using Burmalda.Decay;
using Burmalda.Movement;

namespace Burmalda.RunLifecycle
{
    /// <summary>
    /// Жизнь/смерть текущего забега (PRD 9, issue #24). Смерть от ловушки/
    /// обвала плиты запускает d20-испытание вместо мгновенного проигрыша —
    /// см. <see cref="ResolveHazard"/>. Слушает источники опасности —
    /// попытку шагнуть на смертельную ловушку
    /// (<see cref="GridTraceTrail.LethalTrapTriggered"/>, PRD 4.2) и
    /// обрушение плиты под ногами игрока
    /// (<see cref="TrailDecaySystem.TileDestroyed"/> для текущей позиции) —
    /// по аналогии с legacy/burmolda_demo.html, attemptDeath().
    /// Поражение от Босса — отдельный путь, <see cref="ReportBossDefeat"/>:
    /// d20 к нему НЕ применяется (PRD v7 §8.3, issue #82) — детерминировано.
    /// Ничего не знает про рестарт — при новом забеге создаётся новый
    /// экземпляр (мир перегенерируется заново, см. GridTraceInputController.Restart).
    /// </summary>
    public sealed class RunState : IDisposable
    {
        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly TrailDecaySystem _decay;
        private readonly D20Trial _d20;
        private GridCoordinate _lastAltarCoordinate;
        private bool _disposed;

        public RunState(TunnelGrid grid, GridTraceTrail trail, TrailDecaySystem decay, D20Trial d20)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _decay = decay ?? throw new ArgumentNullException(nameof(decay));
            _d20 = d20 ?? throw new ArgumentNullException(nameof(d20));
            IsAlive = true;
            // Запасной "чекпоинт" для Knockback (#24), пока игрок ещё не
            // прошёл ни одного Алтаря в этом забеге — откат к старту.
            _lastAltarCoordinate = trail.CurrentPosition;

            _trail.LethalTrapTriggered += OnLethalTrapTriggered;
            _decay.TileDestroyed += OnTileDestroyed;
            _trail.PositionChanged += OnPositionChanged;
        }

        /// <summary>Жив ли игрок в этом забеге. Становится false ровно один раз — далее не меняется.</summary>
        public bool IsAlive { get; private set; }

        /// <summary>Срабатывает один раз, когда игрок умирает — с человекочитаемой причиной для будущего UI.</summary>
        public event Action<string> Died;

        /// <summary>Срабатывает при каждом броске d20 (issue #24) — для будущей визуальной обратной связи (PRD §19).</summary>
        public event Action<D20Outcome> D20Resolved;

        /// <summary>Отписывается от трейла и распада. Вызывать при завершении забега/пересборке на рестарте.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.LethalTrapTriggered -= OnLethalTrapTriggered;
            _decay.TileDestroyed -= OnTileDestroyed;
            _trail.PositionChanged -= OnPositionChanged;
            _disposed = true;
        }

        /// <summary>Поражение от Босса (PRD v7 §8.3, issue #82) — детерминировано, d20 не бросается.</summary>
        public void ReportBossDefeat(string reason) => Die(reason);

        private void OnPositionChanged(GridCoordinate coordinate)
        {
            if (_grid.TryGetTile(coordinate, out var tile) && tile.IsAltar)
                _lastAltarCoordinate = coordinate;
        }

        private void OnLethalTrapTriggered(GridCoordinate coordinate, LethalTrapType trapType) =>
            ResolveHazard(DescribeLethalTrap(trapType));

        private static string DescribeLethalTrap(LethalTrapType trapType) => trapType switch
        {
            LethalTrapType.Lava => "Сгорел в лаве",
            LethalTrapType.ArrowWave => "Пронзён стрелой",
            LethalTrapType.BombBlast => "Подорвался на бомбе",
            LethalTrapType.BladeTact => "Разрублен лезвием",
            LethalTrapType.LavaWave => "Сгорел в лаве",
            _ => "Сработала ловушка"
        };

        private void OnTileDestroyed(GridCoordinate coordinate)
        {
            // legacy/burmolda_demo.html, updateDecay(): проверяется только ПОСЛЕДНЯЯ (текущая) плита трейла.
            if (coordinate == _trail.CurrentPosition)
                ResolveHazard("Плита под ногами обрушилась");
        }

        /// <summary>
        /// Бросает d20 (PRD раздел 9) вместо мгновенной смерти от ловушки/
        /// обвала. Fortune — ничего не меняется, игрок остаётся на месте.
        /// Knockback — телепорт к последнему пройденному Алтарю (или к
        /// старту, если Алтарей ещё не было), <paramref name="reason"/> не
        /// используется (не умер). Death — обычная смерть с этой причиной.
        /// </summary>
        private void ResolveHazard(string reason)
        {
            if (!IsAlive) return;

            var outcome = _d20.Roll();
            D20Resolved?.Invoke(outcome);

            switch (outcome)
            {
                case D20Outcome.Knockback:
                    _trail.TeleportTo(_lastAltarCoordinate);
                    break;
                case D20Outcome.Death:
                    Die(reason);
                    break;
                case D20Outcome.Fortune:
                default:
                    break;
            }
        }

        private void Die(string reason)
        {
            if (!IsAlive) return;
            IsAlive = false;
            Died?.Invoke(reason);
        }
    }
}
