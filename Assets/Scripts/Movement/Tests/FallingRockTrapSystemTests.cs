using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    // Задача «падающий камень: новая спецификация» (владелец, Спринт «Стены
    // вместо ловушек», задача 4) — переписан целиком, не дополнен: раньше
    // камень падал на САМУ плиту-триггер, теперь — на плиту ВПЕРЕДИ
    // (Tile.FallingRockTargetCoordinate, дефолт "ряд+1, тот же столбец" из
    // SegmentRowProvider/TunnelObstacleGenerator, здесь передаётся явно).
    // Плита-триггер сама по себе теперь ВСЕГДА безопасна — ни один тест
    // этого файла не проверяет её на IsBlocked/PlayerCrushed.
    public class FallingRockTrapSystemTests
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
        public void PositionChanged_TrailReachesTrigger_BeginsWarningOnTargetTile_DoesNotBlockYet()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger(target);
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);

            trail.TryAdvanceTo(trigger);

            Assert.IsTrue(grid.GetOrCreateTile(target).IsFallingRockWarningActive, "целевая плита обязана подсветиться сразу при активации триггера — \"однозначно видно, куда упадёт\"");
            Assert.IsFalse(grid.GetOrCreateTile(target).IsBlocked);
        }

        [Test]
        public void PositionChanged_TrailReachesTrigger_TriggerTileItselfNeverWarns()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger(target);
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);

            trail.TryAdvanceTo(trigger);

            Assert.IsFalse(grid.GetOrCreateTile(trigger).IsFallingRockWarningActive, "предупреждение — на целевой плите впереди, не на самом триггере");
        }

        [Test]
        public void Tick_BeforeDelayElapses_TargetStaysWarnedButNotResolved()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger(target);
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);

            fallingRock.Tick(FallingRockTrapSystem.DelaySeconds / 2); // половина задержки — рано

            Assert.IsTrue(grid.GetOrCreateTile(target).IsFallingRockWarningActive);
            Assert.IsFalse(grid.GetOrCreateTile(target).IsBlocked);
        }

        [Test]
        public void Tick_DelayElapses_PlayerOnTargetTile_RaisesPlayerCrushed_DoesNotBlock_EndsWarning()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger(target);
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);
            GridCoordinate? crushedAt = null;
            fallingRock.PlayerCrushed += c => crushedAt = c;
            trail.TryAdvanceTo(trigger); // активирует
            trail.TryAdvanceTo(target); // игрок доходит до цели и стоит на ней

            fallingRock.Tick(FallingRockTrapSystem.DelaySeconds);

            Assert.AreEqual(target, crushedAt);
            Assert.IsFalse(grid.GetOrCreateTile(target).IsBlocked, "смертельный исход не должен дополнительно блокировать плиту");
            Assert.IsFalse(grid.GetOrCreateTile(target).IsFallingRockWarningActive, "предупреждение снимается в момент падения независимо от исхода");
        }

        [Test]
        public void Tick_DelayElapses_PlayerNotOnTargetTile_BlocksTargetTile_DoesNotRaisePlayerCrushed_EndsWarning()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger(target);
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);
            var crushed = false;
            fallingRock.PlayerCrushed += _ => crushed = true;
            trail.TryAdvanceTo(trigger); // активирует, игрок остаётся на триггере, не идёт на цель

            fallingRock.Tick(FallingRockTrapSystem.DelaySeconds);

            Assert.IsFalse(crushed);
            Assert.IsTrue(grid.GetOrCreateTile(target).IsBlocked);
            Assert.IsFalse(grid.GetOrCreateTile(target).IsFallingRockWarningActive);
        }

        [Test]
        public void Tick_PlayerNotOnTarget_TargetTileIsPermanentlyBlocked_CannotBeSteppedOnAgain()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger(target);
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);
            fallingRock.Tick(FallingRockTrapSystem.DelaySeconds); // цель блокируется, игрок всё ещё на триггере

            var advanced = trail.TryAdvanceTo(target);

            Assert.IsFalse(advanced, "заблокированная плита непроходима, как обычная стена");
        }

        [Test]
        public void Tick_TriggerTileItself_IsNeverBlocked()
        {
            // Ключевое отличие новой спецификации: сама плита-триггер
            // безопасна и остаётся проходимой независимо от исхода —
            // опасность целиком на плите впереди.
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger(target);
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);

            fallingRock.Tick(FallingRockTrapSystem.DelaySeconds);

            Assert.IsFalse(grid.GetOrCreateTile(trigger).IsBlocked);
        }

        [Test]
        public void Tick_TilesOutsideTargetAndTrigger_AreNeverAffected()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger(target);
            var neighborTile = grid.GetOrCreateTile(new GridCoordinate(2, 1));
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(trigger);

            fallingRock.Tick(FallingRockTrapSystem.DelaySeconds);

            Assert.IsFalse(neighborTile.IsBlocked);
            Assert.IsFalse(neighborTile.IsFallingRockWarningActive);
        }

        [Test]
        public void PositionChanged_RevisitingAlreadyFiredTrigger_DoesNotQueueSecondActivation()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger(target);
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);
            var crushCount = 0;
            fallingRock.PlayerCrushed += _ => crushCount++;

            trail.TryAdvanceTo(trigger); // первый визит — регистрирует активацию
            trail.TryAdvanceTo(new GridCoordinate(0, 2)); // назад
            trail.TryAdvanceTo(trigger); // повторный визит — не должен зарегистрировать вторую
            trail.TryAdvanceTo(target); // игрок доходит до цели к моменту падения

            fallingRock.Tick(FallingRockTrapSystem.DelaySeconds);

            Assert.AreEqual(1, crushCount, "повторный визит на уже сработавший триггер не должен ставить вторую активацию");
        }

        [Test]
        public void Dispose_StopsReactingToFurtherPositionChangesAndTicks()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger(target);
            var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);
            fallingRock.Dispose();

            trail.TryAdvanceTo(trigger);
            fallingRock.Tick(FallingRockTrapSystem.DelaySeconds);

            Assert.IsFalse(grid.GetOrCreateTile(target).IsBlocked);
            Assert.IsFalse(grid.GetOrCreateTile(target).IsFallingRockWarningActive);
        }

        // issue #254, второй раунд (владелец: «ловушки в такт шагам это
        // ошибка, никаких ловушек в такт быть не должно, только тайминги»):
        // игрок остаётся на целевой плите и дальше СТОИТ на месте — камень
        // обязан всё равно упасть по одному только реальному времени, без
        // дополнительных ходов после того, как игрок добрался до цели.
        [Test]
        public void Tick_PlayerStandsStillOnTargetTile_StillCrushedOnRealTimeAlone()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(1, 2);
            var target = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkFallingRockTrigger(target);
            using var fallingRock = new FallingRockTrapSystem(grid, trail, scheduler);
            GridCoordinate? crushedAt = null;
            fallingRock.PlayerCrushed += c => crushedAt = c;
            trail.TryAdvanceTo(trigger);
            trail.TryAdvanceTo(target); // последний ход — дальше игрок просто стоит

            fallingRock.Tick(FallingRockTrapSystem.DelaySeconds);

            Assert.AreEqual(target, crushedAt, "камень обязан был упасть по накопленному реальному времени без единого дополнительного хода игрока");
        }
    }
}
