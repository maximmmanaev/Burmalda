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

        // === Issue #260: кривая задержки от Яруса Глубины ===

        [Test]
        public void ComputeDelaySeconds_Tier0_IsSeveralSecondsNotInstant()
        {
            Assert.Greater(BombTrapSystem.ComputeDelaySeconds(0), 1f, "«несколько секунд, не мгновенно» — критерий приёмки issue #260");
        }

        [Test]
        public void ComputeDelaySeconds_HigherTier_IsShorterThanLowerTier()
        {
            var early = BombTrapSystem.ComputeDelaySeconds(1);
            var late = BombTrapSystem.ComputeDelaySeconds(10);

            Assert.Less(late, early, "задержка обязана монотонно уменьшаться с ростом номера Яруса");
        }

        [Test]
        public void ComputeDelaySeconds_VeryHighTier_NeverGoesBelowMinimum()
        {
            var delay = BombTrapSystem.ComputeDelaySeconds(1000);

            Assert.GreaterOrEqual(delay, BombTrapSystem.MinDelaySeconds, "кривая не должна становиться тривиально короткой/отрицательной на глубоких Ярусах");
        }

        [Test]
        public void ComputeDelaySeconds_NegativeTier_TreatedAsTierZero()
        {
            Assert.AreEqual(BombTrapSystem.ComputeDelaySeconds(0), BombTrapSystem.ComputeDelaySeconds(-5));
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

        // === Issue #260: мигание-предупреждение во время ожидания ===

        [Test]
        public void PositionChanged_TrailReachesTrigger_BeginsWarningOnWholeBlastArea()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger);

            for (var row = 1; row <= 3; row++)
            for (var column = 1; column <= 3; column++)
                Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(row, column)).IsBombWarningActive,
                    $"({row},{column}) должна мигать предупреждением — вся площадь 3×3, не только сам триггер");
        }

        [Test]
        public void Tick_BeforeDelayElapses_WarningStillActive_NothingArmed()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger);

            bomb.Tick(BombTrapSystem.ComputeDelaySeconds(0) / 2); // половина задержки — рано

            Assert.IsFalse(grid.GetOrCreateTile(trigger).LethalTrap.HasValue);
            Assert.IsTrue(grid.GetOrCreateTile(trigger).IsBombWarningActive, "предупреждение обязано мигать всё ожидание");
        }

        [Test]
        public void Tick_DelayElapses_WarningEndsAndAreaCollapses()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger); // игрок стоит на триггере — он же плита площади

            bomb.Tick(BombTrapSystem.ComputeDelaySeconds(0));

            for (var row = 1; row <= 3; row++)
            for (var column = 1; column <= 3; column++)
            {
                var tile = grid.GetOrCreateTile(new GridCoordinate(row, column));
                Assert.IsFalse(tile.IsBombWarningActive, $"({row},{column}) предупреждение обязано смениться взрывом");
                Assert.IsTrue(tile.IsBombCollapsed, $"({row},{column}) обязана получить визуал дыры — вся площадь одинаково, независимо от исхода игрока");
            }
        }

        // === Взрыв: одномоментность и площадь ===

        [Test]
        public void Tick_DelayElapses_AllNineTilesCollapseSimultaneously()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger);

            bomb.Tick(BombTrapSystem.ComputeDelaySeconds(0));

            var collapsedCount = 0;
            for (var row = 1; row <= 3; row++)
            for (var column = 1; column <= 3; column++)
            {
                Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(row, column)).IsBombCollapsed, $"({row},{column}) должна быть в площади 3×3 вокруг триггера");
                collapsedCount++;
            }
            Assert.AreEqual(9, collapsedCount);
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

            bomb.Tick(BombTrapSystem.ComputeDelaySeconds(0));

            Assert.IsFalse(farTile.LethalTrap.HasValue);
            Assert.IsFalse(farTile.IsBlocked);
            Assert.IsFalse(farTile.IsBombCollapsed);
        }

        [Test]
        public void Tick_AtEdgeOfGrid_CollapsesFewerThanNineTiles_StaysInBounds()
        {
            // Критерий приёмки: у края тоннеля (столбец 0, Width=5) площадь
            // 3×3 обрезана — только 6 тайлов (столбцы -1 не существуют).
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 0));
            var trigger = new GridCoordinate(2, 0);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(new GridCoordinate(1, 0));
            trail.TryAdvanceTo(trigger);

            bomb.Tick(BombTrapSystem.ComputeDelaySeconds(0));

            var collapsedCount = 0;
            for (var row = 1; row <= 3; row++)
            for (var column = 0; column <= 1; column++)
            {
                if (grid.GetOrCreateTile(new GridCoordinate(row, column)).IsBombCollapsed)
                    collapsedCount++;
            }
            Assert.AreEqual(6, collapsedCount, "у левого края сетки (столбец 0) площадь 3×3 обрезается до 2×3 = 6 тайлов");
        }

        // === Issue #260: судьба площади после взрыва — постоянная дыра, не возврат к норме ===

        [Test]
        public void Tick_TileNotOccupiedByPlayer_BecomesPermanentlyBlocked_NotLethalTrap()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger); // игрок стоит на триггере (2,2) — соседняя (1,1) свободна

            bomb.Tick(BombTrapSystem.ComputeDelaySeconds(0));

            var untouchedByPlayer = grid.GetOrCreateTile(new GridCoordinate(1, 1));
            Assert.IsTrue(untouchedByPlayer.IsBlocked, "меняет решение issue #214 — площадь взрыва теперь становится постоянной дырой, владелец запросил это явно (issue #260)");
            Assert.IsFalse(untouchedByPlayer.LethalTrap.HasValue, "постоянная дыра не должна ОДНОВременно оставаться LethalTrap — иначе будущий шаг снова бросил бы d20 на остывшую воронку");
        }

        [Test]
        public void Tick_TileOccupiedByPlayer_StaysLethalTrap_DoesNotBecomeBlocked()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger); // последний ход — игрок стоит на триггере

            bomb.Tick(BombTrapSystem.ComputeDelaySeconds(0));

            var underPlayer = grid.GetOrCreateTile(trigger);
            Assert.AreEqual(LethalTrapType.BombBlast, underPlayer.LethalTrap, "плита под игроком идёт через обычную ловушку/d20, не через постоянную дыру");
            Assert.IsFalse(underPlayer.IsBlocked, "плита под игроком не должна быть Blocked — иначе исход Fortune (игрок остаётся стоять) стал бы противоречивым");
        }

        // === Issue #260: d20 для игрока, уже стоящего на плите площади (не новый ход) ===

        [Test]
        public void Tick_PlayerStandingOnBlastTile_FiresLethalTrapTriggered()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger); // стоит на триггере, входит в собственную площадь взрыва
            GridCoordinate? firedCoordinate = null;
            LethalTrapType? firedType = null;
            trail.LethalTrapTriggered += (coordinate, type) =>
            {
                firedCoordinate = coordinate;
                firedType = type;
            };

            bomb.Tick(BombTrapSystem.ComputeDelaySeconds(0));

            Assert.AreEqual(trigger, firedCoordinate, "событие обязано поднять ту же координату, где стоит игрок");
            Assert.AreEqual(LethalTrapType.BombBlast, firedType, "разрешение опасности обязано идти стандартным путём (d20), не отдельной логикой — критерий приёмки issue #260");
        }

        [Test]
        public void Tick_PlayerMovedAwayFromBlastAreaBeforeExplosion_DoesNotFireLethalTrapTriggered()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger); // активирует триггер, запускает отсчёт
            // Уходит за пределы площади 3×3 (радиус 1 вокруг (2,2) — строки
            // 1..3, столбцы 1..3) ДО того, как истечёт задержка.
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(new GridCoordinate(0, 2));
            trail.TryAdvanceTo(new GridCoordinate(0, 1));
            trail.TryAdvanceTo(new GridCoordinate(0, 0)); // строка 0 — вне площади (строки 1..3)
            var fired = false;
            trail.LethalTrapTriggered += (_, _) => fired = true;

            bomb.Tick(BombTrapSystem.ComputeDelaySeconds(0));

            Assert.IsFalse(fired, "игрок ушёл из площади взрыва до его момента — событие не должно подниматься");
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
            bomb.Tick(BombTrapSystem.ComputeDelaySeconds(0));

            Assert.IsFalse(grid.GetOrCreateTile(trigger).LethalTrap.HasValue);
        }

        [Test]
        public void PositionChanged_RevisitingAlreadyFiredTrigger_DoesNotQueueSecondBomb()
        {
            // Issue #260 переработал судьбу площади после взрыва (постоянная
            // дыра/LethalTrap, не "возврат в обычное состояние" — см. класс
            // тестов выше) — это структурно ДЕЛАЕТ повторный проход через
            // уже сработавший триггер физически недостижимым: он либо
            // Blocked (дыра), либо остаётся LethalTrap под игроком (если тот
            // стоял на нём в момент взрыва, как здесь). Тест проверяет
            // именно это — не сам факт "вторая бомба не запланирована"
            // напрямую (после взрыва запланировать её было бы уже не на чем:
            // _firedTriggers не пропустит повторный OnPositionChanged, даже
            // если бы плита была снова проходима).
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(trigger).MarkBombTrigger();
            using var bomb = new BombTrapSystem(grid, trail, scheduler);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(trigger); // игрок стоит на триггере в момент взрыва
            bomb.Tick(BombTrapSystem.ComputeDelaySeconds(0)); // первая (и единственная) бомба полностью отработала

            trail.TryAdvanceTo(new GridCoordinate(1, 2)); // назад на уже посещённую безопасную плиту
            var reApproached = trail.TryAdvanceTo(trigger); // попытка снова шагнуть на триггер

            Assert.IsFalse(reApproached, "триггер теперь несёт LethalTrap=BombBlast после взрыва — обычный шаг на него отклоняется, как на любую другую активную ловушку, второй бомбе взяться неоткуда");
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

            bomb.Tick(BombTrapSystem.ComputeDelaySeconds(0));

            Assert.AreEqual(LethalTrapType.BombBlast, grid.GetOrCreateTile(trigger).LethalTrap,
                "взрыв обязан был произойти по накопленному реальному времени без единого дополнительного хода игрока");
        }
    }
}
