using System;
using Burmalda.Core;
using Burmalda.Movement;

namespace Burmalda.Decay
{
    /// <summary>
    /// Распад плит трейла (PRD 4.1, 16): плиты позади игрока разрушаются со
    /// временем — источник постоянного давления вместо таймера на
    /// прохождение. Портировано из updateDecay()/tryAct() в
    /// legacy/burmolda_demo.html. Пороги распада ускорены в 2 раза
    /// относительно прототипа по прямому запросу владельца продукта (черновой
    /// тюнинг «на глазок», без формального issue — финальное значение задаст
    /// плейтест баланса, Спринт 10); ускорение распада во времени
    /// (DecayAccelerationPerSecond) не менялось.
    /// Стартовая плита трейла (index 0, "safe" в прототипе) распаду не
    /// подвержена — как и в прототипе (updateDecay: "if(r===0)continue").
    ///
    /// <b>Сверка с философией сложности (PRD §18, issue #75, Спринт 9):</b>
    /// "карта не давит таймером, а стимулирует... риск через отклонение от
    /// маршрута". Само срабатывание распада ПОЛНОСТЬЮ завязано на трейл
    /// игрока (<see cref="OnTrailAdvanced"/> начинает отсчёт для конкретной
    /// пройденной плиты) — нет глобального экранного таймера/countdown,
    /// смерть приходит только от плиты, которую разрушил сам игрок своим же
    /// маршрутом, что соответствует формулировке дословно.
    /// <see cref="_decayAcceleration"/> растёт от РЕАЛЬНОГО прошедшего
    /// времени (<see cref="Tick"/> тикает каждый кадр вне зависимости от
    /// того, движется ли игрок) — это единственный момент, теоретически
    /// похожий на "таймер": если игрок просто стоит на месте, плита ПОД НИМ
    /// (уже начавшая распадаться с момента шага на неё) в конце концов
    /// разрушится и без движения дальше. Признано СООТВЕТСТВУЮЩИМ
    /// философии, а не расхождением: угроза по-прежнему локальна (только
    /// плиты, на которые игрок сам наступил) и является следствием
    /// собственного решения игрока задержаться, а не внешним видимым
    /// countdown-UI, который философия явно противопоставляет "давлению
    /// таймером" — такого UI-элемента в проекте нет и не появится по этой
    /// механике. Формула ускорения — буквальный порт прототипа
    /// (см. выше), не новое расхождение этого спринта.
    /// </summary>
    public sealed class TrailDecaySystem : IDisposable
    {
        // legacy/burmolda_demo.html, init(): decayAcc = 1
        private const float InitialDecayAcceleration = 1f;
        // legacy/burmolda_demo.html, updateDecay(): decayAcc += dt * 0.025
        private const float DecayAccelerationPerSecond = 0.025f;
        // Было буквально из прототипа: t.maxD = 6 + trail.length*0.18.
        // Пороги вдвое уменьшены по запросу владельца продукта — плиты
        // разрушаются в 2 раза быстрее.
        private const float BaseDecayThresholdSeconds = 3f;
        private const float DecayThresholdSecondsPerTrailIndex = 0.09f;

        /// <summary>
        /// PRD v10 §7.5: ускорение распада на каждый несомый Саркофаг. ЧЕРНОВОЕ
        /// значение [CONFIG] — финал задаёт плейтест.
        /// </summary>
        public const float DecayAccelerationPerSarcophagus = 0.5f;

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly float _decayThresholdMultiplier;
        private float _decayAcceleration = InitialDecayAcceleration;
        private float _suspendedSecondsRemaining;
        private bool _disposed;

        /// <param name="decayThresholdMultiplier">
        /// Множитель порога распада (PRD v7 §20, Знамение «Хрупкий Свод»:
        /// "порог распада плит −25%" → 0.75). По умолчанию 1 (нейтрально) —
        /// эта система намеренно не ссылается на <c>RunModifiers.RunModifiers</c>
        /// напрямую (Decay.asmdef не получает такой зависимости), значение
        /// читает и передаёт вызывающая сторона (см. Decay.TrailDecayController).
        /// </param>
        public TrailDecaySystem(TunnelGrid grid, GridTraceTrail trail, float decayThresholdMultiplier = 1f)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _decayThresholdMultiplier = decayThresholdMultiplier;
            _trail.Advanced += OnTrailAdvanced;
        }

