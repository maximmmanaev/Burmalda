using System;
using Burmalda.Core;
using Burmalda.Movement;

namespace Burmalda.Decay
{
    /// <summary>
    /// Распад плит трейла (PRD 4.1, 16): плиты позади игрока разрушаются со
    /// временем — источник постоянного давления вместо таймера на
    /// прохождение. Портировано из updateDecay()/tryAct() в
    /// legacy/burmolda_demo.html; константы тайминга не менялись.
    /// Стартовая плита трейла (index 0, "safe" в прототипе) распаду не
    /// подвержена — как и в прототипе (updateDecay: "if(r===0)continue").
    /// </summary>
    public sealed class TrailDecaySystem : IDisposable
    {
        // legacy/burmolda_demo.html, init(): decayAcc = 1
        private const float InitialDecayAcceleration = 1f;
        // legacy/burmolda_demo.html, updateDecay(): decayAcc += dt * 0.025
        private const float DecayAccelerationPerSecond = 0.025f;
        // legacy/burmolda_demo.html, tryAct(): t.maxD = 6 + trail.length * 0.18
        private const float BaseDecayThresholdSeconds = 6f;
        private const float DecayThresholdSecondsPerTrailIndex = 0.18f;

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private float _decayAcceleration = InitialDecayAcceleration;
        private bool _disposed;

        public TrailDecaySystem(TunnelGrid grid, GridTraceTrail trail)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _trail.Advanced += OnTrailAdvanced;
        }

        /// <summary>Срабатывает, когда какая-либо плита трейла впервые разрушается распадом.</summary>
        public event Action<GridCoordinate> TileDestroyed;

        /// <summary>Разрушена ли плита под текущей позицией игрока (провал под ногами).</summary>
        public bool IsCurrentTileDestroyed => _grid.GetOrCreateTile(_trail.CurrentPosition).IsDestroyed;

        /// <summary>
        /// Продвигает распад на <paramref name="deltaSeconds"/> секунд
        /// реального времени: ускоряет глобальный темп распада и накапливает
        /// время распада для всех активных (уже начавших распадаться) плит трейла.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            _decayAcceleration += deltaSeconds * DecayAccelerationPerSecond;

            var path = _trail.Path;
            for (var i = 1; i < path.Count; i++) // i=0 — стартовая плита, не распадается
            {
                var tile = _grid.GetOrCreateTile(path[i]);
                var wasDestroyed = tile.IsDestroyed;
                tile.AdvanceDecay(deltaSeconds * _decayAcceleration);
                if (!wasDestroyed && tile.IsDestroyed) TileDestroyed?.Invoke(tile.Coordinate);
            }
        }

        /// <summary>Отписывается от трейла. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.Advanced -= OnTrailAdvanced;
            _disposed = true;
        }

        private void OnTrailAdvanced(GridCoordinate coordinate)
        {
            // Advanced стреляет только из TryAdvanceTo, т.е. index всегда >= 1 —
            // стартовая плита (index 0) добавляется в конструкторе трейла и в
            // это событие никогда не попадает.
            var trailIndex = _trail.Path.Count - 1;
            var threshold = BaseDecayThresholdSeconds + trailIndex * DecayThresholdSecondsPerTrailIndex;
            _grid.GetOrCreateTile(coordinate).BeginDecay(threshold);
        }
    }
}
