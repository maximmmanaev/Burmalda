using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class FallingRockTrapSystemTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail, TurnBasedThreatScheduler scheduler) CreateTrail(GridCoordinate start)
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, start);
            var scheduler = new TurnBasedThreatScheduler();
            return (grid, trail, scheduler);
        }

        [Test]
        public void PositionChanged_TrailReachesTrigger_DoesNothingImmediately()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger();
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);

            trail.TryAdvanceTo(trigger);

            Assert.IsFalse(grid.GetOrCreateTile(trigger).IsBlocked);
        }

        [Test]
        public void Tick_BeforeDelayElapses_NothingHappens()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger();
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);

            // DelayTicks=1 — минимальная задержка, поэтому "до истечения"
            // здесь значит "вообще не тикать", в отличие от систем с
            // задержкой в несколько ходов (Бомба, DelayTicks=2).
            Assert.IsFalse(grid.GetOrCreateTile(trigger).IsBlocked);
        }

        [Test]
        public void Tick_DelayElapses_PlayerStillOnTile_RaisesPlayerCrushed_DoesNotBlock()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger();
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);
            GridCoordinate? crushedAt = null;
            fallingRock.PlayerCrushed += c => crushedAt = c;
            trail.TryAdvanceTo(trigger); // игрок остаётся на триггере

            fallingRock.Tick();

            Assert.AreEqual(trigger, crushedAt);
            Assert.IsFalse(grid.GetOrCreateTile(trigger).IsBlocked, "смертельный исход не должен дополнительно блокировать плиту");
        }

        [Test]
        public void Tick_DelayElapses_PlayerLeft_BlocksTile_DoesNotRaisePlayerCrushed()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger();
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);
            var crushed = false;
            fallingRock.PlayerCrushed += _ => crushed = true;
            trail.TryAdvanceTo(trigger);
            trail.TryAdvanceTo(new GridCoordinate(0, 2)); // игрок ушёл с триггера

            fallingRock.Tick();

            Assert.IsFalse(crushed);
            Assert.IsTrue(grid.GetOrCreateTile(trigger).IsBlocked);
        }

        [Test]
        public void Tick_PlayerLeft_TileIsPermanentlyBlocked_CannotBeSteppedOnAgain()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger();
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);
            trail.TryAdvanceTo(new GridCoordinate(0, 2));
            fallingRock.Tick();

            var advanced = trail.TryAdvanceTo(trigger);

            Assert.IsFalse(advanced, "заблокированная плита непроходима, как обычная стена");
        }

        [Test]
        public void Tick_TilesOutsideTrigger_AreNeverAffected()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger();
            var neighborTile = grid.GetOrCreateTile(new GridCoordinate(1, 1));
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);
            trail.TryAdvanceTo(new GridCoordinate(0, 2));

            fallingRock.Tick();

            Assert.IsFalse(neighborTile.IsBlocked);
        }

        [Test]
        public void PositionChanged_RevisitingAlreadyFiredTrigger_DoesNotQueueSecondActivation()
        {
            // Ревизит ДО тика первой активации (не после, как у прочих
            // систем этого семейства) — эта ловушка, в отличие от
            // Бомбы/Стрелы/Лезвий, необратимо блокирует плиту при исходе
            // "игрок ушёл", так что после полного цикла повторный визит
            // физически невозможен (стена). Если бы OnPositionChanged
            // ошибочно поставил вторую независимую активацию (планировщик
            // такое умеет, см. TurnBasedThreatSchedulerTests), один Tick()
            // вызвал бы PlayerCrushed дважды — TileDue сработал бы для
            // обеих регистраций в одном и том же тике.
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger();
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);
            var crushCount = 0;
            fallingRock.PlayerCrushed += _ => crushCount++;

            trail.TryAdvanceTo(trigger); // первый визит — регистрирует активацию
            trail.TryAdvanceTo(new GridCoordinate(0, 2)); // назад
            trail.TryAdvanceTo(trigger); // повторный визит — не должен зарегистрировать вторую

            fallingRock.Tick();

            Assert.AreEqual(1, crushCount, "повторный визит на уже сработавший триггер не должен ставить вторую активацию");
        }

        [Test]
        public void Dispose_StopsReactingToFurtherPositionChangesAndTicks()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger();
            var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);
            fallingRock.Dispose();

            trail.TryAdvanceTo(trigger);
            fallingRock.Tick();

            Assert.IsFalse(grid.GetOrCreateTile(trigger).IsBlocked);
        }
    }
}
