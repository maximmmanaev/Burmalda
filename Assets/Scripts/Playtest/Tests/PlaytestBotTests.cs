using System;
using System.Collections.Generic;
using System.Linq;
using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Playtest.Tests
{
    public class PlaytestBotTests
    {
        private static readonly BotPolicyKind[] Policies = { BotPolicyKind.Cautious, BotPolicyKind.Greedy, BotPolicyKind.Skilled };

        [TearDown]
        public void TearDown() => Assert.AreSame(RunEffects.Neutral, RunEffects.Current, "Забег бота обязан снять свои слои эффектов.");

        private static List<string> Fingerprints(BotPolicyKind policy, int seed) =>
            PlaytestCareer.Play(policy, seed, hours: 10, maxRuns: 2).Runs.Select(r => r.Fingerprint()).ToList();

        [Test]
        public void SameSeed_ReproducesRunsExactly([ValueSource(nameof(Policies))] BotPolicyKind policy)
        {
            CollectionAssert.AreEqual(Fingerprints(policy, 11), Fingerprints(policy, 11));
        }

        [Test]
        public void DifferentSeeds_GiveDifferentRuns([ValueSource(nameof(Policies))] BotPolicyKind policy)
        {
            CollectionAssert.AreNotEqual(Fingerprints(policy, 11), Fingerprints(policy, 12));
        }

        // Бот ходит только по правилам: на соседнюю плиту, не в стену и не в закрытые Ворота, не чаще шага.
        [Test]
        public void Bot_NeverWalksThroughWalls_AndRespectsStepLock([ValueSource(nameof(Policies))] BotPolicyKind policy)
        {
            for (var seed = 1; seed <= 2; seed++)
            {
                var profile = new PlaytestProfile(seed);
                using var run = new PlaytestRun(seed, profile);
                var lastStepTime = float.NegativeInfinity;
                var steps = 0;
                run.Stepped += (from, to) =>
                {
                    steps++;
                    Assert.IsTrue(from.IsAdjacentTo(to), $"Шаг {from} → {to} не на соседнюю плиту.");
                    Assert.IsTrue(run.Grid.TryGetTile(to, out var tile));
                    Assert.IsFalse(tile.IsBlocked, $"Шаг в стену {to}.");
                    Assert.IsFalse(tile.IsGated && !tile.IsLeverGateOpen, $"Шаг в закрытые Ворота {to}.");
                    Assert.GreaterOrEqual(run.Now - lastStepTime, PlaytestRun.StepSeconds - 0.001f, "Шаг раньше, чем разблокирован ввод.");
                    lastStepTime = run.Now;
                };
                new PlaytestBot(run, BotPolicyConfig.For(policy), profile, new Random(seed)).Play();

                Assert.Greater(steps, 20, "Бот должен был пройти заметную часть тоннеля.");
                Assert.AreEqual(steps, run.Steps);
                Assert.AreNotEqual(RunOutcome.Running, run.Outcome);
            }
        }

        [Test]
        public void Runs_EndByGameRules([ValueSource(nameof(Policies))] BotPolicyKind policy)
        {
            var career = PlaytestCareer.Play(policy, seed: 3, hours: 10, maxRuns: 5);

            foreach (var run in career.Runs)
            {
                Assert.AreNotEqual(RunOutcome.Stuck, run.Outcome, $"Бот застрял (seed {run.Seed}).");
                Assert.LessOrEqual(run.FateRolls, 3, "Не больше трёх Бросков Судьбы за экспедицию (§16.1).");
                Assert.AreEqual(run.SarcophagiTaken, run.SarcophagiEvacuated + run.SarcophagiLost, "Каждый взятый Саркофаг либо вынесен, либо потерян.");
                if (run.Outcome == RunOutcome.Died)
                {
                    Assert.AreEqual(0, run.Coins, "Окончательная смерть: теряются Мана и все Саркофаги (§15.3).");
                    Assert.AreEqual(0, run.SarcophagiEvacuated);
                    Assert.IsNotNull(run.DeathCause);
                }

                Assert.IsFalse(run.Yarus[4].Entered, "При Лагере 1 дверь после Яруса 3 закрыта (§6.2).");
            }
        }

        [Test]
        public void Cautious_EvacuatesWithTheFirstSarcophagus()
        {
            var career = PlaytestCareer.Play(BotPolicyKind.Cautious, seed: 5, hours: 10, maxRuns: 6);

            Assert.IsTrue(career.Runs.Any(r => r.Outcome == RunOutcome.Evacuated));
            foreach (var run in career.Runs.Where(r => r.Outcome == RunOutcome.Evacuated && r.SarcophagiEvacuated > 0))
            {
                Assert.AreEqual(1, run.SarcophagiEvacuated);
                Assert.IsFalse(run.Yarus[2].Entered, "Осторожный уходит с Алтаря в конце первого Святилища.");
            }
        }

        [Test]
        public void Greedy_GoesDeeperThanCautious()
        {
            var greedy = PlaytestCareer.Play(BotPolicyKind.Greedy, seed: 5, hours: 10, maxRuns: 12);

            Assert.IsTrue(greedy.Runs.Any(r => r.Yarus[2].Entered), "Жадный идёт к следующему Святилищу, пока несёт меньше трёх Саркофагов.");
        }

        [Test]
        public void Career_OpensSarcophagi_FirstOneGivesAPart()
        {
            var career = PlaytestCareer.Play(BotPolicyKind.Cautious, seed: 5, hours: 10, maxRuns: 6);

            var firstWithLoot = career.Runs.First(r => r.SarcophagiEvacuated > 0);
            Assert.AreEqual(1, firstWithLoot.Parts, "Первый вынесенный Саркофаг всегда содержит часть (§21.2).");
            Assert.Greater(firstWithLoot.CoinsFromSarcophagi, 0);
            Assert.Greater(career.PlaySeconds, career.Runs.Sum(r => r.Seconds), "Время игры включает Лагерь между забегами.");
            Assert.AreEqual(career.Runs.Count, career.CoinTimeline.Count);
        }
    }

    public class PlaytestRunTests
    {
        private PlaytestRun _run;

        [SetUp]
        public void SetUp() => _run = new PlaytestRun(1, new PlaytestProfile(1));

        [TearDown]
        public void TearDown() => _run.Dispose();

        [Test]
        public void Step_ToNonAdjacentTile_IsRejected()
        {
            var start = _run.Trail.CurrentPosition;

            Assert.IsFalse(_run.Step(new GridCoordinate(start.Row + 2, start.Column)));
            Assert.AreEqual(start, _run.Trail.CurrentPosition);
            Assert.AreEqual(0, _run.Steps);
        }

        [Test]
        public void Step_TakesStepDuration_OfGameTime()
        {
            var start = _run.Trail.CurrentPosition;

            Assert.IsTrue(_run.Step(new GridCoordinate(start.Row + 1, start.Column)));
            Assert.AreEqual(PlaytestRun.StepSeconds, _run.Now, 0.001f);
        }

        [Test]
        public void Evacuate_OffAltar_IsRejected()
        {
            Assert.IsFalse(_run.CanEvacuate);
            Assert.IsFalse(_run.Evacuate());
            Assert.AreEqual(RunOutcome.Running, _run.Outcome);
        }

        // Стоять на месте нельзя бесконечно: плита под ногами распадается, время идёт (§7.2, §7.5).
        [Test]
        public void StandingStill_OnATrailTile_EndsInDecayDeath()
        {
            var start = _run.Trail.CurrentPosition;
            _run.Step(new GridCoordinate(start.Row + 1, start.Column));

            _run.Advance(120f);

            Assert.IsNotEmpty(_run.Hazards);
            Assert.AreEqual(HazardCause.Decay, _run.Hazards[0].Cause);
        }

        [Test]
        public void TrapPrior_IsAProbabilityDistribution()
        {
            foreach (var prior in new[] { TrapPrior.Tunnel, TrapPrior.Sanctuary })
                Assert.AreEqual(1.0, prior.Rock + prior.Arrows + prior.Crusher + prior.WallLeft + prior.WallRight, 1e-9);
        }

        [Test]
        public void Report_ContainsEveryPolicy()
        {
            var report = PlaytestReport.Build(players: 1, hours: 0.02, seed: 1);

            foreach (var policy in Enum.GetNames(typeof(BotPolicyKind))) StringAssert.Contains("| " + policy + " |", report);
            StringAssert.Contains("## По Ярусам", report);
        }
    }
}
