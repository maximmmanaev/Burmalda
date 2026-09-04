using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class BladeTactTrapSystemTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail, TurnBasedThreatScheduler scheduler) CreateTrail(GridCoordinate start)
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, start);
            var scheduler = new TurnBasedThreatScheduler();
            return (grid, trail, scheduler);
        }

        private static bool IsLethal(TunnelGrid grid, int row, int column) =>
            grid.GetOrCreateTile(new GridCoordinate(row, column)).LethalTrap == LethalTrapType.BladeTact;

        [Test]
        public void PositionChanged_TrailReachesTrigger_DoesNotArmAnyColumnImmediately()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkBladeTactTrigger(targetRow: 1);
            using var blades = new BladeTactTrapSystem(grid, trail, scheduler);

            trail.TryAdvanceTo(trigger);

            for (var column = 0; column < Width; column++)
                Assert.IsFalse(IsLethal(grid, 1, column));
        }

        [Test]
        public void Tick_DelayElapses_ArmsFirstTact_OuterColumns()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkBladeTactTrigger(targetRow: 1);
            using var blades = new BladeTactTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);

            blades.Tick();

            Assert.IsTrue(IsLethal(grid, 1, 0));
            Assert.IsTrue(IsLethal(grid, 1, 4));
            Assert.IsFalse(IsLethal(grid, 1, 1));
            Assert.IsFalse(IsLethal(grid, 1, 2));
            Assert.IsFalse(IsLethal(grid, 1, 3));
        }

        [Test]
        public void Tick_FullTwoCycles_MatchesOwnerPattern_ThenStops()
        {
            // Владелец: "1 и 5 → 2 и 4 → 3 → 2 и 4 → 1 и 5" (0-индексация:
            // [0,4] → [1,3] → [2] → [1,3] → [0,4]), 2 полных цикла = 10 тактов.
            var expectedCycle = new[]
            {
                new[] { 0, 4 },
                new[] { 1, 3 },
                new[] { 2 },
                new[] { 1, 3 },
                new[] { 0, 4 }
            };

            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkBladeTactTrigger(targetRow: 1);
            using var blades = new BladeTactTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);

            for (var cycle = 0; cycle < 2; cycle++)
            {
                foreach (var expectedColumns in expectedCycle)
                {
                    blades.Tick();
                    foreach (var column in expectedColumns)
                        Assert.IsTrue(IsLethal(grid, 1, column), $"цикл {cycle}: столбец {column} должен быть опасен на этом такте");
                }
            }

            blades.Tick(); // финальный тик — снимает последний такт, новый не активирует

            for (var column = 0; column < Width; column++)
                Assert.IsFalse(IsLethal(grid, 1, column), $"после 2 циклов столбец {column} должен быть безопасен — ловушка затихла");
        }

        [Test]
        public void Tick_SecondTact_DisarmsFirstTact()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkBladeTactTrigger(targetRow: 1);
            using var blades = new BladeTactTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);
            blades.Tick(); // такт 1: [0,4]

            blades.Tick(); // такт 2: [1,3]

            Assert.IsFalse(IsLethal(grid, 1, 0), "предыдущий такт должен был уже стать безопасным");
            Assert.IsFalse(IsLethal(grid, 1, 4));
            Assert.IsTrue(IsLethal(grid, 1, 1));
            Assert.IsTrue(IsLethal(grid, 1, 3));
        }

        [Test]
        public void Tick_PlayerLeavesRow_StopsEarly()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkBladeTactTrigger(targetRow: 1);
            using var blades = new BladeTactTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger); // игрок на ряду 1 — та же "зона"
            blades.Tick(); // такт 1: [0,4] активен

            trail.TryAdvanceTo(new GridCoordinate(0, 2)); // покидает ряд 1
            blades.Tick();

            Assert.IsFalse(IsLethal(grid, 1, 0), "такт должен был снять опасность даже при досрочной остановке");
            Assert.IsFalse(IsLethal(grid, 1, 4));
            Assert.IsFalse(IsLethal(grid, 1, 1), "новый такт не должен был активироваться — игрок покинул зону");
            Assert.IsFalse(IsLethal(grid, 1, 3));

            // Дальнейшие тики ничего не меняют — последовательность уже остановлена.
            blades.Tick();
            blades.Tick();
            for (var column = 0; column < Width; column++)
                Assert.IsFalse(IsLethal(grid, 1, column));
        }

        [Test]
        public void Tick_TilesOutsideTargetRow_AreNeverAffected()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkBladeTactTrigger(targetRow: 1);
            var otherRowTile = grid.GetOrCreateTile(new GridCoordinate(2, 0));
            using var blades = new BladeTactTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);

            for (var i = 0; i < 11; i++) blades.Tick();

            Assert.IsFalse(otherRowTile.LethalTrap.HasValue);
        }

        [Test]
        public void PositionChanged_RevisitingAlreadyFiredTrigger_DoesNotQueueSecondSequence()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkBladeTactTrigger(targetRow: 1);
            using var blades = new BladeTactTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);
            for (var i = 0; i < 11; i++) blades.Tick(); // первая последовательность полностью отработала

            trail.TryAdvanceTo(new GridCoordinate(0, 2)); // назад
            trail.TryAdvanceTo(trigger); // повторно на триггер
            blades.Tick();

            Assert.IsFalse(IsLethal(grid, 1, 0), "триггер одноразовый — повторный проход не должен запустить вторую последовательность");
        }

        [Test]
        public void Dispose_StopsReactingToFurtherPositionChangesAndTicks()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkBladeTactTrigger(targetRow: 1);
            var blades = new BladeTactTrapSystem(grid, trail, scheduler);
            blades.Dispose();

            trail.TryAdvanceTo(trigger);
            blades.Tick();

            Assert.IsFalse(IsLethal(grid, 1, 0));
        }
    }
}
