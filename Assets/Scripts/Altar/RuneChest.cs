using System;
using Burmalda.Artifacts;

namespace Burmalda.Altar
{
    /// <summary>Сундук Рун (PRD раздел 7, issue #21) — выбор Руны, постоянно улучшающей 1 из 2 пассивов Идола или активную способность Тотема.</summary>
    public sealed class RuneChest : Chest
    {
        public RuneChest(int cost, Rune content) : base(ChestType.Rune, cost)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public Rune Content { get; }
    }
}
