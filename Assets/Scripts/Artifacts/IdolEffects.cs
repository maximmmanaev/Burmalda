using System;
using Burmalda.Core;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Слой эффектов Идола (PRD v10 §18.2): щит от попадания, окно неуязвимости к ловушкам, замедление и остановка времени
    /// ловушек и распада, подавление ловушек на ближайшие шаги. Подключается к <see cref="RunEffects"/> слоем «idol».
    /// От Лавы не защищает: Лава в <see cref="IRunEffects.TryAbsorbHit"/> не передаётся (§32, инвариант 5).
    /// </summary>
    public sealed class IdolEffects : IRunEffects
    {
        private readonly Func<float> _now;
        private float _invulnerableUntil = float.NegativeInfinity;
        private float _slowUntil = float.NegativeInfinity;
        private float _freezeUntil = float.NegativeInfinity;
        private int _suppressedSteps;

        public IdolEffects(Func<float> now) => _now = now ?? throw new ArgumentNullException(nameof(now));

        /// <summary>Сколько попаданий ещё поглотит Щит.</summary>
        public int ShieldHits { get; private set; }

        /// <summary>Вызывается при поглощении попадания Щитом (ур. 2 — отбросить игрока на безопасную соседнюю плиту).</summary>
        public Action ShieldAbsorbed { get; set; }

        /// <summary>Доля замедления Идола «Время» (ур. 1: ловушки и распад вдвое медленнее) [CONFIG].</summary>
        public static float SlowScale = 0.5f;

        public void GrantShield(int hits) => ShieldHits += hits;

        public void GrantInvulnerability(float seconds) => _invulnerableUntil = Math.Max(_invulnerableUntil, _now() + seconds);

        public void StartSlow(float seconds) => _slowUntil = Math.Max(_slowUntil, _now() + seconds);

        public void StartFreeze(float seconds) => _freezeUntil = Math.Max(_freezeUntil, _now() + seconds);

        /// <summary>Следующие <paramref name="steps"/> шагов не активируют ловушки (Переход, ур. 3).</summary>
        public void SuppressTrapsForSteps(int steps) => _suppressedSteps = Math.Max(_suppressedSteps, steps);

        /// <summary>Шаг сделан — расходует подавление ловушек.</summary>
        public void OnStepTaken()
        {
            if (_suppressedSteps > 0) _suppressedSteps--;
        }

        public bool IsInvulnerable => _now() < _invulnerableUntil;

        public bool IsSlowActive => _now() < _slowUntil;

        public bool IsFreezeActive => _now() < _freezeUntil;

        public float ThreatTimeScale => IsFreezeActive ? 0f : IsSlowActive ? SlowScale : 1f;

        public bool StepsActivateTraps => _suppressedSteps <= 0;

        public bool TryAbsorbHit(ThreatType threat)
        {
            if (IsInvulnerable) return true; // окно неуязвимости к ловушкам — щит не тратится
            if (ShieldHits <= 0) return false;
            ShieldHits--;
            ShieldAbsorbed?.Invoke();
            return true;
        }

        public float WallCloseSlowdown => 1f;
        public float CollapseSpeedMultiplier => 1f;
        public float SourceManaMultiplier => 1f;
        public float VeinManaMultiplier => 1f;
        public float ManaMultiplier => 1f;
        public float StreakBonusMultiplier => 1f;
        public float DecayRateMultiplier => 1f;
        public float ResonanceBonus => NeutralRunEffects.BaseResonanceBonus;
        public float BearTrapEscapeMultiplier => 1f;
        public bool CollapseHeld => false;
        public int BonusManaForStep(int stepNumber) => 0;
    }
}
