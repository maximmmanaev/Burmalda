using System;
using Burmalda.Artifacts;

namespace Burmalda.Altar
{
    /// <summary>Сундук Талисманов (PRD раздел 7) — один из вариантов основного слота витрины (см. <see cref="Ritual"/>).</summary>
    public sealed class TalismanChest : Chest
    {
        public TalismanChest(int cost, Talisman content) : base(ChestType.Talisman, cost)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public Talisman Content { get; }
    }
}
