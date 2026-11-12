using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Burmalda.Artifacts;
using UnityEditor;
using UnityEngine;

namespace Burmalda.Playtest
{
    /// <summary>
    /// Отчёт бота-плейтестера одной командой: <c>scripts/playtest-bot.sh</c> (см. docs/wiki/playtest-bot.md)
    /// или меню Burmalda → Playtest Bot Report. Таблицы — Markdown.
    /// </summary>
    public static class PlaytestReport
    {
        public const int DefaultPlayers = 20;
        public const double DefaultHours = 2.0;
        public const int DefaultSeed = 1;
        public const int ReportedYarusCount = 3;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [MenuItem("Burmalda/Playtest Bot Report")]
        public static void RunFromMenu()
        {
            var path = Path.Combine(Path.GetTempPath(), "burmalda-playtest-report.md");
            File.WriteAllText(path, Build(DefaultPlayers, DefaultHours, DefaultSeed));
            Debug.Log($"[playtest] отчёт: {path}");
            EditorUtility.RevealInFinder(path);
        }

        /// <summary>Точка входа для <c>-executeMethod</c>: аргументы −playtestPlayers, −playtestHours, −playtestSeed, −playtestOut.</summary>
        public static void RunFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string name, string fallback)
            {
                var i = Array.IndexOf(args, name);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
            }

