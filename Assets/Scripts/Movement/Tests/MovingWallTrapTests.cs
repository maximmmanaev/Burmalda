using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class MovingWallTrapTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail, RealTimeThreatScheduler scheduler) CreateTrail(GridCoordinate start)
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, start);
            var scheduler = new RealTimeThreatScheduler();
            return (grid, trail, scheduler);
        }

        private static bool IsCrushLethal(TunnelGrid grid, int row, int column) =>
            grid.GetOrCreateTile(new GridCoordinate(row, column)).LethalTrap == LethalTrapType.MovingWallCrush;

        // Триггер стоит ПЕРЕД закрывающимся рядом — тот же принцип, что у
        // FallingRockTrigger (targetRow = triggerRow + 1), см. doc-комментарий
        // Tile.MovingWallTargetRow/SegmentRowProvider.ApplyTileType.
        private static GridCoordinate Trigger => new GridCoordinate(0, 2);
        private static int TargetRow => 1;

        [Test]
        public void PositionChanged_ArrivesAtTrigger_RevealsSignatureButDoesNotArmAnyColumn()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 0));
            var triggerTile = grid.GetOrCreateTile(Trigger);
            triggerTile.MarkMovingWallTrigger(TargetRow, MovingWallMode.Both);
            using var wall = new MovingWallTrap(grid, trail, scheduler);

            trail.TryAdvanceTo(Trigger);

            Assert.IsTrue(triggerTile.IsDangerSignatureRevealed, "приход на триггер — фаза Detected, сигнатура опасности обязана раскрыться");
            for (var column = 0; column < Width; column++)
            {
                var target = grid.GetOrCreateTile(new GridCoordinate(TargetRow, column));
                Assert.IsFalse(target.IsBlocked);
                Assert.IsFalse(IsCrushLethal(grid, TargetRow, column));
            }
        }

        [Test]
        public void Tick_PlayerStillStandingOnTrigger_ClosureNeverStarts()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 0));
            grid.GetOrCreateTile(Trigger).MarkMovingWallTrigger(TargetRow, MovingWallMode.Both);
            using var wall = new MovingWallTrap(grid, trail, scheduler);
            trail.TryAdvanceTo(Trigger);

            // "WaitingForExit" — сколько угодно тиков реального времени без
            // ухода с плиты-триггера не должны ничего запустить.
            for (var i = 0; i < 10; i++) wall.Tick(MovingWallTrap.StepSeconds);

            for (var column = 0; column < Width; column++)
                Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(TargetRow, column)).IsBlocked);
        }

        [Test]
        public void PositionChanged_LeavesTrigger_SchedulesFirstStageAfterStepSeconds()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 0));
            grid.GetOrCreateTile(Trigger).MarkMovingWallTrigger(TargetRow, MovingWallMode.Both);
            using var wall = new MovingWallTrap(grid, trail, scheduler);
            trail.TryAdvanceTo(Trigger);
            trail.TryAdvanceTo(new GridCoordinate(0, 3)); // "Triggered" — уход с плиты-триггера

            wall.Tick(MovingWallTrap.StepSeconds);

            // Both: первое кольцо — крайние колонки 0 и 4 (тот же порядок,
            // что BladeTactTrapSystem.ComputeRingColumns).
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(TargetRow, 0)).IsBlocked);
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(TargetRow, 4)).IsBlocked);
            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(TargetRow, 1)).IsBlocked);
            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(TargetRow, 2)).IsBlocked);
            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(TargetRow, 3)).IsBlocked);
        }

        [Test]
        public void Mode_Both_ClosesRingsToCenter_Irreversibly_NoReverseSweep()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 0));
            grid.GetOrCreateTile(Trigger).MarkMovingWallTrigger(TargetRow, MovingWallMode.Both);
            using var wall = new MovingWallTrap(grid, trail, scheduler);
            trail.TryAdvanceTo(Trigger);
            trail.TryAdvanceTo(new GridCoordinate(0, 3));

            wall.Tick(MovingWallTrap.StepSeconds); // стадия 0: [0,4]
            wall.Tick(MovingWallTrap.StepSeconds); // стадия 1: [1,3]

            // В отличие от BladeTactTrapSystem — закрытая ранее колонка
            // ОСТАЁТСЯ закрытой, не снимается на следующей стадии.
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(TargetRow, 0)).IsBlocked, "необратимо — не должна открыться обратно");
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(TargetRow, 4)).IsBlocked);
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(TargetRow, 1)).IsBlocked);
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(TargetRow, 3)).IsBlocked);

            wall.Tick(MovingWallTrap.StepSeconds); // стадия 2: [2] — только у width=5 (нечётная)

            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(TargetRow, 2)).IsBlocked);

            // Ряд закрылся целиком, дальнейшие тики — не-op.
            wall.Tick(MovingWallTrap.StepSeconds);
            wall.Tick(MovingWallTrap.StepSeconds);
            for (var column = 0; column < Width; column++)
                Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(TargetRow, column)).IsBlocked);
        }

        [Test]
        public void Mode_FromLeft_ClosesColumnsOneByOneLeftToRight()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 0));
            grid.GetOrCreateTile(Trigger).MarkMovingWallTrigger(TargetRow, MovingWallMode.FromLeft);
            using var wall = new MovingWallTrap(grid, trail, scheduler);
            trail.TryAdvanceTo(Trigger);
            trail.TryAdvanceTo(new GridCoordinate(0, 3));

            for (var expectedColumn = 0; expectedColumn < Width; expectedColumn++)
            {
                wall.Tick(MovingWallTrap.StepSeconds);
                Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(TargetRow, expectedColumn)).IsBlocked,
                    $"стадия {expectedColumn}: колонка {expectedColumn} должна закрыться");
                for (var laterColumn = expectedColumn + 1; laterColumn < Width; laterColumn++)
                    Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(TargetRow, laterColumn)).IsBlocked,
                        $"колонка {laterColumn} ещё не должна была закрыться на стадии {expectedColumn}");
            }
        }

        [Test]
        public void Mode_FromRight_ClosesColumnsOneByOneRightToLeft()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 0));
            grid.GetOrCreateTile(Trigger).MarkMovingWallTrigger(TargetRow, MovingWallMode.FromRight);
            using var wall = new MovingWallTrap(grid, trail, scheduler);
            trail.TryAdvanceTo(Trigger);
            trail.TryAdvanceTo(new GridCoordinate(0, 3));

            for (var i = 0; i < Width; i++)
            {
                var expectedColumn = Width - 1 - i;
                wall.Tick(MovingWallTrap.StepSeconds);
                Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(TargetRow, expectedColumn)).IsBlocked,
                    $"стадия {i}: колонка {expectedColumn} должна закрыться");
            }
        }

        [Test]
        public void Tick_PlayerCaughtOnClosingColumn_BecomesLethalAndFiresLethalTrapTriggered()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 0));
            grid.GetOrCreateTile(Trigger).MarkMovingWallTrigger(TargetRow, MovingWallMode.FromLeft);
            using var wall = new MovingWallTrap(grid, trail, scheduler);
            trail.TryAdvanceTo(Trigger);
            trail.TryAdvanceTo(new GridCoordinate(0, 3));
            trail.TryAdvanceTo(new GridCoordinate(TargetRow, 0)); // игрок встаёт на колонку, которая закроется первой стадией

            GridCoordinate? firedCoordinate = null;
            LethalTrapType? firedType = null;
            trail.LethalTrapTriggered += (coordinate, type) =>
            {
                firedCoordinate = coordinate;
                firedType = type;
            };

            wall.Tick(MovingWallTrap.StepSeconds); // стадия 0: [0] — игрок стоит здесь, ПРЯМО СЕЙЧАС, не новый ход

            Assert.AreEqual(new GridCoordinate(TargetRow, 0), firedCoordinate);
            Assert.AreEqual(LethalTrapType.MovingWallCrush, firedType);
        }

        [Test]
        public void Tick_ColumnsWithoutPlayer_BecomeBlocked_NotLava()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 0));
            grid.GetOrCreateTile(Trigger).MarkMovingWallTrigger(TargetRow, MovingWallMode.Both);
            using var wall = new MovingWallTrap(grid, trail, scheduler);
            trail.TryAdvanceTo(Trigger);
            trail.TryAdvanceTo(new GridCoordinate(0, 3)); // игрок остаётся вне целевого ряда

            wall.Tick(MovingWallTrap.StepSeconds);

            var closed = grid.GetOrCreateTile(new GridCoordinate(TargetRow, 0));
            Assert.IsTrue(closed.IsBlocked);
            Assert.IsFalse(closed.LethalTrap.HasValue, "плита без игрока становится Blocked, не Lava/лethal — MovingWallTrap не переиспользует правило Бомбы");
        }

        [Test]
        public void Tick_TilesOutsideTargetRow_AreNeverAffected()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 0));
            grid.GetOrCreateTile(Trigger).MarkMovingWallTrigger(TargetRow, MovingWallMode.Both);
            var otherRowTile = grid.GetOrCreateTile(new GridCoordinate(2, 0));
            using var wall = new MovingWallTrap(grid, trail, scheduler);
            trail.TryAdvanceTo(Trigger);
            trail.TryAdvanceTo(new GridCoordinate(0, 3));

            for (var i = 0; i < 5; i++) wall.Tick(MovingWallTrap.StepSeconds);

            Assert.IsFalse(otherRowTile.IsBlocked);
            Assert.IsFalse(otherRowTile.LethalTrap.HasValue);
        }

        [Test]
        public void PositionChanged_RevisitingAlreadyFiredTrigger_DoesNotQueueSecondClosure()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 0));
            grid.GetOrCreateTile(Trigger).MarkMovingWallTrigger(TargetRow, MovingWallMode.Both);
            using var wall = new MovingWallTrap(grid, trail, scheduler);
            trail.TryAdvanceTo(Trigger);
            trail.TryAdvanceTo(new GridCoordinate(0, 3));
            for (var i = 0; i < 5; i++) wall.Tick(MovingWallTrap.StepSeconds); // первая последовательность полностью отработала, ряд закрыт целиком

            // Возврат на плиту-триггер и повторный уход не должны поставить
            // в очередь второе, параллельное закрытие (тот же приём, что у
            // прочих систем этого семейства — здесь наблюдаемо не иначе,
            // но по крайней мере не должно быть исключения/повторной записи роли).
            trail.TryAdvanceTo(Trigger);
            trail.TryAdvanceTo(new GridCoordinate(0, 3));
            wall.Tick(MovingWallTrap.StepSeconds);

            for (var column = 0; column < Width; column++)
                Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(TargetRow, column)).IsBlocked);
        }

        [Test]
        public void PositionChanged_LeavingOneTriggerOntoAnotherTrigger_ActivatesOldBeforeDetectingNew()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 0));
            var firstTrigger = new GridCoordinate(0, 0);
            var secondTrigger = new GridCoordinate(0, 1);
            grid.GetOrCreateTile(firstTrigger).MarkMovingWallTrigger(1, MovingWallMode.Both);
            grid.GetOrCreateTile(secondTrigger).MarkMovingWallTrigger(3, MovingWallMode.Both);
            using var wall = new MovingWallTrap(grid, trail, scheduler);

            trail.TryAdvanceTo(firstTrigger);
            trail.TryAdvanceTo(secondTrigger); // шаг сразу с одного триггера на другой

            wall.Tick(MovingWallTrap.StepSeconds);

            // Старое закрытие (ряд 1) обязано было активироваться при уходе
            // с firstTrigger — не потеряться из-за того, что секундой позже
            // игрок встал на новый триггер.
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(1, 0)).IsBlocked);
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(1, 4)).IsBlocked);
            // Новый триггер ещё в фазе Detected/WaitingForExit — ничего не закрыл.
            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(3, 0)).IsBlocked);
        }

        [Test]
        public void Dispose_StopsReactingToFurtherPositionChangesAndTicks()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 0));
            grid.GetOrCreateTile(Trigger).MarkMovingWallTrigger(TargetRow, MovingWallMode.Both);
            var wall = new MovingWallTrap(grid, trail, scheduler);
            wall.Dispose();

            trail.TryAdvanceTo(Trigger);
            trail.TryAdvanceTo(new GridCoordinate(0, 3));
            wall.Tick(MovingWallTrap.StepSeconds);

            for (var column = 0; column < Width; column++)
                Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(TargetRow, column)).IsBlocked);
        }
    }
}
