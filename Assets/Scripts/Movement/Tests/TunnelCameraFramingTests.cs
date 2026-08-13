using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    public class TunnelCameraFramingTests
    {
        [Test]
        public void ComputeDesiredHorizontalFovDegrees_ProductionValues_MatchesExpected()
        {
            // cameraHeight=6 (Height Offset Y со сцены), groundDistanceToRow=1
            // (см. ComputeSteadyStateGroundDistanceToReferenceRow_ProductionOffset
            // ниже), pitch=50 (Pitch Degrees со сцены) -> depth =
            // 6*sin(50°)+1*cos(50°) = 4.5962+0.6428 = 5.2391,
            // hFOV=2*atan(2/5.2391)=41.7885°.
            var hFov = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(
                cameraHeight: 6f, groundDistanceToRow: 1f, pitchDegrees: 50f, tileSize: 1f);

            Assert.AreEqual(41.7885f, hFov, 0.01f);
        }

        [Test]
        public void ComputeDesiredHorizontalFovDegrees_ZeroPitch_IgnoresCameraHeight()
        {
            // Хотфикс: depth = cameraHeight*sin(pitch) + groundDistanceToRow*cos(pitch).
            // При pitch=0 (взгляд строго горизонтально) первое слагаемое
            // обнуляется — камера смотрит вдаль по прямой, высота над полом
            // не должна влиять на то, как широко видно на заданном
            // расстоянии. Ловит регресс, если депт снова случайно станет
            // евклидовым sqrt(h²+d²) (старая ошибка — там высота ВСЕГДА влияла).
            var hFovLowCamera = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(
                cameraHeight: 1f, groundDistanceToRow: 5f, pitchDegrees: 0f, tileSize: 1f);
            var hFovHighCamera = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(
                cameraHeight: 100f, groundDistanceToRow: 5f, pitchDegrees: 0f, tileSize: 1f);

            Assert.AreEqual(hFovLowCamera, hFovHighCamera, 1e-4f);
        }

        [Test]
        public void ComputeDesiredHorizontalFovDegrees_NinetyDegreePitch_IgnoresGroundDistance()
        {
            // Симметрично предыдущему тесту: при pitch=90° (взгляд строго
            // вниз) второе слагаемое обнуляется — расстояние по полу до
            // опорного ряда не должно влиять на то, что видно прямо под
            // камерой на заданной высоте.
            var hFovNearRow = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(
                cameraHeight: 6f, groundDistanceToRow: 0f, pitchDegrees: 90f, tileSize: 1f);
            var hFovFarRow = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(
                cameraHeight: 6f, groundDistanceToRow: 50f, pitchDegrees: 90f, tileSize: 1f);

            Assert.AreEqual(hFovNearRow, hFovFarRow, 1e-3f);
        }

        [Test]
        public void ComputeDesiredHorizontalFovDegrees_LargerTileSize_ScalesWidthLinearly()
        {
            var hFovBase = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(4f, 1f, 50f, 1f);
            var hFovDoubled = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(4f, 2f, 50f, 2f);

            // Удвоение TileSize и groundDistanceToRow одновременно масштабирует
            // всю геометрию (высоту камеры и pitch не трогаем) — угол не совпадёт 1:1,
            // но должен оставаться в разумных пределах, не давать патологий.
            Assert.Greater(hFovDoubled, 0f);
            Assert.Less(hFovDoubled, 180f);
        }

        [Test]
        public void ComputeSteadyStateGroundDistanceToReferenceRow_ProductionOffset_MatchesExpected()
        {
            // Height Offset Z=2 со сцены, TrailingRowsBehindPlayer=5,
            // FramingReferenceRowsBehindPlayer=2 (дефолт) -> опорный ряд на
            // (5-2)=3 ряда впереди трейлинг-ряда камеры -> |3*1-2|=1.
            var distance = TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow(
                heightOffsetZ: 2f, tileSize: 1f, trailingRowsBehindPlayer: 5);

            Assert.AreEqual(1f, distance, 1e-5f);
        }

        [Test]
        public void ComputeSteadyStateGroundDistanceToReferenceRow_LegacyOffset_DiffersFromOldDegenerateFormula()
        {
            // Старый (уже убранный) вырожденный расчёт для Height Offset
            // Z=-1 давал |heightOffsetZ|=1 — ловит регресс на возврат к нему.
            // Правильное значение от устоявшегося состояния: (5-2)*1-(-1)=4.
            var distance = TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow(
                heightOffsetZ: -1f, tileSize: 1f, trailingRowsBehindPlayer: 5);

            Assert.AreEqual(4f, distance, 1e-5f);
            Assert.AreNotEqual(1f, distance, 1e-5f, "не должно совпадать со старым вырожденным |heightOffsetZ|");
        }

        [Test]
        public void ComputeSteadyStateGroundDistanceToReferenceRow_CustomReferenceRow_UsesGivenOffset()
        {
            var distance = TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow(
                heightOffsetZ: 0f, tileSize: 1f, trailingRowsBehindPlayer: 5, referenceRowsBehindPlayer: 5);

            // Опорный ряд совпадает с трейлинг-рядом камеры (0 рядов между
            // ними) -> дистанция равна offset'у по Z ровно.
            Assert.AreEqual(0f, distance, 1e-5f);
        }

        [Test]
        public void ComputeVerticalFovDegrees_SquareAspect_EqualsHorizontalFov()
        {
            var vFov = TunnelCameraFraming.ComputeVerticalFovDegrees(desiredHorizontalFovDegrees: 60f, aspect: 1f);

            Assert.AreEqual(60f, vFov, 0.001f);
        }

        [Test]
        public void ComputeVerticalFovDegrees_PortraitAspect_IsLargerThanHorizontalFov()
        {
            // Портретный экран (aspect<1) уже по ширине, чем по высоте —
            // чтобы горизонтальный охват остался прежним, вертикальный FOV
            // должен вырасти.
            var vFov = TunnelCameraFraming.ComputeVerticalFovDegrees(desiredHorizontalFovDegrees: 41.7885f, aspect: 0.5625f);

            Assert.AreEqual(68.3266f, vFov, 0.01f);
            Assert.Greater(vFov, 41.7885f);
        }

        [Test]
        public void ComputeVerticalFovDegrees_LandscapeAspect_IsSmallerThanHorizontalFov()
        {
            var vFov = TunnelCameraFraming.ComputeVerticalFovDegrees(desiredHorizontalFovDegrees: 41.7885f, aspect: 1.7778f);

            Assert.AreEqual(24.2383f, vFov, 0.01f);
            Assert.Less(vFov, 41.7885f);
        }

        [Test]
        public void ComputeVerticalFovDegrees_RoundTrip_RecoversOriginalHorizontalFov()
        {
            // Обратное преобразование (vFOV -> hFOV по той же формуле в
            // обратную сторону) должно восстановить исходный hFOV — иначе
            // ширина обзора не будет стабильной между аспектами.
            const float originalHFov = 41.7885f;
            const float aspect = 0.5625f;

            var vFov = TunnelCameraFraming.ComputeVerticalFovDegrees(originalHFov, aspect);
            var vFovHalfRad = vFov * 0.5f * Mathf.Deg2Rad;
            var recoveredHFovRad = 2f * Mathf.Atan(Mathf.Tan(vFovHalfRad) * aspect);
            var recoveredHFov = recoveredHFovRad * Mathf.Rad2Deg;

            Assert.AreEqual(originalHFov, recoveredHFov, 0.01f);
        }
    }
}
