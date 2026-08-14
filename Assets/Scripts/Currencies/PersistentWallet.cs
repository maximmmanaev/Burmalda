using System;

namespace Burmalda.Currencies
{
    /// <summary>
    /// Постоянная (не сбрасывается между забегами) валюта (PRD раздел 5) —
    /// общая реализация для Монет и Кристаллов: обе структурно одинаковы
    /// (баланс, пополнение, трата с проверкой достатка). Пока в проекте нет
    /// сохранения прогресса (issue #107, Спринт 9) — баланс живёт только в
    /// памяти текущей игровой сессии, не переживает перезапуск приложения;
    /// владелец кошелька (см. <c>CurrencyController</c>) не пересоздаёт его
    /// между забегами, только между сессиями приложения.
    /// </summary>
    public sealed class PersistentWallet
    {
        /// <summary>Текущий баланс.</summary>
        public int Balance { get; private set; }

        /// <summary>Срабатывает при каждом изменении баланса — с новым значением.</summary>
        public event Action<int> Changed;

        /// <summary>Пополняет баланс. Неположительные значения игнорируются.</summary>
        public void Deposit(int amount)
        {
            if (amount <= 0) return;
            Balance += amount;
            Changed?.Invoke(Balance);
        }

        /// <summary>Списывает <paramref name="amount"/>, если баланса достаточно. Возвращает false и не меняет баланс иначе (в т.ч. для неположительной суммы).</summary>
        public bool Spend(int amount)
        {
            if (amount <= 0) return false;
            if (amount > Balance) return false;

            Balance -= amount;
            Changed?.Invoke(Balance);
            return true;
        }
    }
}
