using System;
using Burmalda.Core;
using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Плавное следование 3D-камеры от третьего лица за трейлом (PRD 16):
    /// камера идёт сверху-сзади за <see cref="GridTraceTrail.CurrentPosition"/>
    /// (через <see cref="WorldGridProjection.ToWorldPosition"/>), а не
    /// телепортируется на каждый ход. Портировано из camera-логики
    /// legacy/burmolda_demo.html (cameraRow/cameraTargetRow, draw()) — офсет
    /// отставания по ряду перенесён буквально, константа сглаживания
    /// отличается от прототипа (замедлена по запросу владельца продукта, см.
    /// <see cref="SmoothingFactor"/>).
    ///
    /// Хотфикс: держит устойчивую ширину обзора тоннеля не поворотом, а
    /// динамическим FOV — см. <see cref="TunnelCameraFraming"/> и
    /// <see cref="TunnelCameraController"/>.
    ///
    /// <b>Архитектура интро (переписана 2026-08-14, прямой запрос владельца
    /// продукта):</b> прежняя версия интерполировала top-down интро ->
    /// устоявшийся режим ПО РЯДАМ (сколько игрок реально продвинулся) — это
    /// оказалось источником серии регрессий на реальном устройстве (плита
    /// уходит за край экрана, геометрия не совпадает при частичном
    /// продвижении, "поле слишком далеко"). Row-based интерполяция убрана
    /// ЦЕЛИКОМ. Вместо неё:
    /// - До <see cref="ConfirmRun"/> — камера статично стоит на
    ///   <see cref="IntroPitchDegrees"/>/<see cref="IntroHeightOffsetZ"/>,
    ///   без анимации, ждёт.
    /// - <see cref="ConfirmRun"/> (тап по собственной текущей позиции игрока,
    ///   см. <see cref="GridTraceInputController.RunConfirmed"/>) запускает
    ///   ОДИН короткий time-based твин (<see cref="TweenDurationSeconds"/>,
    ///   не завязан на движение игрока) от интро- к устоявшимся
    ///   Pitch/HeightOffset.Z, с easing (см. <see cref="AdvanceIntroTween"/>).
    /// - Row-based трейлинг (<see cref="TrailingRowsBehindPlayer"/>) для
    ///   САМОЙ ПОЗИЦИИ (какой ряд тоннеля показывать) остаётся — это не
    ///   про интро, отдельный, не менявшийся механизм устоявшегося
    ///   следования за игроком.
    ///
    /// <see cref="HeightOffset"/>, <see cref="PitchDegrees"/>,
    /// <see cref="IntroPitchDegrees"/> и <see cref="IntroHeightOffsetZ"/> —
    /// публично изменяемые свойства, не только конструкторские параметры:
    /// раньше все применялись один раз в конструкторе, и Transform камеры
    /// каждый кадр перезаписывался вычисленным значением — из инспектора/
    /// Scene view положение камеры поменять было нельзя даже в Play Mode,
    /// правка терялась на следующем кадре. Теперь
    /// <see cref="TunnelCameraController"/> прокидывает значения своих полей
    /// сюда каждый кадр — правки в инспекторе применяются вживую, без
    /// пересборки Follow/рестарта забега.
    /// </summary>
    public sealed class TunnelCameraFollow : IDisposable
    {
        // Было буквально из legacy/burmolda_demo.html, draw(): 0.045 (cameraRow
        // += (cameraTargetRow-cameraRow)*0.045). Дважды замедлено по прямому
        // запросу владельца продукта (0.045 -> 0.02 -> 0.01), затем ВРЕМЕННО
        // удвоено обратно до 0.02 (2026-08-18, прямой запрос — "ускорить в 2
        // раза", пощупать на билде) — камера ощущалась слишком резкой/
        // дёрганой на 0.045, не давала игроку спокойно обдумать маршрут
        // (черновой тюнинг «на глазок», без формального issue — финальное
        // значение задаст плейтест баланса, Спринт 18). Не закоммичено в
        // main — только для локального билда на щуп.
        // Это сглаживание — про устоявшееся следование ПОСЛЕ интро (реальное
        // продвижение игрока по рядам), не про сам твин интро — см.
        // AdvanceIntroTween, у которого своя, не экспоненциальная анимация.
        private const float SmoothingFactor = 0.02f;

        // legacy/burmolda_demo.html, tryAct()/returnToAltar(): cameraTargetRow = r-5
        // (прототип клампил к Math.max(0, ...) — здесь кламп убран, см.
        // ComputeTargetPosition: на 2D top-down прототипе клампинг был не
        // критичен, а в 3D-геометрии этого проекта именно он схлопывал
        // дистанцию камера-игрок на первых 5 рядах). Публичная — нужна
        // TunnelCameraController (калибровка FOV, см.
        // TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow)
        // и тестам вне этого класса, а не только ComputeTargetPosition здесь.
        // Устоявшееся следование за игроком — НЕ про интро, не переписывалось.
        public const int TrailingRowsBehindPlayer = 3;

        // Хотфикс: игровой (устоявшийся) Rotation X — обоснование в
        // doc-комментарии класса. Публичная константа, а не только значение
        // по умолчанию параметра конструктора — TunnelCameraController
        // использует её как значение по умолчанию своего инспекторного поля.
        public const float DefaultPitchDegrees = 50f;

        // Top-down интро на старте забега. 90° исключён (сингулярность взгляда
        // строго по вертикали — forward параллелен "up").
        public const float DefaultIntroPitchDegrees = 70f;

        // Геометрический конфликт (не про pitch): устоявшийся HeightOffset.Z
        // на самом старте забега физически уводит камеру мимо стартовой
        // плиты — см. doc-комментарий класса. Было -1 (подобрано численно
        // через WorldToViewportPoint), ВРЕМЕННО выставлено в 1 (2026-08-18,
        // прямой запрос владельца продукта, пощупать на билде). Не
        // закоммичено в main — только для локального билда на щуп, как и
        // SmoothingFactor выше.
        public const float DefaultIntroHeightOffsetZ = 4f;

        // Прямой запрос владельца продукта (2026-08-14): "быстро, не 2-3
        // секунды". Твин интро -> устоявшийся режим, см. AdvanceIntroTween.
        public const float TweenDurationSeconds = 0.35f;

        // Найдено на реальном устройстве (2026-08-14): NudgeForward (эдж-скролл,
        // TunnelCameraController) без верхнего предела накопления мог увести
        // камеру ЗА пределы уже сгенерированного/раскрытого тоннеля —
        // Movement.TunnelGridReveal/Generation.SegmentRowProvider
        // материализуют плиты только на RowsAheadOfPlayer=8 рядов вперёд
        // ИГРОКА, а не камеры; камера, укатившаяся дальше этого, смотрит в
        // буквально пустоту (подтверждено: чёрный экран на устройстве после
        // долгого удержания пальца в зоне эдж-скролла). Кап держит запас
        // (TrailingRowsBehindPlayer=5 позади + этот кап) заметно меньше
        // 8+5=13 рядов, за которыми начинается нераскрытая пустота —
        // безопасный отступ, а не точная граница.
        public const float MaxManualForwardOffset = 4f;

        // Issue #153, тумблер 1: доезд фиксированной длительности вместо
        // экспоненциального сглаживания — см. AdvanceStepTween. 100мс, как
        // просил владелец продукта после отката #151 (упреждение вместо
        // этого убрано целиком — не решает проблему остаточного отставания,
        // только маскирует её ценой "уползания" при быстром вводе).
        public const float DefaultStepTweenDurationSeconds = 0.1f;

        private readonly GridTraceTrail _trail;
        private readonly WorldGridProjection _projection;
        private Vector3 _heightOffset;
        private float _pitchDegrees;
        private float _introPitchDegrees;
        private float _introHeightOffsetZ;
        private float _manualForwardOffsetZ;
        private bool _hasConfirmedRun;
        private float _introTweenElapsedSeconds;
        private GridCoordinate _confirmRunPlayerPosition;
        private bool _disposed;

        // Issue #153, тумблер 1 — состояние доезда фиксированной длительности,
        // см. AdvanceStepTween. Поддерживается ВСЕГДА (обновляется в
        // OnPositionChanged независимо от того, вызывает ли кто-то
        // AdvanceStepTween) — тумблер живёт снаружи, в TunnelCameraController
        // (какой из двух методов, Tick() или AdvanceStepTween, зовётся
        // каждый кадр), Follow сам о тумблере не знает. Это гарантирует, что
        // Tick()/SmoothingFactor-путь остаётся побайтово прежним при
        // выключенном тумблере — новые поля просто никогда не читаются.
        private float _stepTweenDurationSeconds = DefaultStepTweenDurationSeconds;
        private Vector3 _stepTweenStartPosition;
        private float _stepTweenElapsedSeconds;

        // Issue #153, тумблер 2 (якорь по вьюпорту): мировой эквивалент
        // TrailingRowsBehindPlayer — дефолт СОВПАДАЕТ со старой формулой
        // (TrailingRowsBehindPlayer·tileSize), TrailingDistance ниже полностью
        // обратно совместима, пока её никто явно не переопределил.
        // TunnelCameraController переопределяет её каждый кадр анкор-
        // производным значением, только пока тумблер включён — Follow сам о
        // тумблере/вьюпорте/Camera ничего не знает, только хранит число.
        private float _trailingDistance;

        public TunnelCameraFollow(
            GridTraceTrail trail,
            WorldGridProjection projection,
            Vector3 heightOffset,
            float pitchDegrees = DefaultPitchDegrees,
            float introPitchDegrees = DefaultIntroPitchDegrees,
            float introHeightOffsetZ = DefaultIntroHeightOffsetZ)
        {
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _projection = projection;
            _heightOffset = heightOffset;
            _pitchDegrees = pitchDegrees;
            _introPitchDegrees = introPitchDegrees;
            _introHeightOffsetZ = introHeightOffsetZ;

            // Мировой эквивалент старого TrailingRowsBehindPlayer — см.
            // doc-комментарий TrailingDistance. Тот же tileSize, что и
            // WorldGridProjection.ToWorldPosition использует для Z ниже.
            _trailingDistance = TrailingRowsBehindPlayer * _projection.TileSize;

            TargetPosition = ComputeTargetPosition(_trail.CurrentPosition);
            CurrentPosition = TargetPosition;

            // "Твин уже завершён" с самого начала — до первого реального
            // хода AdvanceStepTween должна быть не-op (см. её докстринг и
            // тест AdvanceStepTween_NoStepYet_IsNoOpAtInitialPosition), а не
            // лерпить из Vector3.zero к начальной позиции.
            _stepTweenStartPosition = CurrentPosition;
            _stepTweenElapsedSeconds = _stepTweenDurationSeconds;

            // PositionChanged (а не Advanced) — камера должна следовать за
            // игроком и назад, при возврате на уже пройденную плиту (#61),
            // не только вперёд на новых плитках (иначе некуда отступить).
            _trail.PositionChanged += OnPositionChanged;
        }

        /// <summary>
        /// Дистанция по Z (мировые единицы, НЕ ряды) от плитки игрока до
        /// целевого положения камеры — issue #153, тумблер 2. По умолчанию
        /// равна <see cref="TrailingRowsBehindPlayer"/>·tileSize (буквально
        /// старое поведение); <see cref="TunnelCameraController"/>
        /// переопределяет её каждый кадр анкор-производным значением
        /// (<see cref="TunnelCameraAnchor.ComputeTrailingDistanceForAnchor"/>),
        /// только пока тумблер 2 включён. Изменение немедленно пересчитывает
        /// <see cref="TargetPosition"/>, по тому же принципу, что и
        /// <see cref="HeightOffset"/>.
        /// </summary>
        public float TrailingDistance
        {
            get => _trailingDistance;
            set
            {
                _trailingDistance = value;
                TargetPosition = ComputeTargetPosition(_trail.CurrentPosition);
            }
        }

        /// <summary>
        /// Смещение камеры над/позади игрока. Изменение немедленно
        /// пересчитывает <see cref="TargetPosition"/> от текущей позиции
        /// трейла — не нужно ждать следующего хода игрока, чтобы увидеть
        /// эффект. Z-компонента до завершения твина интро не применяется
        /// напрямую — см. <see cref="IntroHeightOffsetZ"/>/<see cref="ConfirmRun"/>.
        /// </summary>
        public Vector3 HeightOffset
        {
            get => _heightOffset;
            set
            {
                _heightOffset = value;
                TargetPosition = ComputeTargetPosition(_trail.CurrentPosition);
            }
        }

        /// <summary>
        /// Длительность доезда фиксированной длительности (секунды) — issue
        /// #153, тумблер 1, см. <see cref="AdvanceStepTween"/>. Читается
        /// заново на каждый вызов <see cref="AdvanceStepTween"/> — смена
        /// значения на лету (debug-панель) безопасна даже посреди уже
        /// идущего твина. Отрицательные значения не имеют смысла — клампится
        /// к 0 (мгновенный снап, как <see cref="SnapToTarget"/>).
        /// </summary>
        public float StepTweenDurationSeconds
        {
            get => _stepTweenDurationSeconds;
            set => _stepTweenDurationSeconds = Mathf.Max(0f, value);
        }

        /// <summary>Точка, к которой плавно движется камера — со смещением позади игрока (PRD 16).</summary>
        public Vector3 TargetPosition { get; private set; }

        /// <summary>Текущая сглаженная позиция камеры — присваивать Transform.position.</summary>
        public Vector3 CurrentPosition { get; private set; }

        /// <summary>
        /// Целевой (устоявшийся, игровой) наклон камеры (Rotation X),
        /// градусы — то значение, к которому <see cref="TargetRotation"/>
        /// приходит по итогам твина интро (см. doc-комментарий класса), а
        /// не то, что показывает камера прямо сейчас (для этого —
        /// <see cref="TargetRotation"/>). Публично изменяем — значение можно
        /// крутить в инспекторе <see cref="TunnelCameraController"/> вживую,
        /// включая Play Mode.
        /// </summary>
        public float PitchDegrees
        {
            get => _pitchDegrees;
            set => _pitchDegrees = value;
        }

        /// <summary>
        /// Стартовый (top-down интро) наклон камеры, градусы — см.
        /// doc-комментарий класса. Публично изменяем по тому же принципу,
        /// что и <see cref="PitchDegrees"/>.
        /// </summary>
        public float IntroPitchDegrees
        {
            get => _introPitchDegrees;
            set => _introPitchDegrees = value;
        }

        /// <summary>
        /// Стартовая (top-down интро) Z-компонента <see cref="HeightOffset"/>
        /// — см. doc-комментарий класса и <see cref="DefaultIntroHeightOffsetZ"/>
        /// (геометрический конфликт, не про поворот: устоявшийся Z уводит
        /// камеру мимо стартовой плиты, пока твин интро ещё не завершён).
        /// Публично изменяема по тому же принципу, что и
        /// <see cref="IntroPitchDegrees"/>; изменение немедленно
        /// пересчитывает <see cref="TargetPosition"/>. X/Y компоненты
        /// HeightOffset не интерполируются — конфликт был только по Z.
        /// </summary>
        public float IntroHeightOffsetZ
        {
            get => _introHeightOffsetZ;
            set
            {
                _introHeightOffsetZ = value;
                TargetPosition = ComputeTargetPosition(_trail.CurrentPosition);
            }
        }

        /// <summary>
        /// Поворот камеры прямо сейчас — интерполяция между
        /// <see cref="IntroPitchDegrees"/> и <see cref="PitchDegrees"/> по
        /// ходу твина интро (см. <see cref="AdvanceIntroTween"/>), не по
        /// рядам. Вычисляется живьём при каждом обращении — не кэшируется,
        /// поэтому не нужно отдельно реагировать на изменение
        /// PitchDegrees/IntroPitchDegrees, чтобы результат оставался свежим.
        /// Без поворота влево-вправо/крена (yaw/roll всегда 0).
        /// </summary>
        public Quaternion TargetRotation => Quaternion.Euler(ComputeCurrentPitchDegrees(), 0f, 0f);

        /// <summary>Совпадает с <see cref="TargetRotation"/> — поворот не сглаживается отдельно, твин интро уже даёт плавность.</summary>
        public Quaternion CurrentRotation => TargetRotation;

        /// <summary>
        /// То же значение, что и угол в <see cref="TargetRotation"/>/<see cref="CurrentRotation"/>
        /// (<c>eulerAngles.x</c>), но напрямую как число градусов — нужно
        /// <see cref="TunnelCameraController"/>, чтобы калибровать FOV
        /// (<see cref="TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees"/>)
        /// от РЕАЛЬНОГО текущего pitch каждый кадр (включая ход твина интро), а
        /// не только от устоявшегося <see cref="PitchDegrees"/> — иначе FOV,
        /// откалиброванный под устоявшуюся геометрию, на интро-pitch (близко к
        /// вертикали) даёт слишком узкий кадр: ближняя плитка раздувается,
        /// дальние ряды уходят за край раньше времени.
        /// </summary>
        public float CurrentPitchDegrees => ComputeCurrentPitchDegrees();

        /// <summary>
        /// Продвигает сглаживание позиции на один тик — устоявшееся
        /// следование за РЕАЛЬНЫМ продвижением игрока по рядам (после того,
        /// как твин интро уже завершён), не про сам твин (см.
        /// <see cref="AdvanceIntroTween"/>). Константа сглаживания
        /// применяется за вызов, а не масштабируется на deltaTime (как и в
        /// прототипе, где draw() вызывается раз за кадр). Безопасно
        /// вызывать каждый кадр в любой момент, включая во время твина
        /// интро — там разрыв между Target/CurrentPosition всегда 0
        /// (<see cref="AdvanceIntroTween"/> держит их синхронными
        /// напрямую), так что Tick() в этот момент — фактический не-op.
        /// </summary>
        public void Tick()
        {
            CurrentPosition += (TargetPosition - CurrentPosition) * SmoothingFactor;
        }

        /// <summary>
        /// Issue #153, тумблер 1: доезд ФИКСИРОВАННОЙ длительности вместо
        /// экспоненциального сглаживания (<see cref="Tick"/>, не тронут —
        /// это параллельный, независимый путь; <see cref="TunnelCameraController"/>
        /// решает снаружи, какой из двух вызывать каждый кадр, в
        /// зависимости от тумблера в debug-панели).
        ///
        /// В отличие от экспоненты (константная ДОЛЯ разрыва за тик, поэтому
        /// установившееся отставание растёт пропорционально скорости цели —
        /// см. историю #151), доезд фиксированной длительности всегда
        /// закрывает ТЕКУЩИЙ разрыв ровно за <see cref="StepTweenDurationSeconds"/>
        /// секунд, независимо от его размера — гарантированно приходит в
        /// цель, без остаточной ошибки. Если следующий реальный ход
        /// случается ПОСЛЕ того, как предыдущий твин уже доехал (что
        /// выполняется для любого реалистичного темпа игры, раз
        /// длительность мала — 100мс по умолчанию), доля кадра, на которой
        /// оказывается игрок, оказывается ОДНОЙ И ТОЙ ЖЕ при любом темпе —
        /// именно это и требует инвариант A (issue #153).
        ///
        /// Новый ход ДО завершения текущего твина (быстрый непрерывный
        /// свайп) не телепортирует камеру — <see cref="OnPositionChanged"/>
        /// перезапускает твин от ТЕКУЩЕЙ (возможно, ещё в полёте) позиции к
        /// новой цели, снова на полную длительность.
        ///
        /// Безопасно вызывать каждый кадр в любой момент, включая во время
        /// твина интро — там разрыв между Target/CurrentPosition всегда 0
        /// (как и для <see cref="Tick"/>), так что это фактический не-op.
        /// </summary>
        public void AdvanceStepTween(float deltaSeconds)
        {
            if (_stepTweenElapsedSeconds >= _stepTweenDurationSeconds)
            {
                // Твин уже доехал — держим CurrentPosition РОВНО в
                // TargetPosition (а не оставляем как есть) на случай, если
                // TargetPosition успела чуть измениться по не-шаговой
                // причине (HeightOffset и т.п. — существующее поведение,
                // не про инвариант B, см. doc-комментарий HeightOffset).
                CurrentPosition = TargetPosition;
                return;
            }

            _stepTweenElapsedSeconds = Mathf.Min(_stepTweenElapsedSeconds + deltaSeconds, _stepTweenDurationSeconds);

            if (_stepTweenElapsedSeconds >= _stepTweenDurationSeconds || _stepTweenDurationSeconds <= 0f)
            {
                CurrentPosition = TargetPosition; // явный снап на границе — не полагаемся на Lerp(t=1) быть побитово точным
                return;
            }

            var t = _stepTweenElapsedSeconds / _stepTweenDurationSeconds;
            CurrentPosition = Vector3.Lerp(_stepTweenStartPosition, TargetPosition, t);
        }

        /// <summary>
        /// Запускает твин интро -> устоявшийся режим (см. doc-комментарий
        /// класса) — вызывать на <see cref="GridTraceInputController.RunConfirmed"/>
        /// (тап по собственной текущей позиции игрока, до первого реального
        /// хода). Идемпотентна — повторный вызов, пока твин уже идёт или
        /// завершён, не перезапускает его и не бросает исключение (защита
        /// от повторного тапа/двойного триггера). Сама анимация продвигается
        /// через <see cref="AdvanceIntroTween"/>, эта функция только "взводит" её.
        /// </summary>
        public void ConfirmRun()
        {
            if (_hasConfirmedRun) return;
            _hasConfirmedRun = true;
            _introTweenElapsedSeconds = 0f;
            // Снимок ряда игрока НА МОМЕНТ подтверждения — см. doc-комментарий
            // AdvanceIntroTween: твин ниже имеет право хард-синкать
            // CurrentPosition=TargetPosition только пока игрок ещё не
            // сдвинулся с этой позиции.
            _confirmRunPlayerPosition = _trail.CurrentPosition;
        }

        /// <summary>
        /// Продвигает твин интро -> устоявшийся режим на
        /// <paramref name="deltaSeconds"/> реальных секунд — time-based, не
        /// завязан на движение игрока (в отличие от старой убранной
        /// row-based интерполяции, см. doc-комментарий класса). Не-op, пока
        /// <see cref="ConfirmRun"/> не вызван, и не-op после того, как твин
        /// уже отыграл <see cref="TweenDurationSeconds"/> — повторные вызовы
        /// безопасны, вторая анимация не запускается (прямой запрос
        /// владельца продукта: "не прерывай и не запускай второй
        /// одновременно"). Пока твин идёт И игрок ещё не сдвинулся с
        /// позиции, зафиксированной в <see cref="ConfirmRun"/>, держит
        /// <see cref="CurrentPosition"/> НАПРЯМУЮ равной <see cref="TargetPosition"/>
        /// (без экспоненциального сглаживания <see cref="Tick"/>) — сам твин
        /// уже даёт нужную плавность через easing (см. <see cref="EaseOutCubic"/>),
        /// дополнительное сглаживание поверх него ощущалось бы медленнее
        /// заявленных 0.35с.
        ///
        /// <b>Хотфикс 2026-08-14 (реально сломанный сценарий на устройстве,
        /// не гипотеза):</b> раньше хард-синк применялся безусловно, каждый
        /// кадр окна в 0.35с после ConfirmRun — в том числе если игрок успевал
        /// реально шагнуть в это же окно (непрерывный жест: палец не
        /// отрывался между тапом по своей же плите и первым свайпом вперёд).
        /// TargetPosition завязана на РЯД игрока (см. ComputeTargetPosition/
        /// OnPositionChanged) — реальный ход двигает её независимо от твина,
        /// и безусловный хард-синк тогда мгновенно телепортировал камеру на
        /// новый ряд БЕЗ Tick()/SmoothingFactor, что на устройстве читалось
        /// как рывок на несколько клеток около 3-4 хода (ранние ходы забега
        /// чаще всего попадают в это окно). Раньше этот путь ошибочно не
        /// подозревался — предыдущие два захода чинили FOV и снап-на-тапе
        /// (SnapToTarget/ResetManualForwardOffset), ни один не трогал этот
        /// метод. Теперь: если ряд игрока успел измениться с момента
        /// ConfirmRun, твин считается завершённым немедленно (без хард-синка
        /// на этом кадре) — дальше обычный Tick()/SmoothingFactor подхватывает
        /// CurrentPosition как для любого обычного продвижения.
        /// </summary>
        public void AdvanceIntroTween(float deltaSeconds)
        {
            if (!_hasConfirmedRun) return;
            if (_introTweenElapsedSeconds >= TweenDurationSeconds) return; // уже завершён — дальше обычный Tick()

            if (!_trail.CurrentPosition.Equals(_confirmRunPlayerPosition))
            {
                // Игрок сдвинулся до того, как твин доиграл — см.
                // doc-комментарий выше. Завершаем твин немедленно, без
                // хард-синка на этом кадре: TargetPosition уже пересчитана
                // подпиской OnPositionChanged на реальный ход, CurrentPosition
                // догонит её как обычно, через Tick()/SmoothingFactor.
                _introTweenElapsedSeconds = TweenDurationSeconds;
                return;
            }

            _introTweenElapsedSeconds = Mathf.Min(_introTweenElapsedSeconds + deltaSeconds, TweenDurationSeconds);
            TargetPosition = ComputeTargetPosition(_trail.CurrentPosition);
            CurrentPosition = TargetPosition;
        }

        /// <summary>
        /// Мгновенно (без сглаживания) показывает текущую позицию трейла у
        /// низа экрана — сбрасывает накопленный <see cref="NudgeForward"/> и
        /// ставит <see cref="CurrentPosition"/> сразу в <see cref="TargetPosition"/>.
        ///
        /// <b>Больше НЕ вызывается на каждый новый тап</b> (2026-08-14,
        /// прямой запрос владельца продукта, реально сломанный сценарий, не
        /// гипотеза) — раньше вызывалась из
        /// <see cref="TunnelCameraController"/> на каждый переход "не
        /// прижат"->"прижат" (см. <see cref="GridTraceInputController.PressStarted"/>),
        /// но <see cref="SmoothingFactor"/>=0.01 очень медленный: если между
        /// подряд идущими тапами (палец отрывался/прижимался заново, не
        /// один непрерывный свайп) между <see cref="CurrentPosition"/> и
        /// <see cref="TargetPosition"/> накапливался заметный разрыв,
        /// мгновенный снап на КАЖДОМ новом тапе читался на устройстве как
        /// рывок камеры на несколько клеток вперёд. Обычное продвижение
        /// теперь ВСЕГДА идёт только через <see cref="Tick"/>/
        /// <see cref="SmoothingFactor"/>, без исключений на новый тап — см.
        /// <see cref="ResetManualForwardOffset"/> (тот же сброс эдж-скролла,
        /// но без снапа позиции — им теперь и заменён вызов на
        /// PressStarted). Метод оставлен публичным (не удалён) — годится
        /// для места, где мгновенный снап РЕАЛЬНО нужен (например, самый
        /// первый старт забега, если для него когда-нибудь понадобится
        /// отдельная обработка), просто такого места сейчас нет.
        ///
        /// Не трогает состояние твина интро (<see cref="ConfirmRun"/>/
        /// <see cref="AdvanceIntroTween"/>) — это независимые механизмы.
        /// </summary>
        public void SnapToTarget()
        {
            _manualForwardOffsetZ = 0f;
            TargetPosition = ComputeTargetPosition(_trail.CurrentPosition);
            CurrentPosition = TargetPosition;
        }

        /// <summary>
        /// Сбрасывает накопленный <see cref="NudgeForward"/> БЕЗ снапа
        /// позиции — вызывать на каждый новый тап (переход "не прижат"->
        /// "прижат", см. <see cref="GridTraceInputController.PressStarted"/>)
        /// ВМЕСТО <see cref="SnapToTarget"/> (2026-08-14, см. её
        /// doc-комментарий — почему мгновенный снап там больше не нужен).
        /// Сам сброс эдж-скролла всё ещё нужен на каждом новом тапе — иначе
        /// TargetPosition на следующем тапе стартовал бы с прежнего, уже
        /// неактуального накопленного смещения (см. doc-комментарий
        /// <see cref="NudgeForward"/>) — просто без сопутствующего снапа
        /// <see cref="CurrentPosition"/>: она по-прежнему плавно, тиком,
        /// доедет до нового <see cref="TargetPosition"/> через
        /// <see cref="Tick"/>/<see cref="SmoothingFactor"/>, как и любое
        /// обычное продвижение.
        /// </summary>
        public void ResetManualForwardOffset()
        {
            _manualForwardOffsetZ = 0f;
            TargetPosition = ComputeTargetPosition(_trail.CurrentPosition);
        }

        /// <summary>
        /// Двигает камеру вперёд НАПРЯМУЮ (без сглаживания <see cref="Tick"/>)
        /// на <paramref name="worldDistance"/> мировых единиц — эдж-скролл,
        /// пока палец у верхней границы нижней трети экрана (см.
        /// <see cref="TunnelCameraController"/>): отклик должен быть
        /// мгновенным, а не запаздывающим через экспоненциальное сглаживание
        /// (которое как раз и рассчитано на плавность обычного продвижения
        /// по плитам, не на ручной скролл). Накопленное смещение сбрасывается
        /// только на <see cref="SnapToTarget"/> (новый тап), не на обычное
        /// продвижение трейла — иначе сам эдж-скролл был бы бесполезен: любой
        /// следующий ход стирал бы его эффект.
        /// </summary>
        public void NudgeForward(float worldDistance)
        {
            if (worldDistance <= 0f) return;

            // Кап — см. doc-комментарий MaxManualForwardOffset. CurrentPosition
            // двигается только на РЕАЛЬНО применённую часть запроса (0, если
            // уже на кап), иначе она бы продолжала уезжать даже после того,
            // как "логическое" смещение перестало расти.
            var previousOffset = _manualForwardOffsetZ;
            _manualForwardOffsetZ = Mathf.Min(_manualForwardOffsetZ + worldDistance, MaxManualForwardOffset);
            var appliedDelta = _manualForwardOffsetZ - previousOffset;

            TargetPosition = ComputeTargetPosition(_trail.CurrentPosition);
            CurrentPosition += new Vector3(0f, 0f, appliedDelta);
        }

        /// <summary>Отписывается от трейла. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.PositionChanged -= OnPositionChanged;
            _disposed = true;
        }

        private void OnPositionChanged(GridCoordinate coordinate)
        {
            // Issue #153, тумблер 1: перезапускаем твин фиксированной
            // длительности ОТ ТЕКУЩЕЙ позиции (даже если предыдущий твин ещё
            // не доехал — быстрый непрерывный свайп) — см. AdvanceStepTween.
            // Безвредно, если AdvanceStepTween вообще не вызывается
            // (тумблер выключен, TunnelCameraController зовёт только Tick()) —
            // эти поля тогда просто никогда не читаются.
            _stepTweenStartPosition = CurrentPosition;
            _stepTweenElapsedSeconds = 0f;

            TargetPosition = ComputeTargetPosition(coordinate);
        }

        private Vector3 ComputeTargetPosition(GridCoordinate playerPosition)
        {
            // #62: камера двигается только вперёд-назад (по Z) — столбец
            // зафиксирован на центре ширины тоннеля, не следует за игроком
            // по X (иначе камера уезжает влево-вправо при диагональном пути).
            // Ряд для X роли не играет (см. WorldGridProjection.ToWorldPosition —
            // X зависит только от Column), берём 0 как заглушку.
            var x = _projection.ToWorldPosition(new GridCoordinate(0, _projection.Width / 2)).x;

            // Issue #153, тумблер 2: TrailingDistance — мировая дистанция
            // (не ряды), по умолчанию совпадает со старой формулой
            // (TrailingRowsBehindPlayer·tileSize), при включённом тумблере
            // переопределяется TunnelCameraController анкор-производным
            // значением. Формула ниже (playerRow+0.5)*tileSize — буквально
            // Z-часть WorldGridProjection.ToWorldPosition.
            //
            // НЕ клампится к 0 (было Math.Max(0, ...) — убрано 2026-08-14,
            // найдено через LogCameraDiagnosticMatrix на реальном устройстве):
            // клампинг на первых TrailingRowsBehindPlayer рядах схлопывал
            // дистанцию камера-игрок почти до нуля (на row=0 камера
            // оказывалась ВПЕРЕДИ игрока по Z, а не позади — отсюда "поле не
            // попадает в кадр"/плитка не в кадре сразу после ConfirmRun).
            // Отрицательный Row — такой же корректный мировой Z, как и
            // положительный (нет ни assert'ов, ни индексации по Row нигде
            // на пути). Без клампа дистанция камера-игрок ПОСТОЯННА
            // (TrailingDistance + HeightOffset.Z) на любом Row, включая 0.
            var playerWorldZ = (playerPosition.Row + 0.5f) * _projection.TileSize;
            var followZ = playerWorldZ - TrailingDistance;

            // Z-компонента HeightOffset интерполируется ходом твина интро
            // (см. ComputeIntroTweenProgress01/AdvanceIntroTween), НЕ по
            // рядам, как раньше — X/Y применяются напрямую, конфликт был
            // только по Z.
            var progress = ComputeIntroTweenProgress01();
            var effectiveOffsetZ = Mathf.Lerp(_introHeightOffsetZ, _heightOffset.z, progress);
            // + _manualForwardOffsetZ — накопленный эдж-скролл (NudgeForward),
            // независимая надбавка поверх обычного трейлинга по рядам.
            var effectiveOffset = new Vector3(_heightOffset.x, _heightOffset.y, effectiveOffsetZ + _manualForwardOffsetZ);

            return new Vector3(x, 0f, followZ) + effectiveOffset;
        }

        /// <summary>
        /// 0 — твин интро ещё не запущен (<see cref="ConfirmRun"/> не
        /// вызван) или только что начался, 1 — твин полностью отыграл
        /// <see cref="TweenDurationSeconds"/>. Общий для интерполяции pitch
        /// (<see cref="ComputeCurrentPitchDegrees"/>) и Z-компоненты
        /// HeightOffset (<see cref="ComputeTargetPosition"/>) — оба должны
        /// двигаться синхронно одним и тем же твином, не по отдельности.
        /// </summary>
        private float ComputeIntroTweenProgress01()
        {
            // Восстановлено (issue #109 B.2): причины, из-за которых твин
            // был отключён целиком (framing "поле далеко", рывок камеры на
            // 3-4 ходу) устранены отдельными фиксами в этой же истории —
            // Z-компенсация HeightOffset (см. TunnelCameraController) и
            // AdvanceIntroTween, реагирующий на реальное движение игрока
            // внутри окна твина (см. её докстринг). Реальный прогресс,
            // не хардкод: 0 — ConfirmRun ещё не вызван, растёт линейно до 1
            // за TweenDurationSeconds (сама плавность — через EaseOutCubic
            // в ComputeCurrentPitchDegrees/ComputeTargetPosition через Lerp).
            if (!_hasConfirmedRun) return 0f;
            var linear = Mathf.Clamp01(_introTweenElapsedSeconds / TweenDurationSeconds);
            return EaseOutCubic(linear);
        }

        // Резкий старт, плавное торможение к цели — ощущается быстрее и
        // приятнее чистого Lerp (прямой запрос владельца продукта). См.
        // ComputeIntroTweenProgress01 — сама кривая больше не наблюдаема
        // через публичный API, пока интро отключено.
        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        private float ComputeCurrentPitchDegrees()
        {
            var progress = ComputeIntroTweenProgress01();
            return Mathf.Lerp(_introPitchDegrees, _pitchDegrees, progress);
        }
    }
}
