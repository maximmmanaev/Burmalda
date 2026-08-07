using Burmalda.Core;
using Burmalda.Decay;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.RunLifecycle.Tests
{
    public class RunStateTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail, TrailDecaySystem decay, RunState runState) CreateRun()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var decay = new TrailDecaySystem(grid, trail);
            var runState = new RunState(trail, decay);
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
    }
}
