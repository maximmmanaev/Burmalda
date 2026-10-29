using System;
using System.Collections.Generic;
using System.Linq;
using Burmalda.Artifacts;

namespace Burmalda.Camp
{
    public enum SarcophagusLootKind
    {
        /// <summary>Только Монеты.</summary>
        CoinsOnly,

        /// <summary>Новая Реликвия (открыта навсегда).</summary>
        Relic,

        /// <summary>Часть Идола/Тотема.</summary>
        Part,
    }

    /// <summary>Что выпало из Саркофага (PRD v10 §21.2): Монеты всегда, затем часть или Реликвия, не больше одной.</summary>
    public sealed class SarcophagusLoot
    {
        public SarcophagusLoot(RelicRarity rarity, int coins, SarcophagusLootKind kind, RelicDefinition relic, CollectibleItem part,
            IReadOnlyList<CollectibleItem> partChoices, bool firstPartGift)
        {
            Rarity = rarity;
            Coins = coins;
            Kind = kind;
            Relic = relic;
            Part = part;
            PartChoices = partChoices;
            FirstPartGift = firstPartGift;
        }

        public RelicRarity Rarity { get; }

        public int Coins { get; }

        public SarcophagusLootKind Kind { get; }

        /// <summary>Новая Реликвия (Kind == Relic).</summary>
        public RelicDefinition Relic { get; }

        /// <summary>Предмет, в который ушла часть (Kind == Part, выбора нет); null при выборе из <see cref="PartChoices"/>.</summary>
        public CollectibleItem Part { get; }

        /// <summary>Игроку предлагают выбрать, чья часть (2–3 предмета); null, если выбора нет (§21.4).</summary>
        public IReadOnlyList<CollectibleItem> PartChoices { get; }

        /// <summary>Это подарочная часть первого Саркофага.</summary>
        public bool FirstPartGift { get; }

        public bool NeedsPartChoice => PartChoices != null;
    }

    /// <summary>Числа Саркофагов — ЧЕРНОВЫЕ [CONFIG] (PRD v10 §21): таблица шансов редкости, Монеты, шансы частей и Реликвий, защита от невезения.</summary>
    public static class SarcophagusConfig
    {
        /// <summary>Шансы редкости по рангу бога: [Редкий, Эпический, Легендарный], в процентах (§21.1).</summary>
        public static readonly double[][] RarityPercentByRank =
        {
            new[] { 80.0, 17.0, 3.0 },   // младший
            new[] { 55.0, 35.0, 10.0 },  // средний
            new[] { 25.0, 35.0, 40.0 },  // старший
        };

        /// <summary>Монеты по редкости [CONFIG, черновик].</summary>
        public static int[] CoinsByRarity = { 100, 200, 400 };

        /// <summary>Базовый шанс части по редкости, % (§21.2: 15 / 4 / 1.5).</summary>
        public static double[] PartBasePercent = { 15.0, 4.0, 1.5 };

        /// <summary>Прибавка к шансу части за каждый «пустой» Саркофаг этой редкости, % (§21.2: +3 / +0.25 / +0.25).</summary>
        public static double[] PartPityStepPercent = { 3.0, 0.25, 0.25 };

        /// <summary>Базовый шанс новой Реликвии, % (§21.2: 2), для всех редкостей.</summary>
        public static double RelicBasePercent = 2.0;

        /// <summary>Прибавка за каждый Саркофаг этой редкости без Реликвии, % (§21.2: +1).</summary>
        public static double RelicPityStepPercent = 1.0;

        /// <summary>Шанс предложить выбор «чья часть», если начатого предмета нет, % (§21.4: ~50 [CONFIG]).</summary>
        public static double PartChoicePercent = 50.0;

        public static int CoinsFor(RelicRarity rarity) => CoinsByRarity[(int)rarity];
    }

    /// <summary>Редкость Саркофага при взятии (PRD v10 §21.1) — скрыта в забеге, показывается при открытии в Лагере.</summary>
    public static class SarcophagusRarityRoller
    {
        /// <summary>Саркофаг, взятый в Святилище Яруса <paramref name="yarus"/>: бог и ранг из таблицы §5.2, редкость выпадает сейчас.</summary>
        public static Sarcophagus Take(int yarus, Random rng)
        {
            var shrine = GodTable.ForYarus(Math.Min(Math.Max(yarus, 1), GodTable.YarusCount));
            return new Sarcophagus((SarcophagusRank)(int)shrine.Rank, Roll(shrine.Rank, rng), shrine.God);
        }

        public static RelicRarity Roll(GodRank rank, Random rng)
        {
            var percent = SarcophagusConfig.RarityPercentByRank[(int)rank];
            var x = rng.NextDouble() * 100.0;
            if (x < percent[0]) return RelicRarity.Rare;
            return x < percent[0] + percent[1] ? RelicRarity.Epic : RelicRarity.Legendary;
        }
    }

