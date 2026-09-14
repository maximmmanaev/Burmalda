using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class ArrowWaveTrapSystemTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail, RealTimeThreatScheduler scheduler) CreateTrail(GridCoordinate start)
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, start);
            var scheduler = new RealTimeThreatScheduler();
            return (grid, trail, scheduler);
        }

        [Test]
        public void PositionChanged_TrailReachesTrigger_DoesNotArmAnyColumnImmediately()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkArrowWaveTrigger(targetRow: 2, RowWaveDirection.LeftToRight);
            using var arrowWave = new ArrowWaveTrapSystem(grid, trail, scheduler);

            trail.TryAdvanceTo(trigger);

            for (var column = 0; column < Width; column++)
                Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(2, column)).LethalTrap.HasValue);
        }

        [Test]
        public void Tick_DelayElapses_ArmsFirstColumn_LeftToRight()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkArrowWaveTrigger(targetRow: 2, RowWaveDirection.LeftToRight);
            using var arrowWave = new ArrowWaveTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);

            arrowWave.Tick(ArrowWaveTrapSystem.StepSeconds);

            var firstColumn = grid.GetOrCreateTile(new GridCoordinate(2, 0));
            Assert.AreEqual(LethalTrapType.ArrowWave, firstColumn.LethalTrap);
        }

        [Test]
        public void Tick_DelayElapses_ArmsFirstColumn_RightToLeft()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkArrowWaveTrigger(targetRow: 2, RowWaveDirection.RightToLeft);
            using var arrowWave = new ArrowWaveTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);

            arrowWave.Tick(ArrowWaveTrapSystem.StepSeconds);

            var lastColumn = grid.GetOrCreateTile(new GridCoordinate(2, Width - 1));
            Assert.AreEqual(LethalTrapType.ArrowWave, lastColumn.LethalTrap);
        }

        [Test]
        public void Tick_SecondTick_DisarmsFirstColumn_ArmsSecondColumn()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkArrowWaveTrigger(targetRow: 2, RowWaveDirection.LeftToRight);
            using var arrowWave = new ArrowWaveTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);
            arrowWave.Tick(ArrowWaveTrapSystem.StepSeconds); // столбец 0 опасен

            arrowWave.Tick(ArrowWaveTrapSystem.StepSeconds);

            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(2, 0)).LethalTrap.HasValue, "волна прошла дальше — столбец 0 снова безопасен");
            Assert.AreEqual(LethalTrapType.ArrowWave, grid.GetOrCreateTile(new GridCoordinate(2, 1)).LethalTrap);
        }

        [Test]
        public void Tick_FullSequence_EachColumnDangerousExactlyOnce_ThenAllSafe()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkArrowWaveTrigger(targetRow: 2, RowWaveDirection.LeftToRight);
            using var arrowWave = new ArrowWaveTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);

            for (var column = 0; column < Width; column++)
            {
                arrowWave.Tick(ArrowWaveTrapSystem.StepSeconds);
                var armed = grid.GetOrCreateTile(new GridCoordinate(2, column));
                Assert.AreEqual(LethalTrapType.ArrowWave, armed.LethalTrap, $"столбец {column} должен стать опасным на своём тике волны");

                if (column > 0)
                {
                    var previous = grid.GetOrCreateTile(new GridCoordinate(2, column - 1));
                    Assert.IsFalse(previous.LethalTrap.HasValue, $"столбец {column - 1} должен был уже стать безопасным");
                }
            }

            arrowWave.Tick(ArrowWaveTrapSystem.StepSeconds); // финальный тик — снимает опасность с последнего столбца, новый не активирует

            for (var column = 0; column < Width; column++)
                Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(2, column)).LethalTrap.HasValue, $"после прохода волны столбец {column} должен быть безопасен");
        }

        [Test]
        public void Tick_TilesOutsideWaveRow_AreNeverAffected()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkArrowWaveTrigger(targetRow: 2, RowWaveDirection.LeftToRight);
            var otherRowTile = grid.GetOrCreateTile(new GridCoordinate(3, 0));
            using var arrowWave = new ArrowWaveTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);

            for (var i = 0; i < Width + 1; i++) arrowWave.Tick(ArrowWaveTrapSystem.StepSeconds);

            Assert.IsFalse(otherRowTile.LethalTrap.HasValue, "волна другого ряда не должна задевать соседние ряды");
        }

        [Test]
        public void PositionChanged_RevisitingAlreadyFiredTrigger_DoesNotQueueSecondWave()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkArrowWaveTrigger(targetRow: 2, RowWaveDirection.LeftToRight);
            using var arrowWave = new ArrowWaveTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);
            for (var i = 0; i < Width + 1; i++) arrowWave.Tick(ArrowWaveTrapSystem.StepSeconds); // первая волна полностью прошла

            trail.TryAdvanceTo(new GridCoordinate(0, 2)); // назад
            trail.TryAdvanceTo(trigger); // повторно на триггер
            arrowWave.Tick(ArrowWaveTrapSystem.StepSeconds);

            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(2, 0)).LethalTrap.HasValue, "триггер одноразовый — повторный проход не должен запустить вторую волну");
        }

        [Test]
        public void Dispose_StopsReactingToFurtherPositionChangesAndTicks()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkArrowWaveTrigger(targetRow: 2, RowWaveDirection.LeftToRight);
            var arrowWave = new ArrowWaveTrapSystem(grid, trail, scheduler);
            arrowWave.Dispose();

            trail.TryAdvanceTo(trigger);
            arrowWave.Tick(ArrowWaveTrapSystem.StepSeconds);

            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(2, 0)).LethalTrap.HasValue);
        }

        // issue #254 (владелец, «Волновые ловушки переходят на реальное
        // время»): раньше волна тикалась ровно на каждый шаг игрока,
        // поэтому физически не могла догнать стоящего на месте игрока — она
        // была безопасна по построению, не по игровому замыслу. Ядро
        // критерия приёмки: "игрок стоит на месте несколько секунд — волна
        // всё равно продвигается" — здесь трейл вообще не делает ни одного
        // хода между тиками, только реальное время идёт вперёд.
        [Test]
        public void Tick_PlayerStandsStillForSeveralSeconds_WaveStillAdvancesOnRealTimeAlone()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkArrowWaveTrigger(targetRow: 2, RowWaveDirection.LeftToRight);
            using var arrowWave = new ArrowWaveTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger); // единственный ход за весь тест — дальше игрок стоит на месте

            // Несколько секунд реального времени несколькими мелкими Tick —
            // ни одного хода трейла между ними.
            for (var i = 0; i < Width; i++) arrowWave.Tick(ArrowWaveTrapSystem.StepSeconds);

            var lastColumn = grid.GetOrCreateTile(new GridCoordinate(2, Width - 1));
            Assert.AreEqual(LethalTrapType.ArrowWave, lastColumn.LethalTrap,
                "волна обязана дойти до последнего столбца по одному только реальному времени, без единого хода игрока");
        }
    }
}
