using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    /// <summary>
    /// Issue #158 (продолжение #155): мягкая граница жёсткого клампа
    /// (issue A.3) — <see cref="TunnelCameraSoftBoundary.ComputeCorrection"/>
    /// чистая математика, тестируется напрямую, без Camera/сцены — тот же
    /// принцип, что и <see cref="TunnelCameraAnchor"/>.
    /// </summary>
    public class TunnelCameraSoftBoundaryTests
    {
        private const float MinDistance = 2f;
        private const float MaxDistance = 3f; // bandWidth = 1
        private const float DeltaSeconds = 1f / 60f;

        [Test]
        public void BelowStartFraction_NoCorrection()
        {
            // t = (distance-min)/bandWidth = 0.5 < 0.6 по умолчанию — зона
            // покоя, инвариант B (камера не двигается без хода) требует
            // РОВНО ноль здесь, не "почти ноль".
            var distance = MinDistance + 0.5f * (MaxDistance - MinDistance);
            var correction = TunnelCameraSoftBoundary.ComputeCorrection(distance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);

            Assert.AreEqual(0f, correction, "ниже точки начала нарастания коррекция обязана быть точно нулём (инвариант B в состоянии покоя)");
        }

        [Test]
        public void AtRest_ExactlyAtAnchor_NoCorrection()
        {
            // distance == minDistance — устоявшееся состояние (см.
            // TunnelCameraContinuousFollowTests, инвариант B). t=0, глубоко
            // внутри зоны покоя.
            var correction = TunnelCameraSoftBoundary.ComputeCorrection(MinDistance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            Assert.AreEqual(0f, correction);
        }

        [Test]
        public void ExactlyAtStartFraction_NoCorrection()
        {
            var distance = MinDistance + TunnelCameraSoftBoundary.DefaultStartFraction * (MaxDistance - MinDistance);
            var correction = TunnelCameraSoftBoundary.ComputeCorrection(distance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            Assert.AreEqual(0f, correction, "ровно на точке начала нарастания сила ещё нулевая — ниже граница, дальше начинает расти");
        }

        [Test]
        public void AboveStartFraction_PositiveAndMonotonicallyIncreasing()
        {
            var previous = 0f;
            for (var t = 0.61f; t <= 1f; t += 0.05f)
            {
                var distance = MinDistance + t * (MaxDistance - MinDistance);
                var correction = TunnelCameraSoftBoundary.ComputeCorrection(distance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);

                Assert.Greater(correction, 0f, $"t={t}: коррекция должна быть положительной (тянет назад к anchor)");
                Assert.Greater(correction, previous, $"t={t}: коррекция должна нарастать монотонно к краю");
                previous = correction;
            }
        }

        [Test]
        public void AtTheEdge_ReachesMaximumStrength()
        {
            var justBeforeEdge = TunnelCameraSoftBoundary.ComputeCorrection(
                MinDistance + 0.99f * (MaxDistance - MinDistance), MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            var atEdge = TunnelCameraSoftBoundary.ComputeCorrection(MaxDistance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);

            Assert.Greater(atEdge, justBeforeEdge, "у самого края сила коррекции обязана быть максимальной за весь диапазон");
        }

        [Test]
        public void BelowAnchor_NegativeResidual_NoCorrection()
        {
            // distance < min (шаг назад мог утащить дистанцию ниже anchor) —
            // мягкая граница этой стороны НЕ касается (issue #158 говорит
            // только про верхний край anchor+tolerance), хард-кламп там же,
            // что и раньше.
            var correction = TunnelCameraSoftBoundary.ComputeCorrection(MinDistance - 0.5f, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            Assert.AreEqual(0f, correction);
        }

        [Test]
        public void DegenerateBand_MaxNotGreaterThanMin_ReturnsZeroNotNaN()
        {
            var correction = TunnelCameraSoftBoundary.ComputeCorrection(MinDistance, MinDistance, MinDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            Assert.AreEqual(0f, correction);
        }

        [Test]
        public void StartFractionAtOne_NeverDividesByZero()
        {
            // Дебаг-панель может докрутить точку начала нарастания вплотную
            // к 1.0 — деление на (1-startFraction) не должно давать NaN/Inf.
            var correction = TunnelCameraSoftBoundary.ComputeCorrection(MaxDistance, MinDistance, MaxDistance, DeltaSeconds, 1f);
            Assert.IsFalse(float.IsNaN(correction) || float.IsInfinity(correction));
        }

        [Test]
        public void BeyondEdge_StillFiniteAndPositive()
        {
            // Резкий скачок скорости мог на один кадр протолкнуть distance
            // ЗА maxDistance ещё до хард-клампа этого же кадра (порядок
            // применения в AdvanceContinuousAnchorFollow) — не должно быть
            // NaN/отрицательного значения.
            var correction = TunnelCameraSoftBoundary.ComputeCorrection(MaxDistance + 1f, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            Assert.Greater(correction, 0f);
            Assert.IsFalse(float.IsNaN(correction) || float.IsInfinity(correction));
        }
    }
}
