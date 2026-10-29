namespace Burmalda.Movement
{
    /// <summary>
    /// Двойной тап по своей плите (PRD v10 §7.2 п.5, §18.1) — активация Идола. Два нажатия на плиту игрока в пределах
    /// <see cref="WindowSeconds"/> [CONFIG, черновик]. Не мешает прицеливанию (оно только на соседние плиты) и не пересекается
    /// с иконками Рун (они вне зоны плит).
    /// </summary>
    public sealed class DoubleTapDetector
    {
        public static float WindowSeconds = 0.35f;

        private float _lastTapTime = float.NegativeInfinity;

        /// <summary>Регистрирует нажатие на свою плиту. True — это второй тап подряд (двойной тап), серия сбрасывается.</summary>
        public bool Register(float now)
        {
            if (now - _lastTapTime <= WindowSeconds)
            {
                _lastTapTime = float.NegativeInfinity;
                return true;
            }

            _lastTapTime = now;
            return false;
        }

        /// <summary>Сбрасывает серию (тап вне своей плиты, смена забега).</summary>
        public void Reset() => _lastTapTime = float.NegativeInfinity;
    }
}
