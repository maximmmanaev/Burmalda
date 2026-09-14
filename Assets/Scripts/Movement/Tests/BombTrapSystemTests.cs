using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class BombTrapSystemTests
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
        public void PositionChanged_TrailReachesTrigger_DoesNotArmAnyTileImmediately()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger);

            Assert.IsFalse(grid.GetOrCreateTile(trigger).LethalTrap.HasValue);
        }

        [Test]
        public void Tick_BeforeDelayElapses_NothingArmed()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger);

            bomb.Tick(BombTrapSystem.DelaySeconds / 2); // половина задержки — рано

            Assert.IsFalse(grid.GetOrCreateTile(trigger).LethalTrap.HasValue);
        }

        [Test]
        public void Tick_DelayElapses_AllNineTilesBecomeLethalSimultaneously()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger);

            bomb.Tick(BombTrapSystem.DelaySeconds); // ровно вся задержка — активация

            var armedCount = 0;
            for (var row = 1; row <= 3; row++)
            for (var column = 1; column <= 3; column++)
            {
                var tile = grid.GetOrCreateTile(new GridCoordinate(row, column));
                Assert.AreEqual(LethalTrapType.BombBlast, tile.LethalTrap, $"({row},{column}) должна быть в площади 3×3 вокруг триггера");
                armedCount++;
            }
            Assert.AreEqual(9, armedCount);
        }

        [Test]
        public void Tick_DelayElapses_TilesOutsideBlastRadius_AreNotAffected()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            var farTile = grid.GetOrCreateTile(new GridCoordinate(2, 4)); // за пределами радиуса 1 по столбцу
            using var bomb = new BombTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger);

            bomb.Tick(BombTrapSystem.DelaySeconds);

            Assert.IsFalse(farTile.LethalTrap.HasValue);
        }

        [Test]
        public void Tick_AtEdgeOfGrid_ArmsFewerThanNineTiles_StaysInBounds()
        {
            // Критерий приёмки: у края тоннеля (столбец 0, Width=5) площадь
            // 3×3 обрезана — только 6 тайлов (столбцы -1 не существуют).
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 0));
            var trigger = new GridCoordinate(2, 0);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(new GridCoordinate(1, 0));
            trail.TryAdvanceTo(trigger);

            bomb.Tick(BombTrapSystem.DelaySeconds);

            var armedCount = 0;
            for (var row = 1; row <= 3; row++)
            for (var column = 0; column <= 1; column++)
            {
                if (grid.GetOrCreateTile(new GridCoordinate(row, column)).LethalTrap == LethalTrapType.BombBlast)
                    armedCount++;
            }
            Assert.AreEqual(6, armedCount, "у левого края сетки (столбец 0) площадь 3×3 обрезается до 2×3 = 6 тайлов");
        }

        [Test]
        public void Tick_ExplosionDurationElapses_AreaReturnsToNormal_NotDestroyed()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger);
            bomb.Tick(BombTrapSystem.DelaySeconds); // взрыв

            bomb.Tick(BombTrapSystem.ExplosionDurationSeconds); // снятие

            for (var row = 1; row <= 3; row++)
            for (var column = 1; column <= 3; column++)
            {
                var tile = grid.GetOrCreateTile(new GridCoordinate(row, column));
                Assert.IsFalse(tile.LethalTrap.HasValue, $"({row},{column}) должна вернуться в обычное состояние");
                Assert.IsFalse(tile.IsDestroyed, "владелец: плиты не разрушаются, а возвращаются в обычное состояние");
            }
        }

        [Test]
        public void PositionChanged_RevisitingAlreadyFiredTrigger_DoesNotQueueSecondBomb()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger);
            bomb.Tick(BombTrapSystem.DelaySeconds);
            bomb.Tick(BombTrapSystem.ExplosionDurationSeconds); // первая бомба полностью отработала

            trail.TryAdvanceTo(new GridCoordinate(1, 2)); // назад
            trail.TryAdvanceTo(trigger); // повторно на триггер
            bomb.Tick(BombTrapSystem.DelaySeconds);

            Assert.IsFalse(grid.GetOrCreateTile(trigger).LethalTrap.HasValue, "триггер одноразовый — повторный проход не должен запустить вторую бомбу");
        }

        [Test]
        public void Dispose_StopsReactingToFurtherPositionChangesAndTicks()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            var bomb = new BombTrapSystem(grid, trail, scheduler);
            bomb.Dispose();

            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger);
            bomb.Tick(BombTrapSystem.DelaySeconds);

            Assert.IsFalse(grid.GetOrCreateTile(trigger).LethalTrap.HasValue);
        }

        // issue #254, второй раунд (владелец: «ловушки в такт шагам это
        // ошибка, никаких ловушек в такт быть не должно, только тайминги»):
        // игрок наводится на триггер, встаёт на него и дальше СТОИТ на
        // месте — взрыв обязан всё равно произойти по одному только
        // реальному времени, без единого дополнительного хода.
        [Test]
        public void Tick_PlayerStandsStillOnTrigger_ExplosionStillHappensOnRealTimeAlone()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger); // последний ход за весь тест

            bomb.Tick(BombTrapSystem.DelaySeconds);

            Assert.AreEqual(LethalTrapType.BombBlast, grid.GetOrCreateTile(trigger).LethalTrap,
                "взрыв обязан был произойти по накопленному реальному времени без единого дополнительного хода игрока");
        }
    }
}
