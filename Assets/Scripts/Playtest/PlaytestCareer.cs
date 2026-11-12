using System;
using System.Collections.Generic;
using System.Linq;
using Burmalda.Artifacts;
using Burmalda.Camp;

namespace Burmalda.Playtest
{
    /// <summary>Итог одного забега бота — всё, что нужно отчёту.</summary>
    public sealed class RunSummary
    {
        public BotPolicyKind Policy;
        public int Seed;
        public RunOutcome Outcome;
        public float Seconds;
        public int Steps;
        public int FateRolls;
        public HazardCause? DeathCause;
        public int DeathYarus;
        public int Mana;
        public int CoinsFromMana;
        public int CoinsFromSarcophagi;
        public int SarcophagiTaken;
        public int SarcophagiEvacuated;
        public int SarcophagiLost;
        public int Parts;
        public int Relics;
        public YarusRecord[] Yarus;
        public IReadOnlyList<HazardRecord> Hazards;

        public int Coins => CoinsFromMana + CoinsFromSarcophagi;

        /// <summary>Строка для сравнения двух прогонов с одним seed.</summary>
        public string Fingerprint() =>
            $"{Policy}:{Seed}:{Outcome}:{Seconds:F2}:{Steps}:{FateRolls}:{DeathCause}:{Mana}:{Coins}:{SarcophagiTaken}:{SarcophagiEvacuated}:{Parts}:{Relics}";
    }

    /// <summary>«Карьера» одного игрока-бота: забеги подряд с общим Лагерем и часами игры.</summary>
    public sealed class CareerResult
    {
        public BotPolicyKind Policy;
        public readonly List<RunSummary> Runs = new List<RunSummary>();

        /// <summary>Время игры, секунды: забеги плюс Лагерь между ними.</summary>
        public double PlaySeconds;

        /// <summary>Накопленные Монеты (без трат) после каждого забега: (секунды игры, Монеты).</summary>
        public readonly List<(double seconds, int coins)> CoinTimeline = new List<(double, int)>();

        /// <summary>Когда собран первый Идол/Тотем редкости, секунды игры; нет ключа — не собран.</summary>
        public readonly Dictionary<RelicRarity, double> FirstItemAssembled = new Dictionary<RelicRarity, double>();

        public double? FirstRelicSeconds;

        /// <summary>Секунды игры, когда накоплено столько Монет; null — не накоплено.</summary>
        public double? SecondsToCoins(int coins)
        {
            foreach (var point in CoinTimeline)
                if (point.coins >= coins) return point.seconds;
            return null;
        }
    }

    public static class PlaytestCareer
    {
        /// <summary>ЧЕРНОВОЕ: время в Лагере между забегами и на открытие одного Саркофага, секунды.</summary>
        public static float BetweenRunsSeconds = 20f;
        public static float PerSarcophagusSeconds = 6f;

        /// <summary>Играет, пока не наберётся <paramref name="hours"/> часов игры (или <paramref name="maxRuns"/> забегов).</summary>
        public static CareerResult Play(BotPolicyKind policy, int seed, double hours, int maxRuns = int.MaxValue)
        {
            var profile = new PlaytestProfile(seed);
            var botRng = new Random(unchecked(seed * 31 + (int)policy));
            var career = new CareerResult { Policy = policy };
            for (var i = 0; i < maxRuns && career.PlaySeconds < hours * 3600.0; i++)
            {
                var summary = PlayRun(policy, unchecked(seed * 7919 + i * 104729 + (int)policy), profile, botRng);
                career.PlaySeconds += summary.Seconds + BetweenRunsSeconds;
                OpenSarcophagi(profile, summary, career);
                career.Runs.Add(summary);
                career.CoinTimeline.Add((career.PlaySeconds, profile.Coins.Balance));
            }

            return career;
        }

        public static RunSummary PlayRun(BotPolicyKind policy, int runSeed, PlaytestProfile profile, Random botRng)
        {
            using var run = new PlaytestRun(runSeed, profile);
            new PlaytestBot(run, BotPolicyConfig.For(policy), profile, botRng).Play();
            return Summarize(policy, run);
        }

        public static RunSummary Summarize(BotPolicyKind policy, PlaytestRun run) => new RunSummary
        {
            Policy = policy,
            Seed = run.Seed,
            Outcome = run.Outcome,
            Seconds = run.Now,
            Steps = run.Steps,
            FateRolls = run.FateRollsUsed,
            DeathCause = run.DeathCause,
            DeathYarus = run.DeathYarus,
            Mana = run.ManaConverted,
            CoinsFromMana = run.CoinsFromMana,
            SarcophagiTaken = run.SarcophagiTaken,
            SarcophagiEvacuated = run.SarcophagiEvacuated,
            SarcophagiLost = run.SarcophagiLost,
            Yarus = Enumerable.Range(0, GodTable.YarusCount + 1).Select(run.Yarus).ToArray(),
            Hazards = run.Hazards,
        };

        // Открывает всё вынесенное, как SarcophagusOpeningFlow: по одному, при выборе части берёт первую.
        private static void OpenSarcophagi(PlaytestProfile profile, RunSummary summary, CareerResult career)
        {
            var camp = profile.Camp;
            while (camp.UnopenedSarcophagi.Count > 0)
            {
                var contents = camp.OpenNext();
                if (!contents.HasValue) break;
                career.PlaySeconds += PerSarcophagusSeconds;
                var loot = contents.Value.Loot;
                summary.CoinsFromSarcophagi += loot.Coins;
                if (camp.PendingPartChoice != null) camp.ChoosePart(camp.PendingPartChoice[0].Id);
                if (loot.Kind == SarcophagusLootKind.Part) summary.Parts++;
                if (loot.Kind == SarcophagusLootKind.Relic)
                {
                    summary.Relics++;
                    career.FirstRelicSeconds ??= career.PlaySeconds;
                }

                foreach (var item in CollectibleCatalog.All)
                    if (camp.Progress.IsAssembled(item) && !career.FirstItemAssembled.ContainsKey(item.Rarity))
                        career.FirstItemAssembled[item.Rarity] = career.PlaySeconds;
            }
        }
    }
}
