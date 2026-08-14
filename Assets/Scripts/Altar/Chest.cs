using System;

namespace Burmalda.Altar
{
    /// <summary>Общий термин для витрины Алтаря (PRD раздел 7) — конкретные подтипы хранят типизированное содержимое.</summary>
    public abstract class Chest
    {
        protected Chest(ChestType type, int cost)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost), cost, "Цена сундука не может быть отрицательной.");
            Type = type;
            Cost = cost;
        }

        public ChestType Type { get; }

        /// <summary>Цена в Ключах (PRD раздел 7).</summary>
        public int Cost { get; }
    }
}