    /// <summary>
    /// Содержимое Саркофага (PRD v10 §21.2–21.4): Монеты → проверка части (с защитой от невезения по редкости) → если
    /// части нет, проверка новой Реликвии. Без дубликатов (§21.3). Первый вынесенный Саркофаг всегда даёт часть Редкого.
    /// </summary>
    public static class SarcophagusLootRoller
    {
        public static SarcophagusLoot Roll(Sarcophagus sarcophagus, CollectionProgress progress, ArtifactPool pool, Random rng)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var rarity = sarcophagus.Rarity;
            var coins = SarcophagusConfig.CoinsFor(rarity);
            var r = (int)rarity;

            // Первый вынесенный Саркофаг — всегда часть Редкого Идола или Тотема (§21.2); счётчики защиты не трогаем.
            if (!progress.FirstPartGranted)
            {
                var rare = progress.Unassembled(RelicRarity.Rare);
                if (rare.Count > 0)
                {
                    progress.FirstPartGranted = true;
                    var first = ChoosePart(RelicRarity.Rare, progress, rng, allowChoice: false);
                    return new SarcophagusLoot(rarity, coins, SarcophagusLootKind.Part, null, first.item, null, firstPartGift: true);
                }
            }

            // Проверка части: если предметы этой редкости все собраны — проверки нет (§21.3).
            if (progress.Unassembled(rarity).Count > 0)
            {
                var chance = SarcophagusConfig.PartBasePercent[r] + SarcophagusConfig.PartPityStepPercent[r] * progress.PartMisses(rarity);
                if (rng.NextDouble() * 100.0 < chance)
                {
                    progress.SetPartMisses(rarity, 0);
                    progress.FirstPartGranted = true;
                    var (item, choices) = ChoosePart(rarity, progress, rng, allowChoice: true);
                    return new SarcophagusLoot(rarity, coins, SarcophagusLootKind.Part, null, item, choices, firstPartGift: false);
                }

                progress.SetPartMisses(rarity, progress.PartMisses(rarity) + 1);
            }

            // Проверка новой Реликвии: если все Реликвии этой редкости открыты — проверки нет.
            var unopened = RelicCatalog.All.Where(x => x.Rarity == rarity && !pool.IsUnlocked(x.Id)).ToList();
            if (unopened.Count > 0)
            {
                var chance = SarcophagusConfig.RelicBasePercent + SarcophagusConfig.RelicPityStepPercent * progress.RelicMisses(rarity);
                if (rng.NextDouble() * 100.0 < chance)
                {
                    progress.SetRelicMisses(rarity, 0);
                    var relic = PickRelic(unopened, sarcophagus.God, rng);
                    return new SarcophagusLoot(rarity, coins, SarcophagusLootKind.Relic, relic, null, null, firstPartGift: false);
                }

                progress.SetRelicMisses(rarity, progress.RelicMisses(rarity) + 1);
            }

            return new SarcophagusLoot(rarity, coins, SarcophagusLootKind.CoinsOnly, null, null, null, firstPartGift: false);
        }

        // Неоткрытая Реликвия этой редкости: сначала бога этого Святилища, затем его мифологии, затем любая (§21.3).
        private static RelicDefinition PickRelic(List<RelicDefinition> unopened, God god, Random rng)
        {
            var ofGod = unopened.Where(x => x.God == god).ToList();
            if (ofGod.Count > 0) return ofGod[rng.Next(ofGod.Count)];
            var mythology = RelicCatalog.MythologyOf(god);
            var ofMythology = unopened.Where(x => x.Mythology == mythology).ToList();
            if (ofMythology.Count > 0) return ofMythology[rng.Next(ofMythology.Count)];
            return unopened[rng.Next(unopened.Count)];
        }

        /// <summary>Чья часть (§21.4): уже начатый предмет; иначе случайный несобранный, с шансом ~50% — выбор из 2–3.</summary>
        private static (CollectibleItem item, IReadOnlyList<CollectibleItem> choices) ChoosePart(RelicRarity rarity, CollectionProgress progress, Random rng, bool allowChoice)
        {
            var started = progress.Started(rarity);
            if (started != null)
            {
                progress.AddPart(started.Id);
                return (started, null);
            }

            var pool = progress.Unassembled(rarity).ToList();
            if (allowChoice && pool.Count >= 2 && rng.NextDouble() * 100.0 < SarcophagusConfig.PartChoicePercent)
            {
                var count = Math.Min(pool.Count, 2 + rng.Next(2)); // 2 или 3
                var choices = new List<CollectibleItem>();
                while (choices.Count < count)
                {
                    var pick = pool[rng.Next(pool.Count)];
                    pool.Remove(pick);
                    choices.Add(pick);
                }

                return (null, choices);
            }

            var chosen = pool[rng.Next(pool.Count)];
            progress.AddPart(chosen.Id);
            return (chosen, null);
        }
    }
}