            var players = int.Parse(Arg("-playtestPlayers", DefaultPlayers.ToString(Inv)), Inv);
            var hours = double.Parse(Arg("-playtestHours", DefaultHours.ToString(Inv)), Inv);
            var seed = int.Parse(Arg("-playtestSeed", DefaultSeed.ToString(Inv)), Inv);
            var output = Arg("-playtestOut", Path.Combine(Path.GetTempPath(), "burmalda-playtest-report.md"));
            File.WriteAllText(output, Build(players, hours, seed));
            Debug.Log($"[playtest] отчёт: {output}");
        }

        public static List<CareerResult> PlayAll(BotPolicyKind policy, int players, double hours, int seed) =>
            Enumerable.Range(0, players).Select(p => PlaytestCareer.Play(policy, unchecked(seed * 1000003 + p), hours)).ToList();

        public static string Build(int players, double hours, int seed)
        {
            var policies = (BotPolicyKind[])Enum.GetValues(typeof(BotPolicyKind));
            var careers = policies.ToDictionary(p => p, p => PlayAll(p, players, hours, seed));
            var text = new StringBuilder();
            text.AppendLine("# Отчёт бота-плейтестера");
            text.AppendLine();
            text.AppendLine($"Игроков на политику: {players}, часов игры на игрока: {F(hours, 1)}, seed: {seed}. Числа политик — черновые.");
            text.AppendLine();
            AppendRunTable(text, careers);
            AppendYarusTable(text, careers);
            AppendCauseTable(text, careers);
            return text.ToString();
        }

        private static void AppendRunTable(StringBuilder text, Dictionary<BotPolicyKind, List<CareerResult>> careers)
        {
            text.AppendLine("## Забеги и доход");
            text.AppendLine();
            text.AppendLine("| Политика | Забегов | Эвакуация | Смерть | Застрял | Мин/забег | Мана/забег | Монет/забег | Монет/час | Саркофагов взято / вынесено / потеряно за забег | Частей/час | Реликвий/час | Бросков/забег |");
            text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var (policy, list) in careers.Select(kv => (kv.Key, kv.Value)))
            {
                var runs = list.SelectMany(c => c.Runs).ToList();
                var n = Math.Max(1, runs.Count);
                var playHours = list.Sum(c => c.PlaySeconds) / 3600.0;
                double Share(RunOutcome o) => 100.0 * runs.Count(r => r.Outcome == o) / n;
                text.AppendLine(string.Join(" | ", "| " + policy, runs.Count,
                    F(Share(RunOutcome.Evacuated), 0) + "%", F(Share(RunOutcome.Died), 0) + "%", F(Share(RunOutcome.Stuck), 1) + "%",
                    F(runs.Sum(r => r.Seconds) / n / 60.0, 1),
                    F(runs.Sum(r => (double)r.Mana) / n, 0),
                    F(runs.Sum(r => (double)r.Coins) / n, 0),
                    F(runs.Sum(r => (double)r.Coins) / playHours, 0),
                    $"{F(runs.Sum(r => (double)r.SarcophagiTaken) / n, 2)} / {F(runs.Sum(r => (double)r.SarcophagiEvacuated) / n, 2)} / {F(runs.Sum(r => (double)r.SarcophagiLost) / n, 2)}",
                    F(runs.Sum(r => r.Parts) / playHours, 2),
                    F(runs.Sum(r => r.Relics) / playHours, 2),
                    F(runs.Sum(r => (double)r.FateRolls) / n, 2)) + " |");
            }

            text.AppendLine();
        }

        private static void AppendYarusTable(StringBuilder text, Dictionary<BotPolicyKind, List<CareerResult>> careers)
        {
            text.AppendLine("## По Ярусам");
            text.AppendLine();
            text.AppendLine("Выживание — доля вошедших на Ярус, кто прошёл его Святилище (с учётом Бросков Судьбы). «Чисто» — без единой смерти на Ярусе.");
            text.AppendLine();
            text.AppendLine("| Политика | Ярус | Бог | Вошли | Выживание | Чисто | Сек на Ярус | из них Святилище | Смертей на вход: тоннель / Святилище | Бросков на вход | Саркофаг взят |");
            text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var (policy, list) in careers.Select(kv => (kv.Key, kv.Value)))
                for (var yarus = 1; yarus <= ReportedYarusCount; yarus++)
                {
                    var records = list.SelectMany(c => c.Runs).Select(r => r.Yarus[yarus]).Where(y => y.Entered).ToList();
                    if (records.Count == 0) continue;
                    var cleared = records.Where(y => y.Cleared).ToList();
                    var clearedCount = Math.Max(1, cleared.Count);
                    text.AppendLine(string.Join(" | ", "| " + policy, yarus, GodTable.ForYarus(yarus).God, records.Count,
                        F(100.0 * cleared.Count / records.Count, 0) + "%",
                        F(100.0 * cleared.Count(y => y.Hazards == 0) / records.Count, 0) + "%",
                        F(cleared.Sum(y => y.Seconds) / clearedCount, 0),
                        F(cleared.Sum(y => y.SanctuarySeconds) / clearedCount, 0),
                        $"{F(records.Sum(y => (double)(y.Hazards - y.SanctuaryHazards)) / records.Count, 2)} / {F(records.Sum(y => (double)y.SanctuaryHazards) / records.Count, 2)}",
                        F(records.Sum(y => (double)y.FateRolls) / records.Count, 2),
                        F(100.0 * records.Count(y => y.SarcophagusTaken) / records.Count, 0) + "%") + " |");
                }

            text.AppendLine();
        }

        private static void AppendCauseTable(StringBuilder text, Dictionary<BotPolicyKind, List<CareerResult>> careers)
        {
            var causes = (HazardCause[])Enum.GetValues(typeof(HazardCause));
            text.AppendLine("## Смерти по причинам");
            text.AppendLine();
            text.AppendLine("Все смерти (до Броска Судьбы) / из них окончательные. Rock — Камень, Wall — Давилка и Стены, Arrows — Стрелы, Lava — шаг на Лаву, Decay — плита под ногами распалась, Collapse — обрушение Святилища.");
            text.AppendLine();
            text.AppendLine("| Политика | Ярус | " + string.Join(" | ", causes) + " |");
            text.AppendLine("|---|---|" + string.Concat(causes.Select(_ => "---|")));
            foreach (var (policy, list) in careers.Select(kv => (kv.Key, kv.Value)))
                for (var yarus = 1; yarus <= ReportedYarusCount; yarus++)
                {
                    var runs = list.SelectMany(c => c.Runs).ToList();
                    var hazards = runs.SelectMany(r => r.Hazards).Where(h => h.Yarus == yarus).ToList();
                    if (hazards.Count == 0 && !runs.Any(r => r.Yarus[yarus].Entered)) continue;
                    var cells = causes.Select(cause =>
                        $"{hazards.Count(h => h.Cause == cause)} / {runs.Count(r => r.Outcome == RunOutcome.Died && r.DeathYarus == yarus && r.DeathCause == cause)}");
                    text.AppendLine($"| {policy} | {yarus} | " + string.Join(" | ", cells) + " |");
                }

            text.AppendLine();
        }

        internal static string F(double value, int digits) => value.ToString("F" + digits, Inv);
    }
}
