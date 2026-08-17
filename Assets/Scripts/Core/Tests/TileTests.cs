using NUnit.Framework;

namespace Burmalda.Core.Tests
{
    public class TileTests
    {
        [Test]
        public void NewTile_DecayNotBegun_IsNotDestroyedAndThresholdIsNull()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            Assert.IsFalse(tile.IsDestroyed);
            Assert.IsNull(tile.DecayThresholdSeconds);
            Assert.AreEqual(0f, tile.DecayProgress01);
        }

        [Test]
        public void AdvanceDecay_WithoutBeginDecay_IsNoOp()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.AdvanceDecay(1000f);

            Assert.IsFalse(tile.IsDestroyed);
        }

        [Test]
        public void AdvanceDecay_BelowThreshold_DoesNotDestroyTile()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            tile.BeginDecay(6f);

            tile.AdvanceDecay(5f);

            Assert.IsFalse(tile.IsDestroyed);
            Assert.AreEqual(5f, tile.DecayElapsedSeconds);
        }

        [Test]
        public void AdvanceDecay_ReachesThreshold_DestroysTile()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            tile.BeginDecay(6f);

            tile.AdvanceDecay(6f);

            Assert.IsTrue(tile.IsDestroyed);
        }

        [Test]
        public void AdvanceDecay_ExceedsThreshold_DestroysTile()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            tile.BeginDecay(6f);

            tile.AdvanceDecay(9f);

            Assert.IsTrue(tile.IsDestroyed);
        }

        [Test]
        public void AdvanceDecay_AfterDestroyed_DoesNotAccumulateFurther()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            tile.BeginDecay(6f);
            tile.AdvanceDecay(6f);

            tile.AdvanceDecay(100f);

            Assert.IsTrue(tile.IsDestroyed);
            Assert.AreEqual(6f, tile.DecayElapsedSeconds);
        }

        [Test]
        public void BeginDecay_CalledTwice_KeepsFirstThreshold()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.BeginDecay(6f);
            tile.BeginDecay(999f);

            Assert.AreEqual(6f, tile.DecayThresholdSeconds);
        }

        [Test]
        public void DecayProgress01_HalfwayToThreshold_ReturnsHalf()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            tile.BeginDecay(10f);

            tile.AdvanceDecay(5f);

            Assert.AreEqual(0.5f, tile.DecayProgress01);
        }

        [Test]
        public void DecayProgress01_AfterDestroyed_IsClampedToOne()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            tile.BeginDecay(6f);

            tile.AdvanceDecay(9f);

            Assert.AreEqual(1f, tile.DecayProgress01);
        }

        [Test]
        public void NewTile_IsNotBlocked()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            Assert.IsFalse(tile.IsBlocked);
        }

        [Test]
        public void MarkBlocked_SetsIsBlockedTrue()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.MarkBlocked();

            Assert.IsTrue(tile.IsBlocked);
        }

        [Test]
        public void MarkBlocked_CalledTwice_StaysBlocked()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.MarkBlocked();
            tile.MarkBlocked();

            Assert.IsTrue(tile.IsBlocked);
        }

        [Test]
        public void NewTile_HasNoLethalTrap()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            Assert.IsFalse(tile.LethalTrap.HasValue);
        }

        [Test]
        public void MarkLethalTrap_SetsLethalTrapType()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.MarkLethalTrap(LethalTrapType.Lava);

            Assert.AreEqual(LethalTrapType.Lava, tile.LethalTrap);
        }

        [Test]
        public void MarkLethalTrap_CalledTwice_KeepsFirstType()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.MarkLethalTrap(LethalTrapType.Pit);
            tile.MarkLethalTrap(LethalTrapType.Lava);

            Assert.AreEqual(LethalTrapType.Pit, tile.LethalTrap);
        }

        [Test]
        public void NewTile_HasNoExplosiveTrapTarget()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            Assert.IsFalse(tile.ExplosiveTrapTarget.HasValue);
        }

        [Test]
        public void MarkExplosiveTrapTrigger_SetsTargetCoordinate()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            var target = new GridCoordinate(2, 1);

            tile.MarkExplosiveTrapTrigger(target);

            Assert.AreEqual(target, tile.ExplosiveTrapTarget);
        }

        [Test]
        public void MarkExplosiveTrapTrigger_CalledTwice_KeepsFirstTarget()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            var firstTarget = new GridCoordinate(2, 1);

            tile.MarkExplosiveTrapTrigger(firstTarget);
            tile.MarkExplosiveTrapTrigger(new GridCoordinate(5, 3));

            Assert.AreEqual(firstTarget, tile.ExplosiveTrapTarget);
        }

        [Test]
        public void NewTile_HasNoTimedTrapTargetAndIsNotActive()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            Assert.IsFalse(tile.TimedTrapTarget.HasValue);
            Assert.IsFalse(tile.TimedTrapKind.HasValue);
            Assert.IsFalse(tile.IsTimedTrapActive);
        }

        [Test]
        public void MarkTimedTrapTrigger_SetsTargetCoordinateAndKind()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            var target = new GridCoordinate(2, 1);

            tile.MarkTimedTrapTrigger(target, TimedTrapType.Blade);

            Assert.AreEqual(target, tile.TimedTrapTarget);
            Assert.AreEqual(TimedTrapType.Blade, tile.TimedTrapKind);
        }

        [Test]
        public void MarkTimedTrapTrigger_CalledTwice_KeepsFirstTargetAndKind()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            var firstTarget = new GridCoordinate(2, 1);

            tile.MarkTimedTrapTrigger(firstTarget, TimedTrapType.Arrow);
            tile.MarkTimedTrapTrigger(new GridCoordinate(5, 3), TimedTrapType.Blade);

            Assert.AreEqual(firstTarget, tile.TimedTrapTarget);
            Assert.AreEqual(TimedTrapType.Arrow, tile.TimedTrapKind);
        }

        [Test]
        public void ArmTimedTrap_SetsActiveAndKind()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.ArmTimedTrap(TimedTrapType.Arrow);

            Assert.IsTrue(tile.IsTimedTrapActive);
            Assert.AreEqual(TimedTrapType.Arrow, tile.TimedTrapKind);
        }

        [Test]
        public void DisarmTimedTrap_ClearsActive_KeepsKind()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            tile.ArmTimedTrap(TimedTrapType.Blade);

            tile.DisarmTimedTrap();

            Assert.IsFalse(tile.IsTimedTrapActive);
            Assert.AreEqual(TimedTrapType.Blade, tile.TimedTrapKind); // историческое значение, IsTimedTrapActive уже false — не опасна
        }

        [Test]
        public void NewTile_IsNotLeverAndHasNoGateTargets()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            Assert.IsFalse(tile.IsLever);
            Assert.IsNull(tile.LeverGateTargets);
        }

        [Test]
        public void MarkLever_SetsIsLeverAndGateTargets()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            var targets = new[] { new GridCoordinate(1, 2), new GridCoordinate(1, 3) };

            tile.MarkLever(targets);

            Assert.IsTrue(tile.IsLever);
            CollectionAssert.AreEqual(targets, tile.LeverGateTargets);
        }

        [Test]
        public void MarkLever_CalledTwice_KeepsFirstTargets()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            var firstTargets = new[] { new GridCoordinate(1, 2) };

            tile.MarkLever(firstTargets);
            tile.MarkLever(new[] { new GridCoordinate(9, 9) });

            CollectionAssert.AreEqual(firstTargets, tile.LeverGateTargets);
        }

        [Test]
        public void NewTile_IsNotGatedAndGateIsNotOpen()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            Assert.IsFalse(tile.IsGated);
            Assert.IsFalse(tile.IsLeverGateOpen);
        }

        [Test]
        public void MarkGated_SetsIsGatedTrue_GateStaysClosed()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.MarkGated();

            Assert.IsTrue(tile.IsGated);
            Assert.IsFalse(tile.IsLeverGateOpen);
        }

        [Test]
        public void OpenLeverGate_SetsIsLeverGateOpenTrue()
        {
            var tile = new Tile(new GridCoordinate(1, 1));
            tile.MarkGated();

            tile.OpenLeverGate();

            Assert.IsTrue(tile.IsLeverGateOpen);
        }

        [Test]
        public void NewTile_IsNotManaSourceAndIsNotKeySource()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            Assert.IsFalse(tile.IsManaSource);
            Assert.IsFalse(tile.IsKeySource);
        }

        [Test]
        public void MarkManaSource_SetsIsManaSourceTrue()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.MarkManaSource();

            Assert.IsTrue(tile.IsManaSource);
            Assert.IsFalse(tile.IsKeySource);
        }

        [Test]
        public void MarkKeySource_SetsIsKeySourceTrue()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.MarkKeySource();

            Assert.IsTrue(tile.IsKeySource);
            Assert.IsFalse(tile.IsManaSource);
        }

        [Test]
        public void NewTile_IsNotAltar()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            Assert.IsFalse(tile.IsAltar);
        }

        [Test]
        public void MarkAltar_SetsIsAltarTrue()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.MarkAltar();

            Assert.IsTrue(tile.IsAltar);
        }

        [Test]
        public void NewTile_IsNotBoss()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            Assert.IsFalse(tile.IsBoss);
        }

        [Test]
        public void MarkBoss_SetsIsBossTrue()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.MarkBoss();

            Assert.IsTrue(tile.IsBoss);
        }

        [Test]
        public void NewTile_BossRoomTileIsNull()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            Assert.IsNull(tile.BossRoomTile);
            Assert.IsNull(tile.RiftSubtype);
        }

        [Test]
        public void MarkBossRoomTile_Vein_SetsBossRoomTile()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.MarkBossRoomTile(BossRoomTileKind.Vein);

            Assert.AreEqual(BossRoomTileKind.Vein, tile.BossRoomTile);
            Assert.IsNull(tile.RiftSubtype);
        }

        [Test]
        public void MarkBossRoomTile_CalledTwice_SecondCallIsNoOp()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.MarkBossRoomTile(BossRoomTileKind.Vein);
            tile.MarkBossRoomTile(BossRoomTileKind.Resonance);

            Assert.AreEqual(BossRoomTileKind.Vein, tile.BossRoomTile, "первая пометка не должна перезаписываться");
        }

        [Test]
        public void MarkBossRoomRift_SetsKindRiftAndSubtype()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.MarkBossRoomRift(BossRoomRiftSubtype.Resonance);

            Assert.AreEqual(BossRoomTileKind.Rift, tile.BossRoomTile);
            Assert.AreEqual(BossRoomRiftSubtype.Resonance, tile.RiftSubtype);
        }

        [Test]
        public void MarkBossRoomRift_AfterMarkBossRoomTile_IsNoOp()
        {
            var tile = new Tile(new GridCoordinate(1, 1));

            tile.MarkBossRoomTile(BossRoomTileKind.Vein);
            tile.MarkBossRoomRift(BossRoomRiftSubtype.Vein);

            Assert.AreEqual(BossRoomTileKind.Vein, tile.BossRoomTile);
            Assert.IsNull(tile.RiftSubtype);
        }
    }
}
