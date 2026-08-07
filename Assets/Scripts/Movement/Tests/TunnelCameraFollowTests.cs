using Burmalda.Core;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    public class TunnelCameraFollowTests
    {
        private const float TileSize = 1f;
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail, WorldGridProjection projection) CreateTrail()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var projection = new WorldGridProjection(TileSize, Width);
            return (grid, trail, projection);
        }

        [Test]
        public void Constructor_StartOfRun_TargetAndCurrentPositionMatchStartTileNoLagYet()
        {
            var (_, trail, projection) = CreateTrail();

            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            // Плита (0,2): x=(2-2.5+0.5)*1=0, z=(0+0.5)*1=0.5; трейлинг-ряд max(0,0-5)=0 — офсета ещё нет.
            var expected = new Vector3(0f, 0f, 0.5f);
            Assert.AreEqual(expected, follow.TargetPosition);
            Assert.AreEqual(expected, follow.CurrentPosition);
        }

        [Test]
        public void OnTrailAdvanced_WithinFirstFiveRows_TargetStaysClampedToRowZero()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            for (var row = 1; row <= 5; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            // legacy/burmolda_demo.html: cameraTargetRow = Math.max(0, r-5) — для r<=5 это всегда 0.
            Assert.AreEqual(new Vector3(0f, 0f, 0.5f), follow.TargetPosition);
        }

        [Test]
        public void OnTrailAdvanced_BeyondFiveRows_TargetTrailsFiveRowsBehindPlayer()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            for (var row = 1; row <= 6; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            // Игрок на ряду 6 -> целевой ряд камеры max(0,6-5)=1: x=0, z=(1+0.5)=1.5.
            Assert.AreEqual(new Vector3(0f, 0f, 1.5f), follow.TargetPosition);
        }

        [Test]
        public void OnTrailAdvanced_LateralMove_TargetColumnFollowsImmediatelyWithoutLag()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            for (var row = 1; row <= 6; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));
            trail.TryAdvanceTo(new GridCoordinate(6, 3)); // шаг вбок, ряд не меняется

            // Столбец не отстаёт (как в прототипе — только ряд): col3 -> x=(3-2.5+0.5)=1, ряд по-прежнему 1 -> z=1.5.
            Assert.AreEqual(new Vector3(1f, 0f, 1.5f), follow.TargetPosition);
        }

        [Test]
        public void Tick_AppliesLiteralSmoothingFactorFromPrototypeOncePerCall()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            for (var row = 1; row <= 6; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2)); // Target=(0,0,1.5), Current всё ещё (0,0,0.5)

            follow.Tick();

            // legacy/burmolda_demo.html: cameraRow += (cameraTargetRow-cameraRow)*0.045 — не масштабируется на deltaTime.
            var expectedZ = 0.5f + (1.5f - 0.5f) * 0.045f;
            Assert.AreEqual(new Vector3(0f, 0f, expectedZ), follow.CurrentPosition);
        }

        [Test]
        public void Tick_RepeatedCalls_ConvergesTowardsTarget()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            for (var row = 1; row <= 6; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            for (var i = 0; i < 1000; i++) follow.Tick();

            Assert.AreEqual(follow.TargetPosition.z, follow.CurrentPosition.z, 1e-4f);
        }

        [Test]
        public void TargetRotation_CameraAboveAndBehindPlayer_LooksForwardAndDown()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 3f, 0f));

            for (var row = 1; row <= 6; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            var forward = follow.TargetRotation * Vector3.forward;

            Assert.Less(forward.y, 0f, "камера выше игрока — должна смотреть вниз");
            Assert.Greater(forward.z, 0f, "игрок впереди по тоннелю — должна смотреть вперёд");
            Assert.AreEqual(0f, forward.x, 1e-5f, "нет бокового смещения между камерой и игроком");
        }

        [Test]
        public void TargetRotation_CameraAtSamePositionAsPlayer_FallsBackToIdentity()
        {
            var (_, trail, projection) = CreateTrail();

            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            // На старте трейлинг-ряд ещё клампится к позиции игрока (row0) — направление нулевое.
            Assert.AreEqual(Quaternion.identity, follow.TargetRotation);
        }

        [Test]
        public void Dispose_StopsUpdatingTargetOnFurtherTrailAdvances()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);
            var targetBeforeDispose = follow.TargetPosition;

            follow.Dispose();
            for (var row = 1; row <= 6; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            Assert.AreEqual(targetBeforeDispose, follow.TargetPosition);
        }
    }
}
