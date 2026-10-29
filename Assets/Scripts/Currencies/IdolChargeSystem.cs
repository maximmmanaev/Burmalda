using System;

namespace Burmalda.Currencies
{
    /// <summary>
    /// Заряд Идола (PRD v10 §12.2, §18.1): каждая плита серии чистого прохода заполняет шкалу, полная шкала — одно
    /// применение. Обрыв серии заряд <b>не сбрасывает</b>. Без Идола в слоте заряд не копится («голый» старт).
    /// </summary>
    public sealed class IdolChargeSystem : IDisposable
    {
        /// <summary>Плит серии до полной шкалы [CONFIG, черновик].</summary>
        public static int Capacity = 12;

        private readonly CleanPassStreakSystem _streak;
        private readonly Func<bool> _hasIdol;
        private int _lastStreak;
        private bool _disposed;

        public IdolChargeSystem(CleanPassStreakSystem streak, Func<bool> hasIdol)
        {
            _streak = streak ?? throw new ArgumentNullException(nameof(streak));
            _hasIdol = hasIdol ?? throw new ArgumentNullException(nameof(hasIdol));
            _streak.Changed += OnStreakChanged;
        }

        /// <summary>Накоплено плит серии (0..<see cref="Capacity"/>).</summary>
        public int Charge { get; private set; }

        public bool IsFull => Charge >= Capacity;

        /// <summary>Заполненность шкалы 0..1.</summary>
        public float Fill => Capacity <= 0 ? 0f : Math.Min(1f, Charge / (float)Capacity);

        public event Action Changed;

        /// <summary>Тратит полную шкалу на применение. False, если шкала не полна.</summary>
        public bool TryConsume()
        {
            if (!IsFull) return false;
            Charge = 0;
            Changed?.Invoke();
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _streak.Changed -= OnStreakChanged;
            _disposed = true;
        }

        private void OnStreakChanged(int streak)
        {
            var grew = streak > _lastStreak;
            _lastStreak = streak; // обрыв серии (0) заряд не трогает
            if (!grew || !_hasIdol() || IsFull) return;
            Charge++;
            Changed?.Invoke();
        }
    }
}
