using System;
using Burmalda.Artifacts;

namespace Burmalda.Altar
{
    /// <summary>Сундук Реликвий (PRD раздел 7) — "очень редко на месте амулета появляется Реликвия" (PRD раздел 7, если система уже разлочена).</summary>
    public sealed class RelicChest : Chest
    {
        public RelicChest(int cost, Relic content) : base(ChestType.Relic, cost)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public Relic Content { get; }
    }
}
