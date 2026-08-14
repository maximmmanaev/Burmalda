using System;
using Burmalda.Artifacts;

namespace Burmalda.Altar
{
    /// <summary>Сундук Амулетов (PRD раздел 7) — другой вариант основного слота витрины (см. <see cref="Ritual"/>).</summary>
    public sealed class AmuletChest : Chest
    {
        public AmuletChest(int cost, Amulet content) : base(ChestType.Amulet, cost)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public Amulet Content { get; }
    }
}
