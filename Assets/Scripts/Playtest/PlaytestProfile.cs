using System;
using Burmalda.Artifacts;
using Burmalda.Currencies;

namespace Burmalda.Playtest
{
    /// <summary>
    /// «Сохранение» игрока-бота между забегами: кошелёк Монет, открытые Реликвии, части Идолов/Тотемов,
    /// Лагерь с неоткрытыми Саркофагами. Те же классы, что в игре (<c>Camp.Camp</c>, <c>CollectionProgress</c>).
    /// </summary>
    public sealed class PlaytestProfile
    {
        public PlaytestProfile(int seed)
        {
            Rng = new Random(seed);
            Coins = new PersistentWallet();
            Pool = new ArtifactPool();
            foreach (var id in RelicCatalog.StarterIds) Pool.Unlock(id); // как AltarController.Awake
            Camp = new Camp.Camp(Coins, Pool, new CollectionProgress(), Rng);
        }

        /// <summary>Случайность добычи (редкость и содержимое Саркофагов) — детерминирована seed профиля.</summary>
        public Random Rng { get; }

        public PersistentWallet Coins { get; }
        public ArtifactPool Pool { get; }
        public Camp.Camp Camp { get; }

        /// <summary>Последний Ярус, доступный за дверью (PRD §6.2): при Лагере 1 — Ярус 3.</summary>
        public int LastOpenYarus { get; set; } = 3;
    }
}
