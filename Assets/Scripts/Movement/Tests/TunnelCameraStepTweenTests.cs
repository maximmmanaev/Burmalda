using Burmalda.Core;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    /// <summary>
    /// Issue #153, тумблер 1: доезд камеры до цели за фиксированную
    /// длительность (<see cref="TunnelCameraFollow.AdvanceStepTween"/>)
    /// вместо экспоненциального сглаживания (<see cref="TunnelCameraFollow.Tick"/>,
    /// не тронут — это отдельный, параллельный путь, тумблер решает
    /// <see cref="TunnelCameraController"/> снаружи, какой из двух вызывать
    /// каждый кадр). В отличие от экспоненты, у доезда фиксированной
    /// длительности нет остаточного отставания — гарантированно приходит в
    /// цель ровно за <see cref="TunnelCameraFollow.StepTweenDurationSeconds"/>.
    /// </summary>
    public class TunnelCameraStepTweenTests
    {
        private const float TileSize = 1f;
        private const int Width = 5;

        private static (GridTraceTrail trail, WorldGridProjection projection) CreateTrail()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, Width / 2));
            var projection = new WorldGridProjection(TileSize, Width);
            return (trail, projection);
        }

        [Test]
        public void AdvanceStepTween_NoStepYet_IsNoOpAtInitialPosition()
        {
            // До первого реального хода — CurrentPosition уже равна
            // TargetPosition (как и в конструкторе Follow) — AdvanceStepTween
            // не должна телепортировать камеру в Vector3.zero или куда-то ещё.
            var (trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            var initial = follow.CurrentPosition;

            follow.AdvanceStepTween(0.05f);

            Assert.AreEqual(initial, follow.CurrentPosition);
        }

        [Test]
        public void AdvanceStepTween_FullDurationAfterStep_ReachesTargetExactly()
        {
            var (trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            follow.StepTweenDurationSeconds = 0.1f;

            trail.TryAdvanceTo(new GridCoordinate(1, Width / 2));
            follow.AdvanceStepTween(0.1f);

            Assert.AreEqual(follow.TargetPosition, follow.CurrentPosition);
        }

        [Test]
        public void AdvanceStepTween_HalfDuration_IsExactMidpoint()
        {
            var (trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            follow.StepTweenDurationSeconds = 0.1f;
            var start = follow.CurrentPosition;

            trail.TryAdvanceTo(new GridCoordinate(1, Width / 2));
            var target = follow.TargetPosition;
            follow.AdvanceStepTween(0.05f);

            var expectedMidpoint = Vector3.Lerp(start, target, 0.5f);
            Assert.AreEqual(expectedMidpoint.z, follow.CurrentPosition.z, 1e-5f);
        }

        [Test]
        public void AdvanceStepTween_MultiplePartialCalls_AccumulatesElapsedTime()
        {
            var (trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            follow.StepTweenDurationSeconds = 0.1f;

            trail.TryAdvanceTo(new GridCoordinate(1, Width / 2));
            follow.AdvanceStepTween(0.05f);
            follow.AdvanceStepTween(0.05f); // в сумме — вся длительность

            Assert.AreEqual(follow.TargetPosition, follow.CurrentPosition);
        }

        [Test]
        public void AdvanceStepTween_CalledPastDuration_DoesNotOvershoot()
        {
            var (trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            follow.StepTweenDurationSeconds = 0.1f;

            trail.TryAdvanceTo(new GridCoordinate(1, Width / 2));
            follow.AdvanceStepTween(0.1f); // доехали
            var afterArrival = follow.CurrentPosition;
            follow.AdvanceStepTween(10f); // сильно больше длительности — не должно ничего сломать

            Assert.AreEqual(afterArrival, follow.CurrentPosition);
            Assert.AreEqual(follow.TargetPosition, follow.CurrentPosition);
        }

        [Test]
        public void AdvanceStepTween_NewStepMidTween_RetargetsFromCurrentPositionNotFromOldStart()
        {
            // Непрерывный свайп быстрее длительности твина — второй ход
            // должен подхватить камеру ТАМ, где она сейчас реально
            // находится (mid-flight), а не телепортировать её назад к
            // стартовой точке первого твина.
            var (trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            follow.StepTweenDurationSeconds = 0.1f;

            trail.TryAdvanceTo(new GridCoordinate(1, Width / 2));
            follow.AdvanceStepTween(0.05f); // на полпути первого твина
            var midFlightPosition = follow.CurrentPosition;

            trail.TryAdvanceTo(new GridCoordinate(2, Width / 2)); // новый ход ДО завершения первого твина
            var newTarget = follow.TargetPosition;

            Assert.AreNotEqual(follow.CurrentPosition, newTarget, "новый ход не должен мгновенно телепортировать камеру в новую цель");
            Assert.AreEqual(midFlightPosition, follow.CurrentPosition, "позиция сразу после нового хода (до AdvanceStepTween) должна остаться там, где твин был прерван");

            follow.AdvanceStepTween(0.05f); // полпути ВТОРОГО твина, стартующего от midFlightPosition
            var expectedMidpoint = Vector3.Lerp(midFlightPosition, newTarget, 0.5f);
            Assert.AreEqual(expectedMidpoint.z, follow.CurrentPosition.z, 1e-5f);
        }

        [Test]
        public void AdvanceStepTween_ChangingDurationMidTween_UsesNewDurationForRemainingProgress()
        {
            // Тумблер настраивается вживую (debug-панель) — смена длительности
            // не должна крашить или давать NaN на уже идущем твине.
            var (trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            follow.StepTweenDurationSeconds = 0.2f;

            trail.TryAdvanceTo(new GridCoordinate(1, Width / 2));
            follow.AdvanceStepTween(0.05f);
            follow.StepTweenDurationSeconds = 0.05f; // укоротили длительность на лету

            follow.AdvanceStepTween(1f); // сильно больше новой длительности

            Assert.AreEqual(follow.TargetPosition, follow.CurrentPosition);
        }

        [Test]
        public void Invariant_B_AfterTweenFullyCompletes_FurtherCallsWithoutNewStepLeaveCameraAbsolutelyStill()
        {
            // Issue #153, инвариант B: как только твин последнего реального
            // хода завершился, дальнейшие вызовы AdvanceStepTween БЕЗ нового
            // хода не должны менять CurrentPosition НИ НА ОДИН тик — мир не
            // должен уползать под неподвижным пальцем.
            var (trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            follow.StepTweenDurationSeconds = 0.1f;

            trail.TryAdvanceTo(new GridCoordinate(1, Width / 2));
            follow.AdvanceStepTween(0.1f); // твин полностью доехал
            var settledPosition = follow.CurrentPosition;
            var settledTarget = follow.TargetPosition;
            var settledRotation = follow.CurrentRotation;

            for (var i = 0; i < 300; i++) follow.AdvanceStepTween(1f / 60f); // держим палец, шага нет — как ~5 секунд удержания

            Assert.AreEqual(settledPosition, follow.CurrentPosition, "камера не должна была сдвинуться ни на йоту без нового хода");
            Assert.AreEqual(settledTarget, follow.TargetPosition, "цель тоже не должна была измениться сама по себе");
            Assert.AreEqual(settledRotation, follow.CurrentRotation);
        }

        [Test]
        public void AdvanceStepTween_ZeroDuration_SnapsInstantlyWithoutNaN()
        {
            // Крайнее значение слайдера (0 мс) — не должно давать деление на
            // ноль/NaN, просто мгновенный снап, как SnapToTarget.
            var (trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            follow.StepTweenDurationSeconds = 0f;

            trail.TryAdvanceTo(new GridCoordinate(1, Width / 2));
            follow.AdvanceStepTween(0f);

            Assert.AreEqual(follow.TargetPosition, follow.CurrentPosition);
            Assert.IsFalse(float.IsNaN(follow.CurrentPosition.z));
        }
    }
}
