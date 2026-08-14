using System;

namespace Burmalda.D20
{
    /// <summary>
    /// d20-испытание (PRD раздел 9, issue #24): "При смерти... вместо
    /// мгновенного проигрыша бросается d20." Источник броска внедряется явно
    /// (как <c>Func&lt;float&gt;</c> у <c>TunnelObstacleGenerator</c>) —
    /// тесты подставляют конкретное значение, в игре — реальный кубик 1..20.
    /// </summary>
    public sealed class D20Trial
    {
        private readonly Func<int> _rollD20;

        public D20Trial(Func<int> rollD20)
        {
            _rollD20 = rollD20 ?? throw new ArgumentNullException(nameof(rollD20));
        }

        /// <summary>Бросает кубик и возвращает исход по таблице PRD раздела 9 (15–20/10–14/1–9).</summary>
        public D20Outcome Roll()
        {
            var value = _rollD20();
            if (value < 1 || value > 20)
                throw new InvalidOperationException($"Бросок d20 вне диапазона [1, 20]: {value}.");

            if (value >= 15) return D20Outcome.Fortune;
            return value >= 10 ? D20Outcome.Knockback : D20Outcome.Death;
        }
    }
}
