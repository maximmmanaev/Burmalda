using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    /// <summary>
    /// Issue #158/задача 1: мягкая граница жёсткого клампа (issue A.3),
    /// теперь двусторонняя вокруг отдельной точки покоя (target) —
    /// <see cref="TunnelCameraSoftBoundary.ComputeCorrection"/> чистая
    /// математика, тестируется напрямую, без Camera/сцены — тот же принцип,
    /// что и <see cref="TunnelCameraAnchor"/>.
    /// </summary>
    public class TunnelCameraSoftBoundaryTests
    {
        private const float MinDistance = 1f; // anchor - backTolerance
        private const float TargetDistance = 2f; // anchor — точка покоя, не на краю
        private const float MaxDistance = 3f; // anchor + tolerance
        private const float DeltaSeconds = 1f / 60f;

        [Test]
        public void AtTarget_NoCorrection()
        {
            // distance == target — инвариант B, состояние покоя, коррекция
            // обязана быть РОВНО нулём, не "почти ноль".
            var correction = TunnelCameraSoftBoundary.ComputeCorrection(TargetDistance, TargetDistance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            Assert.AreEqual(0f, correction);
        }

        [TestCase(0.5f, TestName = "UpperSide_BelowStartFraction")]
        public void UpperSide_BelowStartFraction_NoCorrection(float t)
        {
            // t = (distance-target)/(max-target) = 0.5 < 0.6 по умолчанию.
            var distance = TargetDistance + t * (MaxDistance - TargetDistance);
            var correction = TunnelCameraSoftBoundary.ComputeCorrection(distance, TargetDistance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            Assert.AreEqual(0f, correction, "ниже точки начала нарастания коррекция обязана быть точно нулём (инвариант B в состоянии покоя)");
        }

        [Test]
        public void UpperSide_ExactlyAtStartFraction_NoCorrection()
        {
            var distance = TargetDistance + TunnelCameraSoftBoundary.DefaultStartFraction * (MaxDistance - TargetDistance);
            var correction = TunnelCameraSoftBoundary.ComputeCorrection(distance, TargetDistance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            Assert.AreEqual(0f, correction, "ровно на точке начала нарастания сила ещё нулевая — ниже граница, дальше начинает расти");
        }

        [Test]
        public void UpperSide_AboveStartFraction_PositiveAndMonotonicallyIncreasing()
        {
            var previous = 0f;
            for (var t = 0.61f; t <= 1f; t += 0.05f)
            {
                var distance = TargetDistance + t * (MaxDistance - TargetDistance);
                var correction = TunnelCameraSoftBoundary.ComputeCorrection(distance, TargetDistance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);

                Assert.Greater(correction, 0f, $"t={t}: коррекция должна быть положительной (тянет вниз к target)");
                Assert.Greater(correction, previous, $"t={t}: коррекция должна нарастать монотонно к краю");
                previous = correction;
            }
        }

        [Test]
        public void UpperSide_AtTheEdge_ReachesMaximumStrength()
        {
            var justBeforeEdge = TunnelCameraSoftBoundary.ComputeCorrection(
                TargetDistance + 0.99f * (MaxDistance - TargetDistance), TargetDistance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            var atEdge = TunnelCameraSoftBoundary.ComputeCorrection(MaxDistance, TargetDistance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);

            Assert.Greater(atEdge, justBeforeEdge, "у самого верхнего края сила коррекции обязана быть максимальной за весь диапазон");
        }

        [Test]
        public void LowerSide_BelowStartFraction_NoCorrection()
        {
            // t = (target-distance)/(target-min) = 0.5 < 0.6 по умолчанию.
            var distance = TargetDistance - 0.5f * (TargetDistance - MinDistance);
            var correction = TunnelCameraSoftBoundary.ComputeCorrection(distance, TargetDistance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            Assert.AreEqual(0f, correction, "ниже точки начала нарастания коррекция обязана быть точно нулём и на нижней стороне");
        }

        [Test]
        public void LowerSide_ExactlyAtStartFraction_NoCorrection()
        {
            var distance = TargetDistance - TunnelCameraSoftBoundary.DefaultStartFraction * (TargetDistance - MinDistance);
            var correction = TunnelCameraSoftBoundary.ComputeCorrection(distance, TargetDistance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            Assert.AreEqual(0f, correction);
        }

        [Test]
        public void LowerSide_AboveStartFraction_NegativeAndMonotonicallyDecreasing()
        {
            // Отрицательная — прибавка к _continuousCameraZ должна быть
            // отрицательной, чтобы дистанция РОСЛА обратно к target (см.
            // doc-комментарий ComputeCorrection).
            var previous = 0f;
            for (var t = 0.61f; t <= 1f; t += 0.05f)
            {
                var distance = TargetDistance - t * (TargetDistance - MinDistance);
                var correction = TunnelCameraSoftBoundary.ComputeCorrection(distance, TargetDistance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);

                Assert.Less(correction, 0f, $"t={t}: коррекция должна быть отрицательной (тянет вверх к target)");
                Assert.Less(correction, previous, $"t={t}: |коррекция| должна нарастать монотонно к краю");
                previous = correction;
            }
        }

        [Test]
        public void LowerSide_AtTheEdge_ReachesMaximumStrength()
        {
            var justBeforeEdge = TunnelCameraSoftBoundary.ComputeCorrection(
                TargetDistance - 0.99f * (TargetDistance - MinDistance), TargetDistance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            var atEdge = TunnelCameraSoftBoundary.ComputeCorrection(MinDistance, TargetDistance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);

            Assert.Less(atEdge, justBeforeEdge, "у самого нижнего края |сила коррекции| обязана быть максимальной за весь диапазон");
        }

        [Test]
        public void DegenerateUpperSide_MaxNotGreaterThanTarget_ReturnsZeroNotNaN()
        {
            var correction = TunnelCameraSoftBoundary.ComputeCorrection(TargetDistance + 1f, TargetDistance, MinDistance, TargetDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            Assert.AreEqual(0f, correction);
        }

        [Test]
        public void DegenerateLowerSide_MinNotLessThanTarget_ReturnsZeroNotNaN()
        {
            var correction = TunnelCameraSoftBoundary.ComputeCorrection(TargetDistance - 1f, TargetDistance, TargetDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            Assert.AreEqual(0f, correction);
        }

        [Test]
        public void StartFractionAtOne_NeverDividesByZero()
        {
            // Дебаг-панель может докрутить точку начала нарастания вплотную
            // к 1.0 — деление на (1-startFraction) не должно давать NaN/Inf,
            // ни на верхней, ни на нижней стороне.
            var upper = TunnelCameraSoftBoundary.ComputeCorrection(MaxDistance, TargetDistance, MinDistance, MaxDistance, DeltaSeconds, 1f);
            var lower = TunnelCameraSoftBoundary.ComputeCorrection(MinDistance, TargetDistance, MinDistance, MaxDistance, DeltaSeconds, 1f);
            Assert.IsFalse(float.IsNaN(upper) || float.IsInfinity(upper));
            Assert.IsFalse(float.IsNaN(lower) || float.IsInfinity(lower));
        }

        [Test]
        public void BeyondUpperEdge_StillFiniteAndPositive()
        {
            // Резкий скачок скорости мог на один кадр протолкнуть distance
            // ЗА maxDistance ещё до хард-клампа этого же кадра (порядок
            // применения в AdvanceContinuousAnchorFollow) — не должно быть
            // NaN/отрицательного значения.
            var correction = TunnelCameraSoftBoundary.ComputeCorrection(MaxDistance + 1f, TargetDistance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            Assert.Greater(correction, 0f);
            Assert.IsFalse(float.IsNaN(correction) || float.IsInfinity(correction));
        }

        [Test]
        public void BeyondLowerEdge_StillFiniteAndNegative()
        {
            var correction = TunnelCameraSoftBoundary.ComputeCorrection(MinDistance - 1f, TargetDistance, MinDistance, MaxDistance, DeltaSeconds, TunnelCameraSoftBoundary.DefaultStartFraction);
            Assert.Less(correction, 0f);
            Assert.IsFalse(float.IsNaN(correction) || float.IsInfinity(correction));
        }
    }
}
