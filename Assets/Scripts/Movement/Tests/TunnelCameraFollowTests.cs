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
        public void TargetRotation_LateralMove_NoLeftRightYaw()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 3f, 0f));

            for (var row = 1; row <= 6; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));
            trail.TryAdvanceTo(new GridCoordinate(6, 4)); // шаг вбок к краю тоннеля, ряд не меняется

            var forward = follow.TargetRotation * Vector3.forward;

            // Камера смотрит по центру тоннеля, а не на реальный столбец игрока —
            // иначе она поворачивается влево-вправо при каждом боковом шаге.
            Assert.AreEqual(0f, forward.x, 1e-5f, "камера не должна поворачиваться влево-вправо при боковом движении игрока");
        }

        [Test]
        public void TargetRotation_AtVeryStart_CameraAndLookAtCoincide_FallsBackToIdentity()
        {
            // На самом старте забега камера (трейлинг-ряд ещё клампится к 0)
            // и запас взгляда вперёд (см. LookAheadScale ниже) оба совпадают
            // с позицией игрока — без высоты камеры это ровно одна и та же
            // точка, направление нулевое. Тот же degenerate-случай, что был
            // до добавления взгляда с запасом вперёд.
            var (_, trail, projection) = CreateTrail();

            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            Assert.AreEqual(Quaternion.identity, follow.TargetRotation);
        }

        [Test]
        public void TargetRotation_AtVeryStart_WithHeightOffset_LooksDirectlyAtPlayer()
        {
            // Баг-репорт владельца продукта: на самом старте, пока камера ещё
            // не отстала на штатные TrailingRowsBehindPlayer рядов, взгляд
            // «с запасом вперёд» на полную глубину уводил прицел камеры так
            // далеко от игрока (который почти рядом с камерой), что белая
            // плитка выпадала из кадра. Запас должен масштабироваться до 0
            // ровно в момент старта — камера целится прямо в игрока, он
            // гарантированно виден, как и до появления запаса вперёд.
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 3f, -1f));

            var forward = follow.TargetRotation * Vector3.forward;

            // Камера и игрок на одном ряду (0), heightOffset=(0,3,-1) ->
            // direction = playerPos-cameraPos = (0,-3,1), нормализовано.
            var expected = new Vector3(0f, -3f, 1f).normalized;
            Assert.AreEqual(expected.x, forward.x, 1e-4f);
            Assert.AreEqual(expected.y, forward.y, 1e-4f);
            Assert.AreEqual(expected.z, forward.z, 1e-4f);
        }

        [Test]
        public void TargetRotation_PartiallyCaughtUp_NoHeightOffset_AlreadyLooksAheadDownTunnel()
        {
            // Даже до полного отставания камеры (штатные 5 рядов) запас
            // взгляда вперёд должен уже частично включаться, а не резко
            // появляться скачком — иначе на границе снова возможен провал кадра.
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            for (var row = 1; row <= 3; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2)); // трейлинг-ряд камеры всё ещё клампится к 0

            var forward = follow.TargetRotation * Vector3.forward;

            Assert.Greater(forward.z, 0f, "запас взгляда вперёд должен уже частично работать");
            Assert.AreEqual(0f, forward.y, 1e-5f, "без высоты камеры наклона по Y быть не должно");
        }

        [Test]
        public void TargetRotation_LooksAheadOfPlayer_ShallowerPitchThanLookingDirectlyAtPlayer()
        {
            // По запросу владельца продукта: белая плитка (игрок) должна быть
            // ближе к низу экрана, а не в центре. Если бы камера целилась
            // ровно в игрока (старое поведение), при heightOffset=(0,3,0) и
            // трейлинг-ряде 1 (игрок на ряду 6) угол был бы y/z = -3/5 = -0.6.
            // Камера уже полностью отстала на штатные 5 рядов (6-1=5) — запас
            // взгляда вперёд включён на полную, угол должен быть более пологим.
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 3f, 0f));

            for (var row = 1; row <= 6; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            var forward = follow.TargetRotation * Vector3.forward;
            var previousPitchRatio = Mathf.Abs(-3f / 5f);

            Assert.Less(Mathf.Abs(forward.y / forward.z), previousPitchRatio);
        }

        [Test]
        public void TargetRotation_SteadyStateWithProductionHeightOffset_PitchIsApproximately29Degrees()
        {
            // Хотфикс (18.4° -> 29°): с реальным Height Offset со сцены
            // (0,4,-1) исходное значение LookAheadRowsBeyondPlayer=6 давало
            // установившийся Rotation X ровно 18.435° = atan(4/12) — на
            // портретном мобильном экране небо занимало ~40% кадра. После
            // хотфикса — atan(4/7.2) ~= 29.05°, середина запрошенного
            // диапазона 25-33°. Проверяет Rotation X ровно так, как его
            // читают в инспекторе (Transform.eulerAngles.x).
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 4f, -1f));

            for (var row = 1; row <= 10; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            var pitch = follow.TargetRotation.eulerAngles.x;

            Assert.AreEqual(29.05f, pitch, 0.1f);
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
