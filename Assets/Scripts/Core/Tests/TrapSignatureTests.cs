using NUnit.Framework;

namespace Burmalda.Core.Tests
{
    /// <summary>Issue #163: какие ловушки считаются скрытыми (сигнатура вместо точного типа) — см. doc-комментарий TrapSignature.</summary>
    public class TrapSignatureTests
    {
        private static Tile CreateTile() => new Tile(new GridCoordinate(1, 0));

        [Test]
        public void Pit_IsHiddenLethalTrap()
        {
            var tile = CreateTile();
            tile.MarkLethalTrap(LethalTrapType.Pit);

            Assert.IsTrue(TrapSignature.IsHiddenLethalTrap(tile));
        }

        [Test]
        public void Explosion_IsHiddenLethalTrap()
        {
            var tile = CreateTile();
            tile.MarkLethalTrap(LethalTrapType.Explosion);

            Assert.IsTrue(TrapSignature.IsHiddenLethalTrap(tile));
        }

        // PRD v8 §4.2: "заблокированные плиты и лава остаются видимыми как раньше".
        [Test]
        public void Lava_IsNotHiddenLethalTrap()
        {
            var tile = CreateTile();
            tile.MarkLethalTrap(LethalTrapType.Lava);

            Assert.IsFalse(TrapSignature.IsHiddenLethalTrap(tile));
        }

        [Test]
        public void NoLethalTrap_IsNotHiddenLethalTrap()
        {
            var tile = CreateTile();

            Assert.IsFalse(TrapSignature.IsHiddenLethalTrap(tile));
        }

        [Test]
        public void NullTile_ReturnsFalse_DoesNotThrow()
        {
            Assert.IsFalse(TrapSignature.IsHiddenLethalTrap(null));
        }

        // Заблокированная плита (не ловушка вовсе) — вне классификации этого класса.
        [Test]
        public void BlockedTile_WithoutLethalTrap_IsNotHiddenLethalTrap()
        {
            var tile = CreateTile();
            tile.MarkBlocked();

            Assert.IsFalse(TrapSignature.IsHiddenLethalTrap(tile));
        }
    }
}
