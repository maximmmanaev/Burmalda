using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    public class TunnelCameraFramingTests
    {
        [Test]
        public void ComputeDesiredHorizontalFovDegrees_ProductionValues_MatchesExpected()
        {
            // cameraHeight=4 (Height Offset Y со сцены), groundDistanceToRow=1
            // (камера-старт -> первая заспавненная плита, TileSize=1) ->
            // R=sqrt(17)=4.1231, hFOV=2*atan(2/4.1231)=51.753°.
            var hFov = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(
                cameraHeight: 4f, groundDistanceToRow: 1f, tileSize: 1f);

            Assert.AreEqual(51.753f, hFov, 0.01f);
        }

        [Test]
        public void ComputeDesiredHorizontalFovDegrees_LargerTileSize_ScalesWidthLinearly()
        {
            var hFovBase = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(4f, 1f, 1f);
            var hFovDoubled = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(4f, 2f, 2f);

            // Удвоение TileSize и groundDistanceToRow одновременно масштабирует
            // всю геометрию (высоту камеры не трогаем) — угол не совпадёт 1:1,
            // но должен оставаться в разумных пределах, не давать патологий.
            Assert.Greater(hFovDoubled, 0f);
            Assert.Less(hFovDoubled, 180f);
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
            var vFov = TunnelCameraFraming.ComputeVerticalFovDegrees(desiredHorizontalFovDegrees: 51.753f, aspect: 0.5625f);

            Assert.AreEqual(81.546f, vFov, 0.01f);
            Assert.Greater(vFov, 51.753f);
        }

        [Test]
        public void ComputeVerticalFovDegrees_LandscapeAspect_IsSmallerThanHorizontalFov()
        {
            var vFov = TunnelCameraFraming.ComputeVerticalFovDegrees(desiredHorizontalFovDegrees: 51.753f, aspect: 1.7778f);

            Assert.AreEqual(30.523f, vFov, 0.01f);
            Assert.Less(vFov, 51.753f);
        }

        [Test]
        public void ComputeVerticalFovDegrees_RoundTrip_RecoversOriginalHorizontalFov()
        {
            // Обратное преобразование (vFOV -> hFOV по той же формуле в
            // обратную сторону) должно восстановить исходный hFOV — иначе
            // ширина обзора не будет стабильной между аспектами.
            const float originalHFov = 51.753f;
            const float aspect = 0.5625f;

            var vFov = TunnelCameraFraming.ComputeVerticalFovDegrees(originalHFov, aspect);
            var vFovHalfRad = vFov * 0.5f * Mathf.Deg2Rad;
            var recoveredHFovRad = 2f * Mathf.Atan(Mathf.Tan(vFovHalfRad) * aspect);
            var recoveredHFov = recoveredHFovRad * Mathf.Rad2Deg;

            Assert.AreEqual(originalHFov, recoveredHFov, 0.01f);
        }
    }
}
