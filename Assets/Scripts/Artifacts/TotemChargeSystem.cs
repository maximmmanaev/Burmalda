using System;
using Burmalda.Core;
using Burmalda.Movement;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Заряд активной способности Тотема (PRD раздел 12: "копится по ходу
    /// забега"). Реагирует на <see cref="GridTraceTrail.Advanced"/> — растёт
    /// только на НОВЫХ (уникальных, #61) плитах трейла, не на повторных
    /// шагах — так же, как в прототипе заряд начисляется из tryAct только
    /// при успешном продвижении (legacy/burmolda_demo.html:
    /// "totemCharge=Math.min(100,totemCharge+2.5*totemLevel)").
    ///
    /// Формула адаптирована под то, что в этом проекте <see cref="Totem.Level"/>
    /// стартует с 0 (апгрейд Руной, см. issue #21), а не с 1, как totemLevel
    /// в прототипе: ChargePerTileBase*(1+Level) вместо ChargePerTileBase*
    /// totemLevel — при Level=0 даёт то же базовое значение 2.5, что и
    /// totemLevel=1 в прототипе.
    /// </summary>
    public sealed class TotemChargeSystem : IDisposable
    {
        // legacy/burmolda_demo.html: totemCharge+=2.5*totemLevel
        private const float ChargePerTileBase = 2.5f;
        // legacy/burmolda_demo.html: Math.min(100, ...)
        private const float MaxCharge = 100f;

        private readonly GridTraceTrail _trail;
        private readonly Totem _totem;
        private bool _disposed;

        public TotemChargeSystem(GridTraceTrail trail, Totem totem)
        {
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _totem = totem ?? throw new ArgumentNullException(nameof(totem));
            _trail.Advanced += OnAdvanced;
        }

        /// <summary>Текущий заряд в диапазоне [0; 100].</summary>
        public float Charge { get; private set; }

        /// <summary>Заряд полон — способность можно активировать (см. <see cref="TryActivate"/>).</summary>
        public bool IsReady => Charge >= MaxCharge;

        /// <summary>Срабатывает при каждом реальном изменении <see cref="Charge"/>.</summary>
        public event Action<float> ChargeChanged;

        /// <summary>Срабатывает при успешной активации (см. <see cref="TryActivate"/>).</summary>
        public event Action Activated;

        /// <summary>
        /// Пытается потратить полный заряд. Ложь и без изменений, если
        /// <see cref="IsReady"/> ложь. При успехе обнуляет заряд — по
        /// прототипу ("После использования — шкала обнуляется", PRD раздел 12).
        /// </summary>
        public bool TryActivate()
        {
            if (!IsReady) return false;

            Charge = 0f;
            ChargeChanged?.Invoke(Charge);
            Activated?.Invoke();
            return true;
        }

        /// <summary>Отписывается от трейла. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.Advanced -= OnAdvanced;
            _disposed = true;
        }

        private void OnAdvanced(GridCoordinate coordinate)
        {
            var newCharge = Math.Min(MaxCharge, Charge + ChargePerTileBase * (1 + _totem.Level));
            if (newCharge <= Charge) return; // уже на потолке — без изменений, событие не поднимается
            Charge = newCharge;
            ChargeChanged?.Invoke(Charge);
        }
    }
}
