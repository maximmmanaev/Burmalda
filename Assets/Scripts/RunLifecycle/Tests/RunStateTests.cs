using Burmalda.Core;
using Burmalda.FateRoll;
using Burmalda.Decay;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.RunLifecycle.Tests
{
    public class RunStateTests
    {
        private const int Width = 5;

        // По умолчанию все три Броска Судьбы уже потрачены (PRD v10 §16.1: после
        // третьего следующая смерть окончательна) — опасность убивает сразу, как в
        // тестах, написанных до Броска Судьбы. Тесты самого броска передают
        // fateRollsUsed < 3 и последовательность кубиков.
        private static (TunnelGrid grid, GridTraceTrail trail, TrailDecaySystem decay, RunState runState) CreateRun(
            int fateRollsUsed = FateRollRules.MaxRolls, params int[] dice)
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var decay = new TrailDecaySystem(grid, trail);
            var next = 0;
            var runState = new RunState(grid, trail, decay, () => dice[next++ % dice.Length], fateRollsUsed);
            return (grid, trail, decay, runState);
        }

        [Test]
        public void Constructor_NewRun_IsAlive()
        {
            var (_, _, _, runState) = CreateRun();

            Assert.IsTrue(runState.IsAlive);
        }

        [Test]
        public void LethalTrapTriggered_Lava_SetsIsAliveFalseAndFiresDied()
        {
            var (grid, trail, _, runState) = CreateRun();
            var lava = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(lava).MarkLethalTrap(LethalTrapType.Lava);
            string firedReason = null;
            runState.Died += reason => firedReason = reason;

            trail.TryAdvanceTo(lava);

            Assert.IsFalse(runState.IsAlive);
            Assert.IsNotNull(firedReason);
        }

        [Test]
        public void TryAdvanceTo_Lava_StepSucceedsBeforeHazardIsResolved_NotABlockedWall()
        {
            // issue #258: «Лава должна быть проходимой, но летальной, а не
            // физической стеной» — в отличие от четырёх рантайм-типов
            // ловушек, шаг на Лаву больше не отклоняется TryAdvanceTo;
            // проигрыш наступает через тот же ResolveHazard, что и для
            // прочих ловушек (здесь — броски исчерпаны), не в обход него.
            var (grid, trail, _, runState) = CreateRun(); // броски исчерпаны — смерть сразу
            var lava = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(lava).MarkLethalTrap(LethalTrapType.Lava);

            var advanced = trail.TryAdvanceTo(lava);

            Assert.IsTrue(advanced, "Шаг на Лаву должен физически засчитываться, а не отклоняться как раньше.");
            Assert.AreEqual(lava, trail.CurrentPosition, "Игрок должен фактически оказаться на плите Лавы, а не остаться перед ней.");
            Assert.IsFalse(runState.IsAlive, "Разрешение опасности всё равно должно сработать после шага.");
        }

        [Test]
        public void LethalTrapTriggered_ArrowWave_SetsIsAliveFalseAndFiresDiedWithOwnReason()
        {
            // issue #213: новый LethalTrapType не должен провалиться в
            // default ветку switch-выражения DescribeLethalTrap.
            var (grid, trail, _, runState) = CreateRun();
            var arrowTile = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(arrowTile).TransitionToLethalTrap(LethalTrapType.ArrowWave);
            string firedReason = null;
            runState.Died += reason => firedReason = reason;

            trail.TryAdvanceTo(arrowTile);

            Assert.IsFalse(runState.IsAlive);
            Assert.AreEqual("Пронзён стрелой", firedReason);
        }

        [Test]
        public void LethalTrapTriggered_BombBlast_SetsIsAliveFalseAndFiresDiedWithOwnReason()
        {
            // issue #214: тот же регресс-тест, что и для ArrowWave выше.
            var (grid, trail, _, runState) = CreateRun();
            var bombTile = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(bombTile).TransitionToLethalTrap(LethalTrapType.BombBlast);
            string firedReason = null;
            runState.Died += reason => firedReason = reason;

            trail.TryAdvanceTo(bombTile);

            Assert.IsFalse(runState.IsAlive);
            Assert.AreEqual("Подорвался на бомбе", firedReason);
        }

        [Test]
        public void LethalTrapTriggered_BladeTact_SetsIsAliveFalseAndFiresDiedWithOwnReason()
        {
            // issue #215: тот же регресс-тест, что и для ArrowWave выше.
            var (grid, trail, _, runState) = CreateRun();
            var bladeTile = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(bladeTile).TransitionToLethalTrap(LethalTrapType.BladeTact);
            string firedReason = null;
            runState.Died += reason => firedReason = reason;

            trail.TryAdvanceTo(bladeTile);

            Assert.IsFalse(runState.IsAlive);
            Assert.AreEqual("Разрублен лезвием", firedReason);
        }

        [Test]
        public void LethalTrapTriggered_RuntimeTransitionToLava_SetsIsAliveFalseAndFiresDiedWithOwnReason()
        {
            // issue #216, переименован issue #262: раньше проверял отдельный
            // C#-идентификатор LethalTrapType.LavaWave (волна Лавы,
            // Movement.LavaWaveTrapSystem) — владелец слил его с
            // LethalTrapType.Lava (см. её doc-комментарий), отдельного
            // значения для волны больше нет. Тест сохранён не как дубль
            // LethalTrapTriggered_Lava_... выше (тот идёт через MarkLethalTrap,
            // генерация) — этот целенаправленно проверяет РАНТАЙМ-переход
            // (TransitionToLethalTrap, тот же путь, что использует
            // LavaWaveTrapSystem) приводит к тому же исходу.
            var (grid, trail, _, runState) = CreateRun();
            var lavaTile = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(lavaTile).TransitionToLethalTrap(LethalTrapType.Lava);
            string firedReason = null;
            runState.Died += reason => firedReason = reason;

            trail.TryAdvanceTo(lavaTile);

            Assert.IsFalse(runState.IsAlive);
            Assert.AreEqual("Сгорел в лаве", firedReason);
        }

        [Test]
        public void TileDestroyed_UnderCurrentPosition_SetsIsAliveFalseAndFiresDied()
        {
            var (grid, trail, decay, runState) = CreateRun();
            var target = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(target); // порог 3.09с — текущая позиция игрока

            var firedCount = 0;
            runState.Died += _ => firedCount++;

            decay.Tick(1000f); // гарантированно разрушает плиту под ногами

            Assert.IsTrue(grid.GetOrCreateTile(target).IsDestroyed, "тест некорректен, если плита не разрушилась");
            Assert.IsFalse(runState.IsAlive);
            Assert.AreEqual(1, firedCount);
        }

        [Test]
        public void TileDestroyed_NotUnderCurrentPosition_DoesNotDie()
        {
            // Пороги распада соседних по трейлу плиток отличаются всего на
            // DecayThresholdSecondsPerTrailIndex (доли секунды) — единственный
            // способ разрушить именно `behind`, не задев текущую плиту: дать
            // `behind` фору по времени распада ДО того, как игрок сместится
            // на `current` (у которой отсчёт стартует с нуля), а не бить одним
            // огромным Tick — он продвигает распад одинаково для всех активных
            // плиток трейла и разрушил бы обе сразу.
            var (grid, trail, decay, runState) = CreateRun();
            var behind = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(behind);
            decay.Tick(2f); // фора для behind, но меньше её порога (3.09с)

            var current = new GridCoordinate(2, 2);
            trail.TryAdvanceTo(current); // текущая позиция теперь не behind, распад current стартует с нуля
            decay.Tick(1.2f); // добивает behind (была фора), current остаётся далеко от своего порога

            Assert.IsTrue(grid.GetOrCreateTile(behind).IsDestroyed, "тест некорректен, если плита не разрушилась");
            Assert.IsFalse(grid.GetOrCreateTile(current).IsDestroyed, "тест некорректен, если текущая плита тоже разрушилась");
            Assert.IsTrue(runState.IsAlive, "разрушение НЕ текущей плиты не должно убивать игрока");
        }

        [Test]
        public void Die_CalledTwice_FiresDiedOnlyOnce()
        {
            var (grid, trail, decay, runState) = CreateRun();
            var target = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(target);
            var firedCount = 0;
            runState.Died += _ => firedCount++;

            decay.Tick(1000f); // первая смерть — плита под ногами
            decay.Tick(1000f); // повторный тик — уже мёртв, повторного события быть не должно

            Assert.AreEqual(1, firedCount);
        }

        [Test]
        public void Dispose_StopsReactingToFurtherLethalTrapTriggers()
        {
            var (grid, trail, _, runState) = CreateRun();
            runState.Dispose();
            var lava = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(lava).MarkLethalTrap(LethalTrapType.Lava);

            trail.TryAdvanceTo(lava);

            Assert.IsTrue(runState.IsAlive, "после Dispose RunState не должен реагировать на события трейла");
        }

        private static GridCoordinate AddLava(TunnelGrid grid, int row = 1)
        {
            var lava = new GridCoordinate(row, 2);
            grid.GetOrCreateTile(lava).MarkLethalTrap(LethalTrapType.Lava);
            return lava;
        }

        [Test]
        public void Hazard_WithRollsLeft_OffersFateRollAndStaysAliveUntilDecision()
        {
            var (grid, trail, _, runState) = CreateRun(fateRollsUsed: 0, 3, 3);
            int? offeredIndex = null;
            var died = false;
            runState.FateRollOffered += i => offeredIndex = i;
            runState.Died += _ => died = true;

            trail.TryAdvanceTo(AddLava(grid));

            Assert.IsTrue(runState.IsAwaitingFate);
            Assert.AreEqual(1, offeredIndex);
            Assert.IsTrue(runState.IsAlive);
            Assert.IsFalse(died);
        }

        [Test]
        public void DeclineFate_AfterOffer_IsFinalDeathWithReason()
        {
            var (grid, trail, _, runState) = CreateRun(fateRollsUsed: 0, 3, 3);
            string reason = null;
            runState.Died += r => reason = r;
            trail.TryAdvanceTo(AddLava(grid));

            runState.DeclineFate();

            Assert.IsFalse(runState.IsAlive);
            Assert.IsFalse(runState.IsAwaitingFate);
            Assert.AreEqual("Сгорел в лаве", reason);
        }

        [Test]
        public void RollFate_InPlace_ResumesRunOnSameTile()
        {
            var (grid, trail, _, runState) = CreateRun(fateRollsUsed: 0, 6, 6);
            var lava = AddLava(grid);
            trail.TryAdvanceTo(lava);
            FateRollResult? result = null;
            runState.FateRolled += r => result = r;

            var ok = runState.RollFate();

            Assert.IsTrue(ok);
            Assert.AreEqual(FateOutcome.InPlace, result.Value.Outcome);
            Assert.IsTrue(runState.IsAlive);
            Assert.IsFalse(runState.IsAwaitingFate);
            Assert.AreEqual(lava, trail.CurrentPosition);
            Assert.AreEqual(1, runState.FateRollsUsed);
        }

        [Test]
        public void RollFate_ToAltar_TeleportsToLastVisitedAltarAndDoesNotRestoreRoute()
        {
            var (grid, trail, _, runState) = CreateRun(fateRollsUsed: 0, 4, 4); // 8 — к Алтарю на броске I
            var altar = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(altar).MarkAltar();
            trail.TryAdvanceTo(altar);
            var passed = new GridCoordinate(2, 2);
            trail.TryAdvanceTo(passed);
            grid.GetOrCreateTile(passed).AdvanceDecay(1000f); // пройденная плита (распад начат при шаге) обрушена в Лаву
            trail.TryAdvanceTo(AddLava(grid, 3));

            runState.RollFate();

            Assert.IsTrue(runState.IsAlive);
            Assert.AreEqual(altar, trail.CurrentPosition);
            Assert.IsTrue(grid.GetOrCreateTile(passed).IsDestroyed, "разрушенный маршрут не восстанавливается");
        }

        [Test]
        public void RollFate_ToAltar_WithoutAltarVisited_TeleportsToStart()
        {
            var (grid, trail, _, runState) = CreateRun(fateRollsUsed: 0, 4, 4);
            var start = trail.CurrentPosition;
            trail.TryAdvanceTo(AddLava(grid));

            runState.RollFate();

            Assert.AreEqual(start, trail.CurrentPosition);
            Assert.IsTrue(runState.IsAlive);
        }

        [Test]
        public void RollFate_Death_IsFinalDeath()
        {
            var (grid, trail, _, runState) = CreateRun(fateRollsUsed: 0, 2, 3); // 5 — смерть
            string reason = null;
            runState.Died += r => reason = r;
            trail.TryAdvanceTo(AddLava(grid));

            runState.RollFate();

            Assert.IsFalse(runState.IsAlive);
            Assert.IsNotNull(reason);
        }

        [Test]
        public void ThreeRolls_ThenNextHazardIsFinalWithoutOffer()
        {
            var (grid, trail, _, runState) = CreateRun(fateRollsUsed: 0, 6, 6); // всегда «на месте»
            var offers = 0;
            runState.FateRollOffered += _ => offers++;
            var lava = AddLava(grid);
            trail.TryAdvanceTo(lava);
            runState.RollFate(); // 1
            trail.TryAdvanceTo(new GridCoordinate(1, 3));
            Assert.IsFalse(runState.IsAwaitingFate);
            for (var i = 2; i <= 3; i++)
            {
                trail.TeleportTo(new GridCoordinate(0, 2));
                trail.TryAdvanceTo(lava);
                Assert.IsTrue(runState.IsAwaitingFate, $"бросок {i} должен быть предложен");
                runState.RollFate();
            }
            Assert.AreEqual(3, runState.FateRollsUsed);

            trail.TeleportTo(new GridCoordinate(0, 2));
            trail.TryAdvanceTo(lava);

            Assert.IsFalse(runState.IsAlive, "после третьего броска следующая смерть окончательна");
            Assert.AreEqual(3, offers);
        }

        [Test]
        public void RollFate_UsesRollIndexForTable()
        {
            // 4+4=8: на броске I — к Алтарю, на броске II — смерть (≤ 8).
            var (grid, trail, _, runState) = CreateRun(fateRollsUsed: 1, 4, 4);
            trail.TryAdvanceTo(AddLava(grid));

            runState.RollFate();

            Assert.IsFalse(runState.IsAlive);
        }

        [Test]
        public void RollFate_WithoutPendingOffer_DoesNothing()
        {
            var (_, _, _, runState) = CreateRun(fateRollsUsed: 0, 6, 6);

            Assert.IsFalse(runState.RollFate());
            Assert.AreEqual(0, runState.FateRollsUsed);
        }

        [Test]
        public void RollFate_ModifierShiftsSum()
        {
            var (grid, trail, _, runState) = CreateRun(fateRollsUsed: 0, 5, 5); // 10: к Алтарю без модификатора
            runState.FateModifier = 2; // 12 — на месте
            var lava = AddLava(grid);
            trail.TryAdvanceTo(lava);

            runState.RollFate();

            Assert.AreEqual(lava, trail.CurrentPosition);
            Assert.IsTrue(runState.IsAlive);
        }

        [Test]
        public void RollFate_PaymentStubDisabled_DoesNotRoll()
        {
            var old = FateRollRules.FateRollPaymentStub;
            try
            {
                FateRollRules.FateRollPaymentStub = false;
                var (grid, trail, _, runState) = CreateRun(fateRollsUsed: 0, 6, 6);
                trail.TryAdvanceTo(AddLava(grid));

                Assert.IsFalse(runState.RollFate());
                Assert.AreEqual(0, runState.FateRollsUsed);
                Assert.IsTrue(runState.IsAwaitingFate);
            }
            finally
            {
                FateRollRules.FateRollPaymentStub = old;
            }
        }

        [Test]
        public void TileDestroyedUnderPlayer_WithRollsLeft_OffersFateRoll()
        {
            var (grid, trail, decay, runState) = CreateRun(fateRollsUsed: 0, 6, 6);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            decay.Tick(1000f);

            Assert.IsTrue(runState.IsAwaitingFate);
        }

        [Test]
        public void ReportSanctuaryCollapse_WithRollsLeft_OffersFateRoll()
        {
            var (_, _, _, runState) = CreateRun(fateRollsUsed: 0, 6, 6);

            runState.ReportSanctuaryCollapse();

            Assert.IsTrue(runState.IsAwaitingFate);
            Assert.IsTrue(runState.IsAlive);
        }

        [Test]
        public void ReportSanctuaryCollapse_RollsExhausted_DiesWithOwnReason()
        {
            var (_, _, _, runState) = CreateRun();
            string firedReason = null;
            runState.Died += reason => firedReason = reason;

            runState.ReportSanctuaryCollapse();

            Assert.IsFalse(runState.IsAlive);
            Assert.AreEqual("Пол Святилища обрушился", firedReason);
        }

        [Test]
        public void ReportSanctuaryCollapse_WhileAwaitingFate_DoesNotOfferSecondRoll()
        {
            var (_, _, _, runState) = CreateRun(fateRollsUsed: 0, 6, 6);
            var offers = 0;
            runState.FateRollOffered += _ => offers++;

            runState.ReportSanctuaryCollapse();
            runState.ReportSanctuaryCollapse();

            Assert.AreEqual(1, offers);
        }

        [Test]
        public void ReportFallingRockCrush_WithRollsLeft_OffersFateRoll()
        {
            var (_, _, _, runState) = CreateRun(fateRollsUsed: 0, 6, 6);

            runState.ReportFallingRockCrush();

            Assert.IsTrue(runState.IsAwaitingFate);
        }

        [Test]
        public void ReportFallingRockCrush_RollsExhausted_DiesWithOwnReason()
        {
            var (_, _, _, runState) = CreateRun();
            string firedReason = null;
            runState.Died += reason => firedReason = reason;

            runState.ReportFallingRockCrush();

            Assert.IsFalse(runState.IsAlive);
            Assert.AreEqual("Раздавлен камнем", firedReason);
        }

        [Test]
        public void ReportFallingRockCrush_CalledAfterAlreadyDead_DoesNotFireDiedAgain()
        {
            var (grid, trail, _, runState) = CreateRun();
            trail.TryAdvanceTo(AddLava(grid)); // уже мёртв
            var firedCount = 0;
            runState.Died += _ => firedCount++;

            runState.ReportFallingRockCrush();

            Assert.AreEqual(0, firedCount);
        }
    }
}
