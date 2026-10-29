using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    /// <summary>Эффекты Реликвий на ловушках: Берестяной оберег (стены медленнее) и Морок (шаги не активируют ловушки).</summary>
    public class RunEffectsTrapTests
    {
        [TearDown]
        public void ResetEffects() => RunEffects.Current = RunEffects.Neutral;

        private static (TunnelGrid grid, GridTraceTrail trail, MovingWallTrap wall) Armed()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 0));
            grid.GetOrCreateTile(new GridCoordinate(0, 1)).MarkMovingWallTrigger(1, MovingWallMode.Both);
            var wall = new MovingWallTrap(grid, trail, new RealTimeThreatScheduler());
            trail.TryAdvanceTo(new GridCoordinate(0, 1));
            return (grid, trail, wall);
        }

        [Test]
        public void WallSlowdown_StretchesEachClosingStep()
        {
            RunEffects.Current = new ConfigurableRunEffects { WallCloseSlowdown = 2f };
            var (grid, trail, wall) = Armed();
            using (wall)
            {
                trail.TryAdvanceTo(new GridCoordinate(0, 0)); // уход с триггера

                wall.Tick(MovingWallTrap.StepSeconds);
                Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(1, 0)).IsBlocked, "при замедлении ×2 первый шаг ещё не наступил");

                wall.Tick(MovingWallTrap.StepSeconds);
                Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(1, 0)).IsBlocked);
            }
        }

        [Test]
        public void NoEffects_WallClosesAtNormalSpeed()
        {
            var (grid, trail, wall) = Armed();
            using (wall)
            {
                trail.TryAdvanceTo(new GridCoordinate(0, 0));
                wall.Tick(MovingWallTrap.StepSeconds);

                Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(1, 0)).IsBlocked);
            }
        }

        [Test]
        public void Morok_LeavingTrigger_DoesNotActivateTheTrap()
        {
            RunEffects.Current = new ConfigurableRunEffects { StepsActivateTraps = false };
            var (grid, trail, wall) = Armed();
            using (wall)
            {
                trail.TryAdvanceTo(new GridCoordinate(0, 0));
                for (var i = 0; i < 10; i++) wall.Tick(MovingWallTrap.StepSeconds);

                for (var c = 0; c < 5; c++) Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(1, c)).IsBlocked, $"колонка {c}");
            }
        }
    }
}
