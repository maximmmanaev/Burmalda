using System;

namespace Burmalda.RunLifecycle
{
    /// <summary>
    /// Способность Тотема «Второе дыхание» (PRD v4 §12, issue #23) — "одно
    /// бесплатное спасение за забег при провале возврата". Временный (в
    /// забеге) заряд: доступен, если экипированный Тотем игрока даёт эту
    /// способность (решение вызывающей стороны, передаётся в конструктор —
    /// сам класс не знает про <c>Artifacts.Totem</c>), тратится один раз.
    ///
    /// <b>Не подключено к реальному триггеру</b>: "провал возврата" —
    /// механика Возврата в лагерь (issue #26, Спринт 8), которой в проекте
    /// ещё нет. Этот класс — сам заряд и правило "один раз за забег";
    /// вызов <see cref="TryConsume"/> в нужный момент — задача Спринта 8.
    /// </summary>
    public sealed class SecondWindCharge
    {
        public SecondWindCharge(bool isAvailable)
        {
            IsAvailable = isAvailable;
        }

        public bool IsAvailable { get; private set; }

        /// <summary>Срабатывает при успешной трате заряда.</summary>
        public event Action Consumed;

        /// <summary>Тратит заряд, если он доступен. Возвращает false, если уже был потрачен (или изначально недоступен).</summary>
        public bool TryConsume()
        {
            if (!IsAvailable) return false;
            IsAvailable = false;
            Consumed?.Invoke();
            return true;
        }
    }
}
