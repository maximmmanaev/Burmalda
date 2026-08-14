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

            // introHeightOffsetZ явно =0f (совпадает с heightOffset.z=0) —
            // отключает Z-интерполяцию (см. IntroHeightOffsetZ) для этого
            // теста: он про сам факт «нет лага на старте», не про интро-Z.
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);

            // Плита (0,2): x=(2-2.5+0.5)*1=0, z=(0+0.5)*1=0.5; трейлинг-ряд max(0,0-5)=0 — офсета ещё нет.
            var expected = new Vector3(0f, 0f, 0.5f);
            Assert.AreEqual(expected, follow.TargetPosition);
            Assert.AreEqual(expected, follow.CurrentPosition);
        }

        [Test]
        public void OnTrailAdvanced_WithinFirstFiveRows_TargetStaysClampedToRowZero()
        {
            // introHeightOffsetZ:0f — этот тест про трейлинг-ряд (позицию),
            // не про твин интро (см. класс ConfirmRun/AdvanceIntroTween-тестов ниже).
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);

            for (var row = 1; row <= 5; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            // legacy/burmolda_demo.html: cameraTargetRow = Math.max(0, r-5) — для r<=5 это всегда 0.
            Assert.AreEqual(new Vector3(0f, 0f, 0.5f), follow.TargetPosition);
        }

        [Test]
        public void OnTrailAdvanced_BeyondFiveRows_TargetTrailsFiveRowsBehindPlayer()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);

            for (var row = 1; row <= 6; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            // Игрок на ряду 6 -> целевой ряд камеры max(0,6-5)=1: x=0, z=(1+0.5)=1.5.
            Assert.AreEqual(new Vector3(0f, 0f, 1.5f), follow.TargetPosition);
        }

        [Test]
        public void OnTrailAdvanced_LateralMove_TargetColumnStaysFixedAtTunnelCenter()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);

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
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);

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
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);

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
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);

            for (var row = 1; row <= 6; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            for (var i = 0; i < 1000; i++) follow.Tick();

            Assert.AreEqual(follow.TargetPosition.z, follow.CurrentPosition.z, 1e-4f);
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
        public void TargetRotation_IsIndependentOfHeightOffset()
        {
            var (_, trailA, projectionA) = CreateTrail();
            var followA = new TunnelCameraFollow(trailA, projectionA, Vector3.zero);
            var (_, trailB, projectionB) = CreateTrail();
            var followB = new TunnelCameraFollow(trailB, projectionB, new Vector3(0f, 10f, -5f));

            Assert.AreEqual(followA.TargetRotation, followB.TargetRotation);
        }

        [Test]
        public void TargetRotation_BeforeConfirmRun_EqualsIntroPitchDegreesRegardlessOfTrailAdvance()
        {
            // Row-based интерполяция убрана целиком (2026-08-14) — до
            // ConfirmRun камера статично стоит на интро-значениях, даже
            // если трейл успел продвинуться (не должно происходить по
            // геймплею до тапа-кнопки, но код не должен на это реагировать).
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            for (var row = 1; row <= 6; row++) trail.TryAdvanceTo(new GridCoordinate(row, 2));

            Assert.AreEqual(TunnelCameraFollow.DefaultIntroPitchDegrees, follow.TargetRotation.eulerAngles.x, 1e-4f);
        }

        [Test]
        public void IntroPitchDegrees_SetAtStart_UpdatesRotationImmediately()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero);

            follow.IntroPitchDegrees = 75f;

            Assert.AreEqual(75f, follow.TargetRotation.eulerAngles.x, 1e-4f);
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
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            for (var row = 1; row <= 6; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2)); // трейлинг-ряд 1 -> z=1.5, см. тест выше

            follow.HeightOffset = new Vector3(0f, 2f, 0f);

            Assert.AreEqual(new Vector3(0f, 2f, 1.5f), follow.TargetPosition);
        }

        [Test]
        public void TargetPosition_BeforeConfirmRun_ZUsesIntroHeightOffsetZNotHeightOffsetZ()
        {
            // Геометрический фикс (не про pitch): до ConfirmRun Z-компонента
            // офсета берётся из IntroHeightOffsetZ (дефолт -1), а не
            // напрямую из HeightOffset.Z (тут 2) — иначе устоявшийся Z=2
            // уводит камеру мимо стартовой плиты (см. doc-комментарий
            // класса/DefaultIntroHeightOffsetZ).
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 6f, 2f));

            // База z=0.5 (трейлинг-ряд клампится к 0) + IntroHeightOffsetZ.
            var expectedZ = 0.5f + TunnelCameraFollow.DefaultIntroHeightOffsetZ;
            Assert.AreEqual(new Vector3(0f, 6f, expectedZ), follow.TargetPosition, "X/Y — напрямую из HeightOffset, не интерполируются");
        }

        [Test]
        public void IntroHeightOffsetZ_SetAtStart_UpdatesPositionImmediately()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 6f, 2f));

            follow.IntroHeightOffsetZ = -3f;

            Assert.AreEqual(0.5f - 3f, follow.TargetPosition.z, 1e-4f);
        }

        // === ConfirmRun / AdvanceIntroTween — time-based твин интро (2026-08-14) ===

        [Test]
        public void ConfirmRun_WithoutAdvanceIntroTween_StaysAtIntroValues()
        {
            // ConfirmRun только "взводит" твин — сама анимация идёт через AdvanceIntroTween.
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 6f, 2f));

            follow.ConfirmRun();

            Assert.AreEqual(TunnelCameraFollow.DefaultIntroPitchDegrees, follow.TargetRotation.eulerAngles.x, 1e-4f);
            Assert.AreEqual(0.5f + TunnelCameraFollow.DefaultIntroHeightOffsetZ, follow.TargetPosition.z, 1e-4f);
        }

        [Test]
        public void AdvanceIntroTween_WithoutConfirmRun_IsNoOp()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 6f, 2f));

            follow.AdvanceIntroTween(TunnelCameraFollow.TweenDurationSeconds); // весь твин разом, но ConfirmRun не вызван

            Assert.AreEqual(TunnelCameraFollow.DefaultIntroPitchDegrees, follow.TargetRotation.eulerAngles.x, 1e-4f);
        }

        [Test]
        public void ConfirmRun_ThenAdvanceIntroTweenFullDuration_PitchReachesSteadyStateExactly()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 6f, 2f));

            follow.ConfirmRun();
            follow.AdvanceIntroTween(TunnelCameraFollow.TweenDurationSeconds);

            Assert.AreEqual(TunnelCameraFollow.DefaultPitchDegrees, follow.TargetRotation.eulerAngles.x, 1e-4f);
        }

        [Test]
        public void ConfirmRun_ThenAdvanceIntroTweenFullDuration_ZReachesHeightOffsetZExactly()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 6f, 2f));

            follow.ConfirmRun();
            follow.AdvanceIntroTween(TunnelCameraFollow.TweenDurationSeconds);

            // База z=0.5 (трейлинг-ряд клампится к 0 — игрок ещё на старте) + устоявшийся HeightOffset.Z=2.
            Assert.AreEqual(2.5f, follow.TargetPosition.z, 1e-4f);
        }

        [Test]
        public void ConfirmRun_ThenAdvanceIntroTweenFullDuration_CurrentPositionMatchesTargetInstantly()
        {
            // Во время твина CurrentPosition держится РОВНО TargetPosition —
            // без экспоненциального сглаживания Tick() поверх твина.
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 6f, 2f));

            follow.ConfirmRun();
            follow.AdvanceIntroTween(TunnelCameraFollow.TweenDurationSeconds);

            Assert.AreEqual(follow.TargetPosition, follow.CurrentPosition);
        }

        [Test]
        public void AdvanceIntroTween_HalfDuration_UsesEaseOutCubicNotLinear()
        {
            // EaseOutCubic(0.5) = 1-(1-0.5)^3 = 1-0.125 = 0.875 — заметно
            // дальше от старта, чем линейные 0.5 (резкий старт, плавное
            // торможение к цели, как и просили).
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, pitchDegrees: 20f, introPitchDegrees: 80f);
            follow.ConfirmRun();

            follow.AdvanceIntroTween(TunnelCameraFollow.TweenDurationSeconds * 0.5f);

            var expectedPitch = Mathf.Lerp(80f, 20f, 0.875f);
            Assert.AreEqual(expectedPitch, follow.TargetRotation.eulerAngles.x, 1e-2f);

            var linearPitch = Mathf.Lerp(80f, 20f, 0.5f);
            Assert.Greater(Mathf.Abs(follow.TargetRotation.eulerAngles.x - linearPitch), 1e-2f, "не должно быть линейной интерполяцией");
        }

        [Test]
        public void AdvanceIntroTween_MultiplePartialCalls_AccumulatesElapsedTime()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 6f, 2f));
            follow.ConfirmRun();

            follow.AdvanceIntroTween(TunnelCameraFollow.TweenDurationSeconds * 0.5f);
            follow.AdvanceIntroTween(TunnelCameraFollow.TweenDurationSeconds * 0.5f); // в сумме — весь твин

            Assert.AreEqual(TunnelCameraFollow.DefaultPitchDegrees, follow.TargetRotation.eulerAngles.x, 1e-3f);
        }

        [Test]
        public void AdvanceIntroTween_CalledAfterAlreadyComplete_DoesNotOvershootOrChangeAnything()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 6f, 2f));
            follow.ConfirmRun();
            follow.AdvanceIntroTween(TunnelCameraFollow.TweenDurationSeconds); // твин уже завершён

            follow.AdvanceIntroTween(100f); // сильно больше длительности твина — не должно ничего сломать

            Assert.AreEqual(TunnelCameraFollow.DefaultPitchDegrees, follow.TargetRotation.eulerAngles.x, 1e-4f);
            Assert.AreEqual(2.5f, follow.TargetPosition.z, 1e-4f);
        }

        [Test]
        public void ConfirmRun_CalledTwice_DoesNotRestartTween()
        {
            // Защита от двойного триггера (напр. повторный тап) — второй
            // вызов не должен сбросить уже накопленный прогресс твина.
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, new Vector3(0f, 6f, 2f));
            follow.ConfirmRun();
            follow.AdvanceIntroTween(TunnelCameraFollow.TweenDurationSeconds * 0.5f);
            var pitchMidTween = follow.TargetRotation.eulerAngles.x;

            follow.ConfirmRun(); // повторный вызов — не должен сбросить elapsed на 0

            Assert.AreEqual(pitchMidTween, follow.TargetRotation.eulerAngles.x, 1e-4f);
        }

        [Test]
        public void AdvanceIntroTween_PlayerAdvancesMidTween_TweenContinuesUsingCurrentProgressNotRestarted()
        {
            // "Если игрок продвинется до завершения твина — не должно быть
            // возможно по геймплею, но защитись: дай твину доиграть, не
            // прерывай" — обычный ход трейла НЕ трогает elapsed твина.
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            follow.ConfirmRun();
            follow.AdvanceIntroTween(TunnelCameraFollow.TweenDurationSeconds * 0.5f);
            var pitchMidTween = follow.TargetRotation.eulerAngles.x;

            trail.TryAdvanceTo(new GridCoordinate(1, 2)); // обычный ход трейла посреди твина

            Assert.AreEqual(pitchMidTween, follow.TargetRotation.eulerAngles.x, 1e-4f, "ход трейла не должен сбрасывать/ускорять твин");

            follow.AdvanceIntroTween(TunnelCameraFollow.TweenDurationSeconds * 0.5f); // твин продолжает доигрывать как ни в чём не бывало
            Assert.AreEqual(TunnelCameraFollow.DefaultPitchDegrees, follow.TargetRotation.eulerAngles.x, 1e-4f);
        }

        [Test]
        public void SnapToTarget_AfterFallingBehind_InstantlyMatchesTarget()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            for (var row = 1; row <= 20; row++) trail.TryAdvanceTo(new GridCoordinate(row, 2)); // Current сильно отстаёт от Target без Tick()

            follow.SnapToTarget();

            Assert.AreEqual(follow.TargetPosition, follow.CurrentPosition);
        }

        [Test]
        public void SnapToTarget_AfterPriorNudgeForward_ResetsManualOffset()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            for (var row = 1; row <= 6; row++) trail.TryAdvanceTo(new GridCoordinate(row, 2));
            var targetBeforeNudge = follow.TargetPosition;
            follow.NudgeForward(10f); // накопленное ручное смещение

            follow.SnapToTarget();

            // Смещение сброшено — цель вернулась к чисто рядовой (без ручного скролла).
            Assert.AreEqual(targetBeforeNudge, follow.TargetPosition);
            Assert.AreEqual(targetBeforeNudge, follow.CurrentPosition);
        }

        [Test]
        public void NudgeForward_MovesTargetAndCurrentPositionImmediatelyByGivenDistance()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            var targetBefore = follow.TargetPosition;
            var currentBefore = follow.CurrentPosition;

            follow.NudgeForward(3f);

            Assert.AreEqual(targetBefore.z + 3f, follow.TargetPosition.z, 1e-5f);
            Assert.AreEqual(currentBefore.z + 3f, follow.CurrentPosition.z, 1e-5f, "смещение мгновенное, не через сглаживание Tick()");
        }

        [Test]
        public void NudgeForward_CalledRepeatedly_Accumulates()
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            var targetBefore = follow.TargetPosition;

            follow.NudgeForward(2f);
            follow.NudgeForward(1.5f);

            Assert.AreEqual(targetBefore.z + 3.5f, follow.TargetPosition.z, 1e-5f);
        }

        [Test]
        public void NudgeForward_SurvivesNormalTrailAdvance_NotResetByOrdinaryMovement()
        {
            // Эдж-скролл не должен стираться обычным ходом трейла — только SnapToTarget (новый тап).
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            follow.NudgeForward(4f);
            var targetAfterNudge = follow.TargetPosition;

            trail.TryAdvanceTo(new GridCoordinate(1, 2)); // обычный ход, не тап заново

            Assert.AreEqual(targetAfterNudge.z + 1f, follow.TargetPosition.z, 1e-5f); // ряд сдвинулся на 1, ручное смещение осталось
        }

        [Test]
        public void NudgeForward_RequestExceedsMaxManualForwardOffset_ClampsToMaxAndStops()
        {
            // Найдено на реальном устройстве (2026-08-14): без капа камера
            // укатывалась за пределы сгенерированного тоннеля (чёрный экран).
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            var targetBefore = follow.TargetPosition;
            var currentBefore = follow.CurrentPosition;

            follow.NudgeForward(TunnelCameraFollow.MaxManualForwardOffset + 100f); // сильно больше капа за один вызов
            follow.NudgeForward(50f); // повторный запрос — уже без эффекта, кап исчерпан

            Assert.AreEqual(targetBefore.z + TunnelCameraFollow.MaxManualForwardOffset, follow.TargetPosition.z, 1e-4f);
            Assert.AreEqual(currentBefore.z + TunnelCameraFollow.MaxManualForwardOffset, follow.CurrentPosition.z, 1e-4f);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void NudgeForward_NonPositiveDistance_IsNoOp(float distance)
        {
            var (_, trail, projection) = CreateTrail();
            var follow = new TunnelCameraFollow(trail, projection, Vector3.zero, introHeightOffsetZ: 0f);
            var targetBefore = follow.TargetPosition;
            var currentBefore = follow.CurrentPosition;

            follow.NudgeForward(distance);

            Assert.AreEqual(targetBefore, follow.TargetPosition);
            Assert.AreEqual(currentBefore, follow.CurrentPosition);
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
