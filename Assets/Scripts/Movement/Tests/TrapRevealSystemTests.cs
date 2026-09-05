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

        // Владелец, 2026-09-05 «оставить только пять новых ловушек»: раньше
        // здесь были Tick_PitTile_RevealsSignature/Tick_ExplosionTile_DoesNotReveal
        // — Pit/Explosion удалены вместе с Core.TrapSignature, а с ними и
        // сама идея "некоторые LethalTrapType скрыты" — ни один из пяти
        // оставшихся типов не гейтится примериванием (см. doc-комментарий
        // TrapRevealSystem.HasHiddenDanger), только их ТРИГГЕРЫ. Тест ниже
        // (Lava) остаётся — Лава была видна всегда и раньше, это не
        // регрессия, а неизменная часть поведения.
        [Test]
        public void Tick_LavaTile_DoesNotReveal()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).MarkLethalTrap(LethalTrapType.Lava);
            var system = new TrapRevealSystem(grid);

            system.Tick(coordinate);

            Assert.IsFalse(grid.GetOrCreateTile(coordinate).IsDangerSignatureRevealed);
        }

        [Test]
        public void Tick_ActiveArrowWaveTile_DoesNotReveal()
        {
            // Активная (уже сработавшая) ловушка с таймингом — опасность
            // прямо сейчас, видна всегда, без гейта примериванием (тот же
            // принцип, что Лава выше) — в отличие от её ТРИГГЕРА (см. тесты
            // ниже).
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).TransitionToLethalTrap(LethalTrapType.ArrowWave);
            var system = new TrapRevealSystem(grid);

            system.Tick(coordinate);

            Assert.IsFalse(grid.GetOrCreateTile(coordinate).IsDangerSignatureRevealed);
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

        // issue #216 — триггер Лавы скрыт тем же приёмом, что и прочие триггеры выше.
        [Test]
        public void Tick_LavaTriggerTile_RevealsSignature()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).MarkLavaTrigger();
            var system = new TrapRevealSystem(grid);

            system.Tick(coordinate);

            Assert.IsTrue(grid.GetOrCreateTile(coordinate).IsDangerSignatureRevealed);
        }

        // Владелец, 2026-09-04 (отменяет issue #193): рычаг видим всегда,
        // не опасность и не скрытый механизм — примеривание не должно
        // требоваться, чтобы его заметить.
        [Test]
        public void Tick_LeverTile_DoesNotReveal()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).MarkLever(new System.Collections.Generic.List<GridCoordinate> { new GridCoordinate(2, 2) });
            var system = new TrapRevealSystem(grid);

            system.Tick(coordinate);

            Assert.IsFalse(grid.GetOrCreateTile(coordinate).IsDangerSignatureRevealed);
        }

        [Test]
        public void Tick_RevealsSignature_RaisesSignatureRevealedEvent()
        {
            var grid = new TunnelGrid(Width);
            var coordinate = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(coordinate).MarkBombTrigger();
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
            grid.GetOrCreateTile(coordinate).MarkBombTrigger();
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
            grid.GetOrCreateTile(hidden).MarkBombTrigger();
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
