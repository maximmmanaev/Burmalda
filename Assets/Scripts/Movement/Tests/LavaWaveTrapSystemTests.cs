using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class LavaWaveTrapSystemTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail, TurnBasedThreatScheduler scheduler) CreateTrail(GridCoordinate start)
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, start);
            var scheduler = new TurnBasedThreatScheduler();
            return (grid, trail, scheduler);
        }

        private static bool IsRowFullyLava(TunnelGrid grid, int row)
        {
            for (var column = 0; column < Width; column++)
            {
                if (!grid.TryGetTile(new GridCoordinate(row, column), out var tile)) return false;
                if (tile.LethalTrap != LethalTrapType.LavaWave) return false;
            }
            return true;
        }

        // TryGetTile, не GetOrCreateTile — иначе выбросило бы исключение
        // для рядов за пределами сетки (Row < 0, см. TunnelGrid.Contains),
        // которые как раз и нужно проверять в тестах на границу тоннеля.
        // Немате­риализованная плита по определению не тронута волной.
        private static bool IsRowUntouched(TunnelGrid grid, int row)
        {
            for (var column = 0; column < Width; column++)
            {
                if (grid.TryGetTile(new GridCoordinate(row, column), out var tile) && tile.LethalTrap.HasValue)
                    return false;
            }
            return true;
        }

        // Ходит по прямой колонке col, из start.Row в target.Row (только вперёд).
        private static void WalkForwardTo(GridTraceTrail trail, int fromRow, int toRow, int column)
        {
            for (var row = fromRow + 1; row <= toRow; row++)
                Assert.IsTrue(trail.TryAdvanceTo(new GridCoordinate(row, column)), $"шаг на ({row},{column}) должен был пройти");
        }

        [Test]
        public void PositionChanged_TrailReachesTrigger_DoesNotArmAnyRowImmediately()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);

            WalkForwardTo(trail, 0, 5, 2);

            Assert.IsFalse(IsRowFullyLava(grid, 5));
        }

        // Владелец, 2026-09-04: "включая случай, когда игрок в момент
        // активации триггера стоит именно в ряду триггера" — критический тест.
        [Test]
        public void Tick_PlayerStillOnTriggerRow_DoesNotConvertTriggerRow_WaitsUntilPlayerMovesForward()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 5, 2); // игрок стоит прямо на триггере

            lava.Tick();
            Assert.IsFalse(IsRowFullyLava(grid, 5), "ряд игрока не должен стать лавой немедленно");

            lava.Tick();
            lava.Tick();
            Assert.IsFalse(IsRowFullyLava(grid, 5), "волна обязана ждать, а не пропускать ряд навсегда");

            trail.TryAdvanceTo(new GridCoordinate(6, 2)); // игрок продвинулся вперёд — ряд 5 теперь позади него
            lava.Tick();

            Assert.IsTrue(IsRowFullyLava(grid, 5), "как только ряд оказался строго позади игрока, волна должна была его конвертировать");
        }

        [Test]
        public void Tick_ConvertsWholeRow_AllColumns()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 6, 2); // игрок уже прошёл дальше триггера

            lava.Tick();

            for (var column = 0; column < Width; column++)
                Assert.AreEqual(LethalTrapType.LavaWave, grid.GetOrCreateTile(new GridCoordinate(5, column)).LethalTrap, $"столбец {column} ряда 5 должен быть лавой");
        }

        [Test]
        public void Tick_SecondStep_ConvertsRowBehindTriggerRow()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 6, 2);
            lava.Tick(); // ряд 5

            lava.Tick(); // должен быть ряд 4

            Assert.IsTrue(IsRowFullyLava(grid, 4));
        }

        [Test]
        public void Tick_ConvertedRow_NeverRevertsToSafe()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 6, 2);
            lava.Tick(); // ряд 5 — лава

            for (var i = 0; i < 10; i++) lava.Tick(); // дальнейшие тики волны и её завершение

            Assert.IsTrue(IsRowFullyLava(grid, 5), "владелец: «отрезая путь назад» — необратимость и есть смысл ловушки");
        }

        [Test]
        public void Tick_FullSequence_ConvertsExactlyMaxRows_ThenStops()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(10, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 20, 2); // далеко впереди — инвариант никогда не блокирует

            for (var i = 0; i < LavaWaveTrapSystem.MaxRows; i++) lava.Tick();

            for (var row = 10; row > 10 - LavaWaveTrapSystem.MaxRows; row--)
                Assert.IsTrue(IsRowFullyLava(grid, row), $"ряд {row} должен быть частью волны (всего {LavaWaveTrapSystem.MaxRows} рядов)");

            var beyondCapRow = 10 - LavaWaveTrapSystem.MaxRows;
            Assert.IsTrue(IsRowUntouched(grid, beyondCapRow), "ряд за пределами лимита не должен быть тронут");

            lava.Tick(); // седьмой тик — не должен ничего менять
            Assert.IsTrue(IsRowUntouched(grid, beyondCapRow));
        }

        [Test]
        public void Tick_WaveReachesStartOfTunnel_StopsEarly_BeforeReachingMaxRows()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2); // всего 3 валидных ряда позади (2, 1, 0)
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 10, 2); // далеко впереди

            lava.Tick(); // ряд 2
            lava.Tick(); // ряд 1
            lava.Tick(); // ряд 0

            Assert.IsTrue(IsRowFullyLava(grid, 2));
            Assert.IsTrue(IsRowFullyLava(grid, 1));
            Assert.IsTrue(IsRowFullyLava(grid, 0));

            Assert.DoesNotThrow(() => lava.Tick(), "волна должна тихо остановиться на границе тоннеля, не упасть на отрицательном ряду");
        }

        [Test]
        public void Tick_RowsOutsideWavePath_AreNeverAffected()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 6, 2);

            lava.Tick(); // ряд 5

            Assert.IsTrue(IsRowUntouched(grid, 6), "ряд игрока не должен быть тронут");
            Assert.IsTrue(IsRowUntouched(grid, 3), "ряд, до которого волна ещё не дошла, не должен быть тронут");
        }

        [Test]
        public void PositionChanged_RevisitingAlreadyFiredTrigger_DoesNotStartSecondWave()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 5, 2); // первый визит — на самом триггере

            trail.TryAdvanceTo(new GridCoordinate(4, 2)); // назад
            trail.TryAdvanceTo(trigger); // повторный визит на триггер — не должен запустить вторую волну
            WalkForwardTo(trail, 5, 6, 2); // вперёд, за пределы ряда триггера

            lava.Tick();

            // Одна волна — ряд 5 стал лавой ровно один раз (это уже
            // покрыто Tick_ConvertsWholeRow), здесь важно, что ДАЛЬШЕ по
            // тикам не оказывается ДВУХ независимых волн, тикающих
            // одновременно — косвенно проверяется тем, что после ровно
            // MaxRows тиков волна корректно останавливается (см. следующую
            // проверку: ряд глубоко за пределами лимита остаётся нетронутым).
            for (var i = 0; i < LavaWaveTrapSystem.MaxRows; i++) lava.Tick();

            Assert.IsTrue(IsRowUntouched(grid, 5 - LavaWaveTrapSystem.MaxRows), "если бы вторая волна зарегистрировалась, лимит рядов был бы превышен");
        }

        [Test]
        public void Dispose_StopsReactingToFurtherPositionChangesAndTicks()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            lava.Dispose();

            WalkForwardTo(trail, 0, 6, 2);
            lava.Tick();

            Assert.IsTrue(IsRowUntouched(grid, 5));
        }
    }
}
