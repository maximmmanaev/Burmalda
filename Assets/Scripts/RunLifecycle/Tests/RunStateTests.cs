using Burmalda.Core;
using Burmalda.D20;
using Burmalda.Decay;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.RunLifecycle.Tests
{
    public class RunStateTests
    {
        private const int Width = 5;

        // d20Roll по умолчанию — 5, в диапазоне Death (1-9): все СУЩЕСТВУЮЩИЕ
        // тесты (написанные до issue #24) ожидают мгновенную смерть от
        // ловушки/обвала и не должны меняться при появлении d20 — только
        // новые тесты Fortune/Knockback переопределяют этот параметр.
        private static (TunnelGrid grid, GridTraceTrail trail, TrailDecaySystem decay, RunState runState) CreateRun(int d20Roll = 5)
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var decay = new TrailDecaySystem(grid, trail);
            var d20 = new D20Trial(() => d20Roll);
            var runState = new RunState(grid, trail, decay, d20);
            return (grid, trail, decay, runState);
        }

        [Test]
        public void Constructor_NewRun_IsAlive()
        {
            var (_, _, _, runState) = CreateRun();

            Assert.IsTrue(runState.IsAlive);
        }

        [Test]
        public void LethalTrapTriggered_Pit_SetsIsAliveFalseAndFiresDied()
        {
            var (grid, trail, _, runState) = CreateRun();
            var pit = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(pit).MarkLethalTrap(LethalTrapType.Pit);
            string firedReason = null;
            runState.Died += reason => firedReason = reason;

            trail.TryAdvanceTo(pit);

            Assert.IsFalse(runState.IsAlive);
            Assert.IsNotNull(firedReason);
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
        public void LethalTrapTriggered_Explosion_SetsIsAliveFalseAndFiresDiedWithOwnReason()
        {
            // Issue #10: до этого при добавлении третьего LethalTrapType
            // тернарный оператор в RunState свёл бы Explosion к сообщению
            // "Сгорел в лаве" — явная проверка сообщения ловит такой регресс.
            var (grid, trail, _, runState) = CreateRun();
            var trigger = new GridCoordinate(1, 2);
            var explosive = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkExplosiveTrapTrigger(explosive);
            grid.GetOrCreateTile(explosive).MarkLethalTrap(LethalTrapType.Explosion);
            trail.TryAdvanceTo(trigger);
            string firedReason = null;
            runState.Died += reason => firedReason = reason;

            trail.TryAdvanceTo(explosive);

            Assert.IsFalse(runState.IsAlive);
            Assert.AreEqual("Подорвался на ловушке", firedReason);
        }

        [Test]
        public void TimedTrapTriggered_Arrow_SetsIsAliveFalseAndFiresDiedWithOwnReason()
        {
            // Issue #45: плита-цель уже активна (снаряд физически проходит) — попытка шагнуть на неё смертельна.
            var (grid, trail, _, runState) = CreateRun();
            var target = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(target).ArmTimedTrap(TimedTrapType.Arrow);
            string firedReason = null;
            runState.Died += reason => firedReason = reason;

            trail.TryAdvanceTo(target);

            Assert.IsFalse(runState.IsAlive);
            Assert.AreEqual("Пронзён стрелой", firedReason);
        }

        [Test]
        public void TimedTrapTriggered_Blade_SetsIsAliveFalseAndFiresDiedWithOwnReason()
        {
            var (grid, trail, _, runState) = CreateRun();
            var target = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(target).ArmTimedTrap(TimedTrapType.Blade);
            string firedReason = null;
            runState.Died += reason => firedReason = reason;

            trail.TryAdvanceTo(target);

            Assert.IsFalse(runState.IsAlive);
            Assert.AreEqual("Разрублен лезвием", firedReason);
        }

        [Test]
        public void TimedTrapTriggered_DisarmedTarget_DoesNotKill()
        {
            // Снаряд уже прошёл (DisarmTimedTrap) — шаг на бывшую цель безопасен.
            var (grid, trail, _, runState) = CreateRun();
            var target = new GridCoordinate(1, 2);
            var tile = grid.GetOrCreateTile(target);
            tile.ArmTimedTrap(TimedTrapType.Arrow);
            tile.DisarmTimedTrap();

            var advanced = trail.TryAdvanceTo(target);

            Assert.IsTrue(advanced);
            Assert.IsTrue(runState.IsAlive);
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
            var pit = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(pit).MarkLethalTrap(LethalTrapType.Pit);

            trail.TryAdvanceTo(pit);

            Assert.IsTrue(runState.IsAlive, "после Dispose RunState не должен реагировать на события трейла");
        }

        [Test]
        public void Dispose_StopsReactingToFurtherTimedTrapTriggers()
        {
            var (grid, trail, _, runState) = CreateRun();
            runState.Dispose();
            var target = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(target).ArmTimedTrap(TimedTrapType.Arrow);

            trail.TryAdvanceTo(target);

            Assert.IsTrue(runState.IsAlive, "после Dispose RunState не должен реагировать на события трейла");
        }

        [Test]
        public void LethalTrapTriggered_D20Fortune_StaysAliveAndDoesNotFireDied()
        {
            var (grid, trail, _, runState) = CreateRun(d20Roll: 20);
            var pit = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(pit).MarkLethalTrap(LethalTrapType.Pit);
            var fired = false;
            runState.Died += _ => fired = true;

            trail.TryAdvanceTo(pit);

            Assert.IsTrue(runState.IsAlive);
            Assert.IsFalse(fired);
        }

        [Test]
        public void LethalTrapTriggered_D20Knockback_TeleportsToStartWhenNoAltarVisited()
        {
            var (grid, trail, _, runState) = CreateRun(d20Roll: 12);
            var start = trail.CurrentPosition;
            var pit = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(pit).MarkLethalTrap(LethalTrapType.Pit);

            trail.TryAdvanceTo(pit);

            Assert.IsTrue(runState.IsAlive);
            Assert.AreEqual(start, trail.CurrentPosition);
        }

        [Test]
        public void LethalTrapTriggered_D20Knockback_TeleportsToLastVisitedAltar()
        {
            var (grid, trail, _, runState) = CreateRun(d20Roll: 12);
            var altar = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(altar).MarkAltar();
            trail.TryAdvanceTo(altar);
            var pit = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(pit).MarkLethalTrap(LethalTrapType.Pit);

            trail.TryAdvanceTo(pit);

            Assert.IsTrue(runState.IsAlive);
            Assert.AreEqual(altar, trail.CurrentPosition);
        }

        [Test]
        public void D20Resolved_FiresWithRolledOutcome()
        {
            var (grid, trail, _, runState) = CreateRun(d20Roll: 20);
            var pit = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(pit).MarkLethalTrap(LethalTrapType.Pit);
            D20Outcome? seen = null;
            runState.D20Resolved += outcome => seen = outcome;

            trail.TryAdvanceTo(pit);

            Assert.AreEqual(D20Outcome.Fortune, seen);
        }

        [Test]
        public void TileDestroyed_D20Fortune_StaysAlive()
        {
            var (grid, trail, decay, runState) = CreateRun(d20Roll: 15);
            var target = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(target);

            decay.Tick(1000f);

            Assert.IsTrue(grid.GetOrCreateTile(target).IsDestroyed, "тест некорректен, если плита не разрушилась");
            Assert.IsTrue(runState.IsAlive);
        }

        [Test]
        public void ReportBossDefeat_SetsIsAliveFalseAndFiresDiedWithGivenReason_NoD20Rolled()
        {
            // #82: d20 НЕ применяется к поражению от Босса — детерминировано.
            var (_, _, _, runState) = CreateRun(d20Roll: 20); // Fortune, если бы d20 всё-таки бросился — тест поймал бы регресс
            string firedReason = null;
            runState.Died += reason => firedReason = reason;
            var d20Fired = false;
            runState.D20Resolved += _ => d20Fired = true;

            runState.ReportBossDefeat("Не хватило энергии до Босса");

            Assert.IsFalse(runState.IsAlive);
            Assert.AreEqual("Не хватило энергии до Босса", firedReason);
            Assert.IsFalse(d20Fired, "поражение от Босса детерминировано, d20 бросаться не должен");
        }

        [Test]
        public void ReportBossDefeat_CalledAfterAlreadyDead_DoesNotFireDiedAgain()
        {
            var (grid, trail, _, runState) = CreateRun(d20Roll: 5); // Death
            var pit = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(pit).MarkLethalTrap(LethalTrapType.Pit);
            trail.TryAdvanceTo(pit); // уже мёртв
            var firedCount = 0;
            runState.Died += _ => firedCount++;

            runState.ReportBossDefeat("Не хватило энергии до Босса");

            Assert.AreEqual(0, firedCount);
        }
    }
}
