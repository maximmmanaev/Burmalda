using Burmalda.Core;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    /// <summary>
    /// Хотфикс камеры: угол наклона без неба в кадре (Pitch Degrees, дефолт
    /// 50°) + устойчивая ширина обзора тоннеля в
    /// <see cref="TunnelCameraFraming.DesiredVisibleTiles"/> плитки на любом
    /// аспекте экрана (см. <see cref="TunnelCameraFraming"/>) + плитка
    /// игрока заметно ниже центра кадра. Проверяет через реальный
    /// <see cref="Camera"/> и <see cref="Camera.WorldToViewportPoint"/> —
    /// world-позиции берутся из фактически заспавненной сетки
    /// (<see cref="WorldGridProjection"/>), не выдуманы. Batchmode-совместимо
    /// (EditMode, без сцены/Play Mode).
    /// </summary>
    public class TunnelCameraViewportFramingTests
    {
        private const float TileSize = 1f;
        private const int Width = 5;

        // Продукт таргетит только портретную ориентацию (PRD v6, одноручный
        // свайп) — Player Settings > Resolution and Presentation >
        // Orientation закреплён на Portrait, ландшафт физически недостижим
        // в игре. Ширина обзора (левый/правый край опорного ряда) всё же
        // проверяется на всех трёх аспектах ниже — она по построению
        // аспект-инвариантна (зависит только от tan(hFOV/2), не от aspect),
        // так что заодно ловит регресс этого свойства. А низкое положение
        // плитки игрока (вертикальный тест ниже) — только портрет: в
        // landscape/square с текущим Pitch Degrees=50°/Height Offset=(0,6,2)
        // плитка игрока уходит за нижний край экрана (проверено), но раз
        // эти аспекты недостижимы в билде — не тестируем их для этого условия.
        private static readonly (float aspect, string name)[] AllAspects =
        {
            (0.5625f, "portrait"),
            (1.7778f, "landscape"),
            (1f, "square"),
        };

        private static (TunnelGrid grid, GridTraceTrail trail, WorldGridProjection projection, Vector3 heightOffset) CreateSteadyStateSetup()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, Width / 2));
            var projection = new WorldGridProjection(TileSize, Width);
            var heightOffset = new Vector3(0f, 6f, 2f); // Height Offset со сцены — не трогается

            // Продвигаем трейл в установившийся режим (playerRow >= TrailingRowsBehindPlayer),
            // иначе top-down интро (issue: плитки вне кадра на старте) ещё не
            // отпустило pitch к игровому значению.
            for (var row = 1; row <= 10; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, Width / 2));

            return (grid, trail, projection, heightOffset);
        }

        private static float ComputeDesiredHorizontalFovDeg(WorldGridProjection projection, Vector3 heightOffset, float pitchDeg)
        {
            // Та же чистая функция, что и TunnelCameraController — без
            // вырожденного старта забега (см. историю багов калибровки).
            var groundDistanceToRow = TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow(
                heightOffset.z, projection.TileSize, TunnelCameraFollow.TrailingRowsBehindPlayer);
            return TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(
                heightOffset.y, groundDistanceToRow, pitchDeg, projection.TileSize);
        }

        [Test]
        public void SteadyStateReferenceRow_DesiredVisibleWidth_LandsNearFrameEdgesOnAnyAspect()
        {
            var (grid, trail, projection, heightOffset) = CreateSteadyStateSetup();
            var follow = new TunnelCameraFollow(trail, projection, heightOffset);

            // Устоявшийся режим -> top-down интро уже полностью отпустило
            // pitch к игровому значению (PitchDegrees).
            var pitch = follow.TargetRotation.eulerAngles.x;
            Assert.GreaterOrEqual(pitch, 30f, "условие отсутствия неба в кадре (vFOV=60° -> vFOV/2=30°)");

            var desiredHorizontalFovDeg = ComputeDesiredHorizontalFovDeg(projection, heightOffset, pitch);

            // Опорный ряд — FramingReferenceRowsBehindPlayer плиток позади
            // текущей позиции игрока (реально заспавнен трейлом).
            //
            // Важно: край проверяем по TunnelCameraFraming.DesiredVisibleTiles
            // (4 плитки), НЕ по полной ширине сетки (Width=5 колонок, край в
            // край). Первая версия этого теста ошибочно брала крайние
            // колонки всей сетки (0 и Width-1) — при DesiredVisibleTiles=4 <
            // Width=5 это два разных расстояния от центра тоннеля (2.0 против
            // 2.5), а FOV намеренно калиброван только под 4 плитки (тач-таргет
            // на мобиле, см. DesiredVisibleTiles) — тест валился на ~-12.5%/
            // ~112.5% вместо ожидаемых ~0%/100%. Тоннель зафиксирован по X в
            // центре (см. #62/TunnelCameraFollow.ComputeTargetPosition), так
            // что центр опорного ряда — это центр колонки Width/2.
            var referenceRow = trail.CurrentPosition.Row - TunnelCameraFraming.FramingReferenceRowsBehindPlayer;
            var centerX = projection.ToWorldPosition(new GridCoordinate(referenceRow, Width / 2)).x;
            var halfVisibleWidth = TunnelCameraFraming.DesiredVisibleTiles * TileSize / 2f;
            var leftEdgeX = centerX - halfVisibleWidth;
            var rightEdgeX = centerX + halfVisibleWidth;
            var rowWorldZ = projection.ToWorldPosition(new GridCoordinate(referenceRow, 0)).z;

            GameObject cameraObject = null;
            try
            {
                cameraObject = new GameObject("TestCamera_TunnelCameraViewportFraming");
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(follow.CurrentPosition, follow.CurrentRotation);

                foreach (var (aspect, name) in AllAspects)
                {
                    camera.aspect = aspect;
                    camera.fieldOfView = TunnelCameraFraming.ComputeVerticalFovDegrees(desiredHorizontalFovDeg, aspect);

                    var leftViewport = camera.WorldToViewportPoint(new Vector3(leftEdgeX, 0f, rowWorldZ));
                    var rightViewport = camera.WorldToViewportPoint(new Vector3(rightEdgeX, 0f, rowWorldZ));

                    // Камера смотрит без yaw (Quaternion.Euler(pitch,0,0)) —
                    // у forward нет X-компоненты, поэтому депт вдоль оси
                    // взгляда одинаков для любого X на одном ряду (проверено
                    // напрямую: depth(x=-2.5)==depth(x=0)==depth(x=2.5) для
                    // pitch=50°). Значит расхождения "депт до центра vs депт
                    // до края" НЕ существует — эта симметричная точка ровно
                    // на границе DesiredVisibleTiles обязана лечь ровно на
                    // край кадра (0.0/1.0) с точностью до накопленной ошибки
                    // прямого/обратного перевода hFOV<->vFOV через atan/tan,
                    // отсюда узкий допуск (не ±12.5%, как в прежней ошибочной
                    // версии теста).
                    Assert.AreEqual(0f, leftViewport.x, 0.01f, $"левый край видимой ширины, aspect={name}");
                    Assert.AreEqual(1f, rightViewport.x, 0.01f, $"правый край видимой ширины, aspect={name}");
                }
            }
            finally
            {
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                follow.Dispose();
            }
        }

        [Test]
        public void SteadyState_PlayerTile_LandsNoticeablyBelowCenterInPortrait()
        {
            // Issue: плитка игрока должна быть ближе к низу экрана, а не в
            // центре — раньше это было утверждение "на глазок" в комментариях
            // тюнинга, без проверки реальным WorldToViewportPoint. Только
            // портрет — единственная поддерживаемая ориентация (см. класс).
            var (grid, trail, projection, heightOffset) = CreateSteadyStateSetup();
            var follow = new TunnelCameraFollow(trail, projection, heightOffset);
            var pitch = follow.TargetRotation.eulerAngles.x;
            var desiredHorizontalFovDeg = ComputeDesiredHorizontalFovDeg(projection, heightOffset, pitch);

            var playerWorldPosition = projection.ToWorldPosition(trail.CurrentPosition);

            GameObject cameraObject = null;
            try
            {
                cameraObject = new GameObject("TestCamera_PlayerTileLowInFrame");
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(follow.CurrentPosition, follow.CurrentRotation);
                camera.aspect = 0.5625f; // портрет 9:16 — целевая (единственная поддерживаемая) ориентация
                camera.fieldOfView = TunnelCameraFraming.ComputeVerticalFovDegrees(desiredHorizontalFovDeg, camera.aspect);

                var playerViewport = camera.WorldToViewportPoint(playerWorldPosition);

                Assert.GreaterOrEqual(playerViewport.y, 0f, "плитка игрока не должна быть за нижним краем экрана");
                Assert.LessOrEqual(playerViewport.y, 0.35f, "плитка игрока должна быть заметно ниже центра (0.5)");
            }
            finally
            {
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                follow.Dispose();
            }
        }
    }
}
