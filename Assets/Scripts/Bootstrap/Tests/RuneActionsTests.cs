using Burmalda.Artifacts;
using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Bootstrap.Tests
{
    /// <summary>Руны славянских богов по тапу (PRD v10 §20.4): Морок, Распутье, Удар Перуна, Ночь Чернобога.</summary>
    public class RuneActionsTests
    {
        private float _now;

        private (RuneActions actions, RelicEffectState effects, TunnelGrid grid, GridTraceTrail trail) Make(params string[] ids)
        {
            var build = new RunRelicBuild();
            foreach (var id in ids) build.TryAdd(RelicCatalog.Get(id));
            var effects = new RelicEffectState(() => build, () => _now, () => 1);
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            return (new RuneActions(effects, grid, trail), effects, grid, trail);
        }

        [Test]
        public void Rasputye_RevealsTypesOfAllTriggersInNext5Rows_Only()
        {
            var (actions, _, grid, _) = Make(RelicEffectState.RasputeRune);
            var inside = new[] { new GridCoordinate(1, 0), new GridCoordinate(3, 2), new GridCoordinate(5, 4) };
            foreach (var c in inside) grid.GetOrCreateTile(c).MarkBombTrigger();
            var outside = grid.GetOrCreateTile(new GridCoordinate(6, 2));
            outside.MarkBombTrigger();
            var plain = grid.GetOrCreateTile(new GridCoordinate(2, 2));

            Assert.IsTrue(actions.Activate(RelicEffectState.RasputeRune));

            foreach (var c in inside) Assert.IsTrue(grid.GetOrCreateTile(c).IsTrapTypeRevealed, c.ToString());
            Assert.IsFalse(outside.IsTrapTypeRevealed, "дальше 5 рядов не видно");
            Assert.IsFalse(plain.IsTrapTypeRevealed, "обычные плиты не трогаем");
        }

        [Test]
        public void Rasputye_IsOneShot()
        {
            var (actions, _, _, _) = Make(RelicEffectState.RasputeRune);

            Assert.IsTrue(actions.Activate(RelicEffectState.RasputeRune));
            Assert.IsFalse(actions.Activate(RelicEffectState.RasputeRune));
        }

        [Test]
        public void PerunStrike_BreaksOneAdjacentWall_ForwardFirst()
        {
            var (actions, _, grid, _) = Make(RelicEffectState.PerunStrikeRune);
            var forward = grid.GetOrCreateTile(new GridCoordinate(1, 2));
            var side = grid.GetOrCreateTile(new GridCoordinate(0, 1));
            forward.MarkBlocked();
            side.MarkBlocked();
            var far = grid.GetOrCreateTile(new GridCoordinate(2, 2));
            far.MarkBlocked();

            Assert.IsTrue(actions.Activate(RelicEffectState.PerunStrikeRune));

            Assert.IsFalse(forward.IsBlocked, "ближайшая впереди разбита");
            Assert.IsTrue(side.IsBlocked, "только одна стена");
            Assert.IsTrue(far.IsBlocked, "неблизкие не трогаем");
        }

        [Test]
        public void PerunStrike_NoAdjacentWall_IsNotSpent()
        {
            var (actions, effects, grid, _) = Make(RelicEffectState.PerunStrikeRune);
            grid.GetOrCreateTile(new GridCoordinate(3, 2)).MarkBlocked();

            Assert.IsFalse(actions.Activate(RelicEffectState.PerunStrikeRune));
            Assert.IsTrue(effects.CanUseRune(RelicEffectState.PerunStrikeRune), "цели нет — Руна осталась");
        }

        [Test]
        public void MorokAndNight_StartTheirTimers()
        {
            var (actions, effects, _, _) = Make(RelicEffectState.MorokRune, RelicEffectState.NightRune);

            Assert.IsTrue(actions.Activate(RelicEffectState.MorokRune));
            Assert.IsTrue(actions.Activate(RelicEffectState.NightRune));

            Assert.IsFalse(effects.StepsActivateTraps);
            Assert.IsTrue(effects.CollapseHeld);
        }

        [Test]
        public void RuneNotInBuild_DoesNothing()
        {
            var (actions, _, _, _) = Make();
            Assert.IsFalse(actions.Activate(RelicEffectState.MorokRune));
        }
    }
}
