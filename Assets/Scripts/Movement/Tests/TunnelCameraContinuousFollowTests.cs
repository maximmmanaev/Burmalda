using Burmalda.Core;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Movement.Tests
{
    /// <summary>
    /// Issue #155/задача 1: непрерывное покадровое следование с компенсацией
    /// скорости (<see cref="TunnelCameraFollow.AdvanceContinuousAnchorFollow"/>) —
    /// замена твина фиксированной длительности (#153, тумблер 1, запрещён —
    /// давал стоп-старт) и экспоненциального сглаживания (устаревший метод
    /// Tick(), удалён задачей 1). Проверяет реальными
    /// <see cref="Camera.WorldToViewportPoint"/>/покадровой симуляцией с
    /// настоящим deltaTime (1/60с) — тот же паттерн реальной Camera, что и
    /// <see cref="TunnelCameraViewportFramingTests"/>, но теперь с временной
    /// динамикой, не только статикой.
    /// </summary>
    public class TunnelCameraContinuousFollowTests
    {
        private const float TileSize = 1f;
        private const int Width = 5;
        private const float PortraitAspect = 0.5625f;

        // Задача 1 (владелец продукта подтвердил на устройстве): anchor
        // 32% -> 46%, полоса клампа стала двусторонней —
        // [anchor-BackToleranceViewportFraction, anchor+ToleranceViewportFraction].
        private const float AnchorViewportY = 0.46f;
        private const float ToleranceViewportFraction = 0.12f;
        private const float BackToleranceViewportFraction = 0.10f;
        private const float FrameDeltaSeconds = 1f / 60f;

        private static readonly Vector3 HeightOffset = new Vector3(0f, 6f, 0f);

        private static float ComputeSteadyVerticalFov(WorldGridProjection projection, float pitchDeg)
        {
            var groundDistanceToRow = TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow(
                HeightOffset.z, projection.TileSize, TunnelCameraFollow.TrailingRowsBehindPlayer);
            var desiredHorizontalFovDeg = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(
                HeightOffset.y, groundDistanceToRow, pitchDeg, projection.TileSize);
            return TunnelCameraFraming.ComputeVerticalFovDegrees(desiredHorizontalFovDeg, PortraitAspect);
        }

        private readonly struct Setup
        {
            public readonly GridTraceTrail Trail;
            public readonly WorldGridProjection Projection;
            public readonly TunnelCameraFollow Follow;
            public readonly float VFov;
            public readonly float TargetDistance; // дистанция на anchor — точка покоя (задача 1: строго ВНУТРИ полосы, не её граница)
            public readonly float MinDistance; // дистанция на anchor-backTolerance — нижняя граница клампа (задача 1)
            public readonly float MaxDistance; // дистанция на anchor+tolerance — верхняя граница клампа

            public Setup(GridTraceTrail trail, WorldGridProjection projection, TunnelCameraFollow follow, float vFov, float targetDistance, float minDistance, float maxDistance)
            {
                Trail = trail;
                Projection = projection;
                Follow = follow;
                VFov = vFov;
                TargetDistance = targetDistance;
                MinDistance = minDistance;
                MaxDistance = maxDistance;
            }
        }

        private static Setup CreateSetup()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, Width / 2));
            var projection = new WorldGridProjection(TileSize, Width);
            var follow = new TunnelCameraFollow(trail, projection, HeightOffset);
            follow.ConfirmRun();
            follow.AdvanceIntroTween(TunnelCameraFollow.TweenDurationSeconds); // интро полностью отыграно — устоявшийся режим

            var vFov = ComputeSteadyVerticalFov(projection, TunnelCameraFollow.DefaultPitchDegrees);
            var targetDistance = TunnelCameraAnchor.ComputeTrailingDistanceForAnchor(
                HeightOffset.y, TunnelCameraFollow.DefaultPitchDegrees, vFov, AnchorViewportY);
            var minDistance = TunnelCameraAnchor.ComputeTrailingDistanceForAnchor(
                HeightOffset.y, TunnelCameraFollow.DefaultPitchDegrees, vFov, AnchorViewportY - BackToleranceViewportFraction);
            var maxDistance = TunnelCameraAnchor.ComputeTrailingDistanceForAnchor(
                HeightOffset.y, TunnelCameraFollow.DefaultPitchDegrees, vFov, AnchorViewportY + ToleranceViewportFraction);

            return new Setup(trail, projection, follow, vFov, targetDistance, minDistance, maxDistance);
        }

        private static GameObject CreateCamera(string name, TunnelCameraFollow follow, float vFov)
        {
            var cameraObject = new GameObject(name);
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(follow.CurrentPosition, follow.CurrentRotation);
            camera.aspect = PortraitAspect;
            camera.fieldOfView = vFov;
            return cameraObject;
        }

        private static float ViewportYOfPlayerTile(Setup s, GameObject cameraObject)
        {
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.SetPositionAndRotation(s.Follow.CurrentPosition, s.Follow.CurrentRotation);
            var playerWorld = s.Projection.ToWorldPosition(s.Trail.CurrentPosition);
            return camera.WorldToViewportPoint(playerWorld).y;
        }

        /// <summary>Прогоняет устойчивое движение с постоянным темпом (ходов/с), тикая AdvanceContinuousAnchorFollow каждый симулированный кадр (1/60с).</summary>
        private static void RunSteadyMovement(Setup s, float movesPerSecond, float totalSeconds)
        {
            var stepIntervalSeconds = 1f / movesPerSecond;
            var secondsSinceLastStep = 0f;
            var elapsed = 0f;
            while (elapsed < totalSeconds)
            {
                secondsSinceLastStep += FrameDeltaSeconds;
                if (secondsSinceLastStep >= stepIntervalSeconds)
                {
                    s.Trail.TryAdvanceTo(new GridCoordinate(s.Trail.CurrentPosition.Row + 1, Width / 2));
                    secondsSinceLastStep -= stepIntervalSeconds;
                }
                s.Follow.AdvanceContinuousAnchorFollow(FrameDeltaSeconds, s.TargetDistance, s.MinDistance, s.MaxDistance);
                elapsed += FrameDeltaSeconds;
            }
        }

        /// <summary>
        /// Прогоняет устойчивое движение ровно на <paramref name="cycles"/>
        /// полных шаг-циклов (шаг → ровно framesPerCycle кадров), последний
        /// кадр — прямо ПЕРЕД тем, что было бы следующим ходом. Скачок
        /// дистанции D на ~1 плитку в момент хода — неизбежное следствие
        /// "без предсказания, непрерывная камера, дискретный игрок" (см. PR
        /// issue #155), а не ошибка слежения: при фиксированном wall-clock
        /// сэмплинге (<see cref="RunSteadyMovement"/>) тест ловит СЛУЧАЙНУЮ
        /// фазу этого скачка, разную для разных темпов из-за округления
        /// числа кадров — отсюда ложный разброс между темпами. Фиксация
        /// фазы (сравнение "перед следующим ходом" со "перед следующим
        /// ходом") даёт сопоставимый результат независимо от темпа.
        /// </summary>
        private static void RunSteadyMovementPhaseConsistent(Setup s, float movesPerSecond, int cycles)
        {
            var stepIntervalSeconds = 1f / movesPerSecond;
            var framesPerCycle = Mathf.RoundToInt(stepIntervalSeconds / FrameDeltaSeconds);
            for (var cycle = 0; cycle < cycles; cycle++)
            {
                s.Trail.TryAdvanceTo(new GridCoordinate(s.Trail.CurrentPosition.Row + 1, Width / 2));
                for (var frame = 0; frame < framesPerCycle; frame++)
                    s.Follow.AdvanceContinuousAnchorFollow(FrameDeltaSeconds, s.TargetDistance, s.MinDistance, s.MaxDistance);
            }
        }

        // Issue #155, инвариант A: доля высоты кадра, на которой находится
        // игрок в устоявшемся движении, отличается не более чем на ±3% между
        // темпами. 1/2/4.6 хода/с — от комфортного до максимально
        // достижимого пальцем (см. историю issue #153). Сэмплируем
        // фазово-согласованно (см. RunSteadyMovementPhaseConsistent) — иначе
        // тест ловит фазу неизбежного скачка D на шаге, а не саму точность
        // слежения.
        [TestCase(1f, TestName = "SlowPace")]
        [TestCase(2f, TestName = "MediumPace")]
        [TestCase(4.6f, TestName = "FastPace")]
        public void SteadyMovement_ThreePaces_PlayerAnchorWithinThreePercent(float movesPerSecond)
        {
            var s = CreateSetup();
            RunSteadyMovementPhaseConsistent(s, movesPerSecond, cycles: 15); // достаточно циклов для выхода на установившийся режим

            GameObject cameraObject = null;
            try
            {
                cameraObject = CreateCamera("TestCamera_Pace", s.Follow, s.VFov);
                var viewportY = ViewportYOfPlayerTile(s, cameraObject);

                Assert.AreEqual(AnchorViewportY, viewportY, 0.03f, $"movesPerSecond={movesPerSecond}: доля кадра под игроком должна быть в пределах ±3% от якоря на любом темпе");
            }
            finally
            {
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                s.Follow.Dispose();
            }
        }

        // Issue #155, инвариант B: указатель зажат, шага нет → камера не
        // двигается ни на йоту. Даём устояться (быстрый темп, чтобы
        // накопить и скорость, и любую остаточную ошибку клампа), потом
        // ходы прекращаются — после полного затухания скорости и схождения
        // коррекции дальнейшие вызовы БЕЗ нового хода не должны менять
        // CurrentPosition/TargetPosition/CurrentRotation вообще.
        [Test]
        public void Rest_NoNewStepAfterSettling_CameraStaysAbsolutelyStill()
        {
            var s = CreateSetup();
            RunSteadyMovement(s, movesPerSecond: 4f, totalSeconds: 3f);

            // Ждём дольше VelocityDecaySeconds без единого хода — скорость
            // затухает до нуля, коррекция сходится к идеальной дистанции.
            for (var i = 0; i < 120; i++) // 2с — заведомо больше 250мс затухания
                s.Follow.AdvanceContinuousAnchorFollow(FrameDeltaSeconds, s.TargetDistance, s.MinDistance, s.MaxDistance);

            var settledPosition = s.Follow.CurrentPosition;
            var settledTarget = s.Follow.TargetPosition;
            var settledRotation = s.Follow.CurrentRotation;

            for (var i = 0; i < 300; i++) // ~5с удержания без хода
                s.Follow.AdvanceContinuousAnchorFollow(FrameDeltaSeconds, s.TargetDistance, s.MinDistance, s.MaxDistance);

            Assert.AreEqual(settledPosition, s.Follow.CurrentPosition, "камера не должна была сдвинуться ни на йоту без нового хода");
            Assert.AreEqual(settledTarget, s.Follow.TargetPosition);
            Assert.AreEqual(settledRotation, s.Follow.CurrentRotation);

            s.Follow.Dispose();
        }

        // Issue #155, инвариант C — задача 1 переформулировала: полоса
        // клампа теперь ДВУСТОРОННЯЯ, [anchor-backTolerance, anchor+tolerance],
        // а не [anchor, anchor+tolerance] с жёстким запретом опускаться ниже
        // anchor. Старый запрет был защитой от ухода плитки из-под пальца
        // при протяжке — в схеме A (issue #157) палец между шагами
        // поднимается, защита не нужна (см. doc-комментарий
        // TunnelCameraFollow.AdvanceContinuousAnchorFollow). Тест проверяет
        // ОБА факта разом: (1) доля экрана всегда внутри новой двусторонней
        // полосы — на каждом кадре, включая резкий старт/стоп/шаг назад;
        // (2) старая граница на anchor реально снята — доля экрана хотя бы
        // раз в сценарии "шаг назад" опускается СТРОГО ниже anchor (иначе
        // тест 1 не отличил бы новое поведение от старого, если бы
        // backTolerance был случайно не подключён).
        [Test]
        public void EveryFrame_ThroughStartStopAndStepBack_ScreenFractionAlwaysWithinTwoSidedBandAndCanDropBelowAnchor()
        {
            var s = CreateSetup();
            GameObject cameraObject = null;
            try
            {
                cameraObject = CreateCamera("TestCamera_ClampBand", s.Follow, s.VFov);
                const float epsilon = 0.001f; // допуск на float-накопление за тысячи кадров
                var minViewportYSeen = float.MaxValue;

                void AssertBandOnThisFrame(string phase)
                {
                    var viewportY = ViewportYOfPlayerTile(s, cameraObject);
                    minViewportYSeen = Mathf.Min(minViewportYSeen, viewportY);
                    Assert.GreaterOrEqual(viewportY, AnchorViewportY - BackToleranceViewportFraction - epsilon, $"{phase}: плитка игрока ушла НИЖЕ anchor-backTolerance — кламп не сработал");
                    Assert.LessOrEqual(viewportY, AnchorViewportY + ToleranceViewportFraction + epsilon, $"{phase}: плитка игрока ушла ВЫШЕ anchor+tolerance — кламп не сработал");
                }

                // Резкий старт: сразу быстрый темп с нуля.
                for (var cycle = 0; cycle < 60; cycle++)
                {
                    s.Trail.TryAdvanceTo(new GridCoordinate(s.Trail.CurrentPosition.Row + 1, Width / 2));
                    for (var t = 0; t < 8; t++) // ~7.5 хода/с — быстрее, чем FastPace теста инварианта A, намеренно на грани/за гранью реалистичного
                    {
                        s.Follow.AdvanceContinuousAnchorFollow(FrameDeltaSeconds, s.TargetDistance, s.MinDistance, s.MaxDistance);
                        AssertBandOnThisFrame($"резкий старт, цикл {cycle}");
                    }
                }

                // Резкая остановка посреди быстрой серии — держим кадры без хода.
                for (var i = 0; i < 180; i++)
                {
                    s.Follow.AdvanceContinuousAnchorFollow(FrameDeltaSeconds, s.TargetDistance, s.MinDistance, s.MaxDistance);
                    AssertBandOnThisFrame("резкая остановка");
                }

                // Шаг назад на уже посещённую плитку — тот самый сценарий,
                // который раньше упирался в жёсткий пол на anchor. Несколько
                // шагов назад подряд, чтобы гарантированно дотолкать
                // дистанцию до нижней половины новой полосы, а не поймать
                // случайную недостаточную фазу.
                for (var back = 0; back < 3; back++)
                {
                    var current = s.Trail.CurrentPosition;
                    s.Trail.TryAdvanceTo(new GridCoordinate(current.Row - 1, Width / 2));
                    for (var t = 0; t < 10; t++)
                    {
                        s.Follow.AdvanceContinuousAnchorFollow(FrameDeltaSeconds, s.TargetDistance, s.MinDistance, s.MaxDistance);
                        AssertBandOnThisFrame($"шаг назад #{back}");
                    }
                }

                Assert.Less(minViewportYSeen, AnchorViewportY, "доля экрана должна была хотя бы раз опуститься СТРОГО ниже anchor — старая жёсткая граница на anchor снята задачей 1");
            }
            finally
            {
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                s.Follow.Dispose();
            }
        }

        // Issue #155, инвариант D — новый тест, именно он поймал бы провал
        // #153 (стоп-старт твина): при равномерном темпе ходов максимальный
        // интервал, в течение которого смещение камеры равно нулю, не
        // превышает 50мс.
        [TestCase(1f, TestName = "OneMovePerSecond")]
        [TestCase(2f, TestName = "TwoMovesPerSecond")]
        [TestCase(4f, TestName = "FourMovesPerSecond")]
        public void SteadyMovement_NoStopStart_MaxZeroMovementGapUnder50Ms(float movesPerSecond)
        {
            var s = CreateSetup();
            // Прогреваем до устойчивого режима до начала замера — первые
            // несколько кадров после самого первого хода не показательны.
            RunSteadyMovement(s, movesPerSecond, totalSeconds: 2f);

            var stepIntervalSeconds = 1f / movesPerSecond;
            var secondsSinceLastStep = 0f;
            var maxZeroGapSeconds = 0f;
            var currentZeroGapSeconds = 0f;
            var previousZ = s.Follow.CurrentPosition.z;

            var measuredSeconds = 0f;
            while (measuredSeconds < 3f)
            {
                secondsSinceLastStep += FrameDeltaSeconds;
                if (secondsSinceLastStep >= stepIntervalSeconds)
                {
                    s.Trail.TryAdvanceTo(new GridCoordinate(s.Trail.CurrentPosition.Row + 1, Width / 2));
                    secondsSinceLastStep -= stepIntervalSeconds;
                }

                s.Follow.AdvanceContinuousAnchorFollow(FrameDeltaSeconds, s.TargetDistance, s.MinDistance, s.MaxDistance);

                var currentZ = s.Follow.CurrentPosition.z;
                if (currentZ == previousZ)
                {
                    currentZeroGapSeconds += FrameDeltaSeconds;
                    maxZeroGapSeconds = Mathf.Max(maxZeroGapSeconds, currentZeroGapSeconds);
                }
                else
                {
                    currentZeroGapSeconds = 0f;
                }
                previousZ = currentZ;
                measuredSeconds += FrameDeltaSeconds;
            }

            Assert.LessOrEqual(maxZeroGapSeconds, 0.05f, $"movesPerSecond={movesPerSecond}: камера простаивала {maxZeroGapSeconds * 1000f:0}мс подряд без движения — стоп-старт");

            s.Follow.Dispose();
        }

        // Issue #158 (продолжение #155): мягкая граница жёсткого клампа
        // (A.3) должна наблюдаемо сгладить ПОДХОД к anchor+tolerance —
        // сравниваем максимальный скачок дистанции камера-игрок за кадр при
        // резком старте с дефолтной точкой начала нарастания (0.6) против
        // той же самой сцены, где мягкая коррекция фактически выключена
        // (startFraction=0.999 — зона нарастания почти нулевой ширины,
        // ближайшее возможное приближение к старому поведению "голый
        // хард-кламп" без деления на ноль). Прямое доказательство того, что
        // изменение действительно убирает толчок, а не просто не ломает
        // инварианты.
        [Test]
        public void SoftBoundary_HardStart_SmallerMaxFrameJumpThanWithoutSoftening()
        {
            var withSoftening = MeasureMaxTrailingDistanceJumpOnHardStart(TunnelCameraSoftBoundary.DefaultStartFraction);
            var withoutSoftening = MeasureMaxTrailingDistanceJumpOnHardStart(0.999f);

            Assert.Less(withSoftening, withoutSoftening,
                $"мягкая граница (startFraction={TunnelCameraSoftBoundary.DefaultStartFraction}) должна давать меньший максимальный скачок дистанции за кадр, чем почти-выключенная мягкая граница (max jump: {withSoftening} vs {withoutSoftening})");
        }

        // Дистанция камера-игрок восстанавливается из публичного CurrentPosition.z
        // (приватная _continuousCameraZ недоступна тестам) — корректно, пока
        // HeightOffset.z=0 и ручной эдж-скролл не используется (оба верны в
        // этом Setup, см. CreateSetup/HeightOffset наверху файла): тогда
        // CurrentPosition.z == _continuousCameraZ буквально, см.
        // AdvanceContinuousAnchorFollow — effectiveOffsetZ сворачивается в 0.
        private static float MeasureMaxTrailingDistanceJumpOnHardStart(float softBoundaryStartFraction)
        {
            var s = CreateSetup();
            var maxJump = 0f;
            var previousDistance = (s.Trail.CurrentPosition.Row + 0.5f) * TileSize - s.Follow.CurrentPosition.z;

            // Тот же "резкий старт" профиль, что и в инварианте C — темп,
            // заведомо толкающий дистанцию к anchor+tolerance с первых кадров.
            for (var cycle = 0; cycle < 60; cycle++)
            {
                s.Trail.TryAdvanceTo(new GridCoordinate(s.Trail.CurrentPosition.Row + 1, Width / 2));
                for (var t = 0; t < 8; t++)
                {
                    s.Follow.AdvanceContinuousAnchorFollow(FrameDeltaSeconds, s.TargetDistance, s.MinDistance, s.MaxDistance, softBoundaryStartFraction);

                    var playerWorldZ = (s.Trail.CurrentPosition.Row + 0.5f) * TileSize;
                    var currentDistance = playerWorldZ - s.Follow.CurrentPosition.z;
                    var jump = Mathf.Abs(currentDistance - previousDistance);
                    if (jump > maxJump) maxJump = jump;
                    previousDistance = currentDistance;
                }
            }

            s.Follow.Dispose();
            return maxJump;
        }

        [Test]
        public void SteadyState_AtAnchor_AtLeastTwoRowsBehindPlayerRemainVisible()
        {
            // Та же геометрическая честная оговорка, что и в #153: при
            // неизменных Pitch=50°/HeightOffset.y=6/DesiredVisibleTiles=4
            // достижимый максимум — 2 ряда, не 4 (см. PR issue #155 для
            // разбора чисел и альтернатив). Регрессионный барьер.
            var s = CreateSetup();
            RunSteadyMovement(s, movesPerSecond: 2f, totalSeconds: 5f);

            GameObject cameraObject = null;
            try
            {
                cameraObject = CreateCamera("TestCamera_RowsBehind", s.Follow, s.VFov);
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.SetPositionAndRotation(s.Follow.CurrentPosition, s.Follow.CurrentRotation);

                var visibleRowsBehind = 0;
                for (var offset = 1; offset <= 8; offset++)
                {
                    var row = s.Trail.CurrentPosition.Row - offset;
                    var worldPosition = s.Projection.ToWorldPosition(new GridCoordinate(row, Width / 2));
                    var viewport = camera.WorldToViewportPoint(worldPosition);
                    if (viewport.z > 0f && viewport.y >= 0f && viewport.y <= 1f) visibleRowsBehind = offset;
                    else break;
                }

                Assert.GreaterOrEqual(visibleRowsBehind, 2, "минимум 2 ряда позади должны оставаться в кадре — геометрический предел, см. doc-комментарий теста");
            }
            finally
            {
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                s.Follow.Dispose();
            }
        }

        [Test]
        public void MaterializedRowsAheadOfPlayer_StillAllLandInFrame()
        {
            var s = CreateSetup();
            RunSteadyMovement(s, movesPerSecond: 2f, totalSeconds: 5f);

            GameObject cameraObject = null;
            try
            {
                cameraObject = CreateCamera("TestCamera_ForwardVisibility", s.Follow, s.VFov);
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.SetPositionAndRotation(s.Follow.CurrentPosition, s.Follow.CurrentRotation);

                for (var offset = 0; offset <= 8; offset++)
                {
                    var row = s.Trail.CurrentPosition.Row + offset;
                    var worldPosition = s.Projection.ToWorldPosition(new GridCoordinate(row, Width / 2));
                    var viewport = camera.WorldToViewportPoint(worldPosition);

                    Assert.Greater(viewport.z, 0f, $"ряд +{offset} должен быть перед камерой");
                    Assert.GreaterOrEqual(viewport.y, 0f, $"ряд +{offset} не должен быть за нижним краем");
                    Assert.LessOrEqual(viewport.y, 1f, $"ряд +{offset} не должен быть за верхним краем");
                }
            }
            finally
            {
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                s.Follow.Dispose();
            }
        }

        [Test]
        public void IntroThenFirstSixMoves_PlayerTileStaysInFrameThroughout()
        {
            // Интро-твин (#140/#144) + Lerp якоря по прогрессу интро (#153)
            // не должны сломаться этой задачей. Во время интро CurrentPosition
            // двигает только AdvanceIntroTween — AdvanceContinuousAnchorFollow
            // (единственный механизм устоявшегося следования, задача 1) не
            // вызывается до его завершения (см. TunnelCameraController —
            // тот же порог IntroTweenProgress01>=1), здесь проверяем именно
            // эту границу.
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, Width / 2));
            var projection = new WorldGridProjection(TileSize, Width);
            var follow = new TunnelCameraFollow(trail, projection, HeightOffset);
            follow.ConfirmRun();

            GameObject cameraObject = null;
            try
            {
                cameraObject = new GameObject("TestCamera_IntroThenMoves");
                var camera = cameraObject.AddComponent<Camera>();
                camera.aspect = PortraitAspect;

                for (var row = 1; row <= 6; row++)
                {
                    follow.AdvanceIntroTween(0.05f);

                    var vFov = ComputeSteadyVerticalFov(projection, follow.CurrentPitchDegrees);
                    var targetDistance = TunnelCameraAnchor.ComputeTrailingDistanceForAnchor(
                        HeightOffset.y, follow.CurrentPitchDegrees, vFov, AnchorViewportY);
                    var fixedTrailingDistance = TunnelCameraFollow.TrailingRowsBehindPlayer * TileSize;
                    follow.TrailingDistance = Mathf.Lerp(fixedTrailingDistance, targetDistance, follow.IntroTweenProgress01);

                    trail.TryAdvanceTo(new GridCoordinate(row, Width / 2));

                    if (follow.IntroTweenProgress01 >= 1f)
                    {
                        var minDistance = TunnelCameraAnchor.ComputeTrailingDistanceForAnchor(
                            HeightOffset.y, follow.CurrentPitchDegrees, vFov, AnchorViewportY - BackToleranceViewportFraction);
                        var maxDistance = TunnelCameraAnchor.ComputeTrailingDistanceForAnchor(
                            HeightOffset.y, follow.CurrentPitchDegrees, vFov, AnchorViewportY + ToleranceViewportFraction);
                        follow.AdvanceContinuousAnchorFollow(0.05f, targetDistance, minDistance, maxDistance);
                    }
                    // Иначе — ничего: AdvanceIntroTween выше уже хард-синкнул
                    // CurrentPosition=TargetPosition на этом кадре.

                    camera.transform.SetPositionAndRotation(follow.CurrentPosition, follow.CurrentRotation);
                    camera.fieldOfView = vFov > 0f ? vFov : 60f;

                    var playerWorld = projection.ToWorldPosition(trail.CurrentPosition);
                    var viewport = camera.WorldToViewportPoint(playerWorld);

                    Assert.Greater(viewport.z, 0f, $"ход {row}: плитка игрока должна быть перед камерой");
                    Assert.GreaterOrEqual(viewport.y, 0f, $"ход {row}: плитка игрока не должна быть за нижним краем");
                    Assert.LessOrEqual(viewport.y, 1f, $"ход {row}: плитка игрока не должна быть за верхним краем");
                }
            }
            finally
            {
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
                follow.Dispose();
            }
        }
    }
}
