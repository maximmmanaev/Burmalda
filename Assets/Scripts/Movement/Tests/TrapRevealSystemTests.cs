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

        // Задача «разрушение плиты», продолжение (владелец, 2026-09-01):
        // ИНВЕРСИЯ прежнего теста с тем же именем — сработавший взрыв
        // больше не входит в TrapSignature.IsHiddenLethalTrap (см. её
        // doc-комментарий), значит нечего раскрывать через этот флаг — он
        // и так уже виден всегда (резолверы проверяют LethalTrap ==
        // Explosion напрямую, без гейта IsDangerSignatureRevealed). Тот же
        // случай, что и Лава ниже.
        [Test]
        public void Tick_ExplosionTile_DoesNotReveal()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).TransitionToLethalTrap(LethalTrapType.Explosion);
            var system = new TrapRevealSystem(grid);

            system.Tick(coordinate);

            Assert.IsFalse(grid.GetOrCreateTile(coordinate).IsDangerSignatureRevealed);
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

        // issue #213 — триггер Стрелы скрыт тем же приёмом, что и прочие триггеры выше.
        [Test]
        public void Tick_ArrowWaveTriggerTile_RevealsSignature()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).MarkArrowWaveTrigger(targetRow: 2, RowWaveDirection.LeftToRight);
            var system = new TrapRevealSystem(grid);

            system.Tick(coordinate);

            Assert.IsTrue(grid.GetOrCreateTile(coordinate).IsDangerSignatureRevealed);
        }

        // issue #214 — триггер Бомбы скрыт тем же приёмом, что и прочие триггеры выше.
        [Test]
        public void Tick_BombTriggerTile_RevealsSignature()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).MarkBombTrigger();
            var system = new TrapRevealSystem(grid);

            system.Tick(coordinate);

            Assert.IsTrue(grid.GetOrCreateTile(coordinate).IsDangerSignatureRevealed);
        }

        // issue #215 — триггер Лезвий скрыт тем же приёмом, что и прочие триггеры выше.
        [Test]
        public void Tick_BladeTactTriggerTile_RevealsSignature()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).MarkBladeTactTrigger(targetRow: 1);
            var system = new TrapRevealSystem(grid);

            system.Tick(coordinate);

            Assert.IsTrue(grid.GetOrCreateTile(coordinate).IsDangerSignatureRevealed);
        }

        // issue #217 — триггер Падающего камня скрыт тем же приёмом, что и прочие триггеры выше.
        [Test]
        public void Tick_FallingRockTriggerTile_RevealsSignature()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).MarkFallingRockTrigger();
            var system = new TrapRevealSystem(grid);

            system.Tick(coordinate);

            Assert.IsTrue(grid.GetOrCreateTile(coordinate).IsDangerSignatureRevealed);
        }

        // Issue #193 (владелец, 2026-09-01): рычаг теперь скрыт той же
        // сигнатурой, что и ловушки/триггеры — "две разновидности
        // механизма с разной видимостью научат игрока неверному правилу".
        [Test]
        public void Tick_LeverTile_RevealsSignature()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).MarkLever(new System.Collections.Generic.List<GridCoordinate> { new GridCoordinate(2, 2) });
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
