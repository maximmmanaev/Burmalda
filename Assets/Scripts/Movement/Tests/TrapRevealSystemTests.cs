using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class TrapRevealSystemTests
    {
        private const int Width = 5;

        [Test]
        public void Tick_NullPreviewTarget_DoesNothing()
        {
            var grid = new TunnelGrid(Width);
            var system = new TrapRevealSystem(grid);

            Assert.DoesNotThrow(() => system.Tick(null));
        }

        [Test]
        public void Tick_PreviewTargetNotMaterialized_DoesNothing()
        {
            var grid = new TunnelGrid(Width);
            var system = new TrapRevealSystem(grid);

            Assert.DoesNotThrow(() => system.Tick(new GridCoordinate(3, 2)));
        }

        [Test]
        public void Tick_OrdinaryTile_DoesNotReveal()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate);
            var system = new TrapRevealSystem(grid);

            system.Tick(coordinate);

            Assert.IsFalse(grid.GetOrCreateTile(coordinate).IsDangerSignatureRevealed);
        }

        [Test]
        public void Tick_PitTile_RevealsSignature()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).MarkLethalTrap(LethalTrapType.Pit);
            var system = new TrapRevealSystem(grid);

            system.Tick(coordinate);

            Assert.IsTrue(grid.GetOrCreateTile(coordinate).IsDangerSignatureRevealed);
        }

        [Test]
        public void Tick_ExplosionTile_RevealsSignature()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).MarkLethalTrap(LethalTrapType.Explosion);
            var system = new TrapRevealSystem(grid);

            system.Tick(coordinate);

            Assert.IsTrue(grid.GetOrCreateTile(coordinate).IsDangerSignatureRevealed);
        }

        [Test]
        public void Tick_LavaTile_DoesNotReveal()
        {
            // Лава видна всегда, не скрыта — TrapSignature.IsHiddenLethalTrap
            // её не считает скрытой (см. её doc-комментарий).
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).MarkLethalTrap(LethalTrapType.Lava);
            var system = new TrapRevealSystem(grid);

            system.Tick(coordinate);

            Assert.IsFalse(grid.GetOrCreateTile(coordinate).IsDangerSignatureRevealed);
        }

        [Test]
        public void Tick_ExplosiveTriggerTile_RevealsSignature()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).MarkExplosiveTrapTrigger(new GridCoordinate(2, 2));
            var system = new TrapRevealSystem(grid);

            system.Tick(coordinate);

            Assert.IsTrue(grid.GetOrCreateTile(coordinate).IsDangerSignatureRevealed);
        }

        [Test]
        public void Tick_TimedTrapTriggerTile_RevealsSignature()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).MarkTimedTrapTrigger(new GridCoordinate(2, 2), TimedTrapType.Blade);
            var system = new TrapRevealSystem(grid);

            system.Tick(coordinate);

            Assert.IsTrue(grid.GetOrCreateTile(coordinate).IsDangerSignatureRevealed);
        }

        [Test]
        public void Tick_RevealsSignature_RaisesSignatureRevealedEvent()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).MarkLethalTrap(LethalTrapType.Pit);
            var system = new TrapRevealSystem(grid);
            GridCoordinate? revealed = null;
            system.SignatureRevealed += c => revealed = c;

            system.Tick(coordinate);

            Assert.AreEqual(coordinate, revealed);
        }

        [Test]
        public void Tick_AlreadyRevealed_DoesNotRaiseEventAgain()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).MarkLethalTrap(LethalTrapType.Pit);
            var system = new TrapRevealSystem(grid);
            var raiseCount = 0;
            system.SignatureRevealed += _ => raiseCount++;

            system.Tick(coordinate); // первый раз — раскрывает
            system.Tick(coordinate); // палец всё ещё там — уже раскрыто, второго события быть не должно
            system.Tick(coordinate);

            Assert.AreEqual(1, raiseCount, "событие должно сработать только один раз — на момент реального раскрытия, не на каждый кадр примеривания");
        }

        [Test]
        public void Tick_MovingPreviewAwayAndBack_DoesNotRaiseEventAgain()
        {
            var grid = new TunnelGrid(Width);
            var hidden = new GridCoordinate(1, 2);
            var ordinary = new GridCoordinate(1, 3);
            grid.GetOrCreateTile(hidden).MarkLethalTrap(LethalTrapType.Pit);
            grid.GetOrCreateTile(ordinary);
            var system = new TrapRevealSystem(grid);
            var raiseCount = 0;
            system.SignatureRevealed += _ => raiseCount++;

            system.Tick(hidden); // раскрывает
            system.Tick(ordinary); // палец отвели
            system.Tick(hidden); // вернулись — уже раскрыто

            Assert.AreEqual(1, raiseCount);
            Assert.IsTrue(grid.GetOrCreateTile(hidden).IsDangerSignatureRevealed, "раскрытие остаётся навсегда, даже после того как палец отвели");
        }
    }
}
