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
        public void OnTrailAdvanced_LateralMove_TargetColumnStaysFixedAtTunnelCenter()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            for (var row = 1; row <= 6; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));
            trail.TryAdvanceTo(new GridCoordinate(6, 3)); // шаг вбок, ряд не меняется

            // #62: камера двигается только вперёд-назад (по Z) — X зафиксирован
            // на центре ширины тоннеля (Width/2=2 -> x=0), не следует за
            // столбцом игрока, даже когда игрок сместился в столбец 3.
            Assert.AreEqual(new Vector3(0f, 0f, 1.5f), follow.TargetPosition);
        }

        [Test]
        public void OnTrailAdvanced_DiagonalMovementAcrossColumns_TargetColumnNeverLeavesTunnelCenter()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            // Зигзаг через разные столбцы (0..4 при ширине 5) — камера не
            // должна уезжать влево-вправо ни на одном шаге.
            var path = new[]
            {
                new GridCoordinate(1, 1),
                new GridCoordinate(2, 0),
                new GridCoordinate(3, 1),
                new GridCoordinate(4, 2),
                new GridCoordinate(5, 3),
                new GridCoordinate(6, 4),
            };
            foreach (var step in path)
            {
                Assert.IsTrue(trail.TryAdvanceTo(step), $"шаг {step} должен быть валиден для теста");
                Assert.AreEqual(0f, follow.TargetPosition.x, 1e-5f, $"X цели камеры не должен меняться на шаге {step}");
            }
        }

        [Test]
        public void OnPositionChanged_RevisitOlderTileAfterGoingDeep_TargetMovesBackward()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            for (var row = 1; row <= 10; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));
            var targetZAtDepth = follow.TargetPosition.z; // трейлинг-ряд max(0,10-5)=5 -> z=5.5

            trail.TryAdvanceTo(new GridCoordinate(9, 2)); // шаг назад на уже пройденную плиту (#61)

            // Без подписки на PositionChanged камера осталась бы на месте —
            // трейлинг-ряд должен пересчитаться от новой CurrentPosition (9),
            // а не только от последней НОВОЙ плитки.
            Assert.Less(follow.TargetPosition.z, targetZAtDepth);
            Assert.AreEqual(new Vector3(0f, 0f, 4.5f), follow.TargetPosition); // трейлинг-ряд max(0,9-5)=4 -> z=4.5
        }

        [Test]
        public void Tick_AppliesSmoothingFactorOncePerCall()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            for (var row = 1; row <= 6; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2)); // Target=(0,0,1.5), Current всё ещё (0,0,0.5)

            follow.Tick();

            // Черновой тюнинг темпа по запросу владельца продукта (без issue,
            // см. changelog) — камера ощущалась слишком резкой/дёрганой,
            // константа дважды замедлена относительно буквального значения
            // из прототипа (0.045 -> 0.02 -> 0.01); не масштабируется на deltaTime.
            var expectedZ = 0.5f + (1.5f - 0.5f) * 0.01f;
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
        public void TargetRotation_IsFixedAt35DegreesPitch_RegardlessOfHeightOffset()
        {
            // Хотфикс (18.4° -> 29° -> 35°): поворот камеры больше не
            // вычисляется динамически «взглядом вперёд» — Rotation X
            // зафиксирован. При vFOV=60° горизонт исчезает из кадра при
            // pitch >= 30° (vFOV/2); 35° — с запасом на покачивание камеры.
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 4f, -1f));

            Assert.AreEqual(35f, follow.TargetRotation.eulerAngles.x, 1e-4f);
            Assert.AreEqual(35f, follow.CurrentRotation.eulerAngles.x, 1e-4f);
        }

        [Test]
        public void TargetRotation_NoYawOrRoll_OnlyPitch()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 4f, -1f));

            var euler = follow.TargetRotation.eulerAngles;

            Assert.AreEqual(0f, euler.y, 1e-4f, "поворота влево-вправо (yaw) быть не должно");
            Assert.AreEqual(0f, euler.z, 1e-4f, "крена (roll) быть не должно");
        }

        [Test]
        public void TargetRotation_DoesNotChangeAsTrailAdvances()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 4f, -1f));
            var rotationAtStart = follow.TargetRotation;

            for (var row = 1; row <= 10; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));
            trail.TryAdvanceTo(new GridCoordinate(9, 2)); // шаг назад (#61)

            Assert.AreEqual(rotationAtStart, follow.TargetRotation);
        }

        [Test]
        public void TargetRotation_IsIndependentOfHeightOffset()
        {
            // В отличие от старой look-at-логики, поворот больше не зависит
            // от heightOffset вообще — только позиция (ComputeTargetPosition).
            var (_, trailA, projectionA) = CreateTrail();
            var followA = new TunnelCameraFollow(trailA, projectionA, Vector3.zero);
            var (_, trailB, projectionB) = CreateTrail();
            var followB = new TunnelCameraFollow(trailB, projectionB, new Vector3(0f, 10f, -5f));

            Assert.AreEqual(followA.TargetRotation, followB.TargetRotation);
        }

        [Test]
        public void Constructor_CustomPitchDegrees_OverridesDefault()
        {
            var (_, trail, projection) = CreateTrail();

            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, pitchDegrees: 20f);

            Assert.AreEqual(20f, follow.TargetRotation.eulerAngles.x, 1e-4f);
        }

        [Test]
        public void PitchDegrees_SetAfterConstruction_UpdatesRotationImmediately()
        {
            // Живая правка из инспектора (TunnelCameraController) — не должна
            // требовать пересборки Follow/рестарта забега, чтобы применяться.
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            follow.PitchDegrees = 40f;

            Assert.AreEqual(40f, follow.TargetRotation.eulerAngles.x, 1e-4f);
            Assert.AreEqual(40f, follow.CurrentRotation.eulerAngles.x, 1e-4f);
        }

        [Test]
        public void HeightOffset_SetAfterConstruction_RecomputesTargetPositionImmediately()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            follow.HeightOffset = new Vector3(0f, 4f, -1f);

            // Игрок всё ещё на старте (0,2): x=0, z=0.5 (см. первый тест) + новый offset.
            Assert.AreEqual(new Vector3(0f, 4f, -0.5f), follow.TargetPosition);
        }

        [Test]
        public void HeightOffset_SetAfterTrailAdvanced_UsesCurrentPositionNotStale()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);
            for (var row = 1; row <= 6; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2)); // трейлинг-ряд 1 -> z=1.5, см. тест выше

            follow.HeightOffset = new Vector3(0f, 2f, 0f);

            Assert.AreEqual(new Vector3(0f, 2f, 1.5f), follow.TargetPosition);
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