        /// <summary>Срабатывает, когда какая-либо плита трейла впервые разрушается распадом.</summary>
        public event Action<GridCoordinate> TileDestroyed;

        /// <summary>Разрушена ли плита под текущей позицией игрока (провал под ногами).</summary>
        public bool IsCurrentTileDestroyed => _grid.GetOrCreateTile(_trail.CurrentPosition).IsDestroyed;

        /// <summary>Число несомых Саркофагов (PRD v10 §7.5): каждый ускоряет распад под игроком.</summary>
        public int CarriedSarcophagi { get; set; }

        /// <summary>
        /// Множитель времени жизни плиты по полосе Яруса (<see cref="YarusProgression.DecayLifetimeMultiplier"/>,
        /// спринт v10-2). Применяется к плитам, у которых распад начинается после установки; по умолчанию 1.
        /// </summary>
        public float YarusLifetimeMultiplier { get; set; } = 1f;

        /// <summary>Время жизни плиты (сек) для следующего шага при текущей длине трейла — для дебаг-панели.</summary>
        public float NextTileLifetimeSeconds => ComputeThreshold(_trail.Path.Count);

        /// <summary>Множитель распада от несомых Саркофагов (1 + 0.5 × N) — для дебаг-панели.</summary>
        public float CarriedSarcophagusFactor => 1f + DecayAccelerationPerSarcophagus * Math.Max(0, CarriedSarcophagi);

        /// <summary>Распад временно приостановлен (PRD раздел 12 — Тотем, Неуязвимость).</summary>
        public bool IsSuspended => _suspendedSecondsRemaining > 0f;

        /// <summary>
        /// Приостанавливает распад на <paramref name="seconds"/> секунд
        /// реального времени (PRD раздел 12: "Трейл временно не распадается").
        /// Повторный вызов во время уже идущей приостановки берёт большее из
        /// двух значений оставшегося времени, а не складывает их.
        /// </summary>
        public void Suspend(float seconds)
        {
            if (seconds <= 0f) return;
            _suspendedSecondsRemaining = Math.Max(_suspendedSecondsRemaining, seconds);
        }

        /// <summary>
        /// Продвигает распад на <paramref name="deltaSeconds"/> секунд
        /// реального времени: ускоряет глобальный темп распада и накапливает
        /// время распада для всех активных (уже начавших распадаться) плит
        /// трейла. Не-op целиком, пока действует <see cref="Suspend"/>.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            if (_suspendedSecondsRemaining > 0f)
            {
                _suspendedSecondsRemaining -= deltaSeconds;
                return;
            }

            _decayAcceleration += deltaSeconds * DecayAccelerationPerSecond;

            var carriedFactor = CarriedSarcophagusFactor;
            var path = _trail.Path;
            for (var i = 1; i < path.Count; i++) // i=0 — стартовая плита, не распадается
            {
                var tile = _grid.GetOrCreateTile(path[i]);
                var wasDestroyed = tile.IsDestroyed;
                tile.AdvanceDecay(deltaSeconds * _decayAcceleration * carriedFactor);
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
            _grid.GetOrCreateTile(coordinate).BeginDecay(ComputeThreshold(trailIndex));
        }

        // «Честное окно»: полоса Яруса сокращает время жизни, но не ниже YarusProgression.MinTileLifetimeSeconds
        // (и не выше исходного порога — пол никогда не удлиняет жизнь плиты).
        private float ComputeThreshold(int trailIndex)
        {
            var threshold = (BaseDecayThresholdSeconds + trailIndex * DecayThresholdSecondsPerTrailIndex) * _decayThresholdMultiplier;
            return Math.Max(threshold * YarusLifetimeMultiplier, Math.Min(threshold, YarusProgression.MinTileLifetimeSeconds));
        }
    }
}
