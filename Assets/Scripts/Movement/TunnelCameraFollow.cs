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
    /// Хотфикс (Rotation X 18.4°/29°/35° -> 50°): целевой (игровой) наклон
    /// камеры больше не вычисляется динамическим «взглядом вперёд» — вместо
    /// этого он фиксирован на <see cref="PitchDegrees"/> в устоявшемся
    /// режиме. Устойчивость ширины обзора тоннеля на разных аспектах экрана
    /// обеспечивает не поворот, а динамический FOV — см.
    /// <see cref="TunnelCameraFraming"/> и <see cref="TunnelCameraController"/>.
    ///
    /// Top-down интро на старте забега (возвращено — та же формула, что была
    /// у старой убранной LookAheadRowsBeyondPlayer-логики, только для угла,
    /// не для точки взгляда): в момент старта трейлинг-ряд камеры клампится
    /// к 0 и совпадает с позицией игрока — на фиксированном игровом pitch
    /// стартовая плитка физически оказывалась вне кадра (проверено вручную
    /// через WorldToViewportPoint, см. changelog). Вместо фиксированного
    /// pitch с первого кадра — интерполяция от <see cref="IntroPitchDegrees"/>
    /// (почти top-down, гарантированно видит плитку прямо под камерой) к
    /// <see cref="PitchDegrees"/> по мере того, как камера "нагоняет" штатное
    /// отставание (<see cref="TrailingRowsBehindPlayer"/>) — см.
    /// <see cref="ComputeCurrentPitchDegrees"/>.
    ///
    /// Тот же top-down интро задел ещё один, отдельный (геометрический, не
    /// про угол) конфликт: устоявшийся <see cref="HeightOffset"/>.Z, применённый
    /// с самого первого кадра, физически уводит камеру мимо стартовой плиты
    /// (трейлинг-ряд ещё клампится к 0) — плита уходит за НИЖНИЙ край экрана.
    /// Чинится тем же паттерном интерполяции, что и pitch, только для
    /// Z-компоненты офсета — см. <see cref="IntroHeightOffsetZ"/>.
    ///
    /// <see cref="HeightOffset"/>, <see cref="PitchDegrees"/>,
    /// <see cref="IntroPitchDegrees"/> и <see cref="IntroHeightOffsetZ"/> —
    /// публично изменяемые свойства, не
    /// только конструкторские параметры: раньше все применялись один раз в
    /// конструкторе, и Transform камеры каждый кадр перезаписывался
    /// вычисленным значением — из инспектора/Scene view положение камеры
    /// поменять было нельзя даже в Play Mode, правка терялась на следующем
    /// кадре. Теперь <see cref="TunnelCameraController"/> прокидывает
    /// значения своих полей сюда каждый кадр — правки в инспекторе
    /// применяются вживую, без пересборки Follow/рестарта забега.
    /// </summary>
    public sealed class TunnelCameraFollow : IDisposable
    {
        // Было буквально из legacy/burmolda_demo.html, draw(): 0.045 (cameraRow
        // += (cameraTargetRow-cameraRow)*0.045). Дважды замедлено по прямому
        // запросу владельца продукта (0.045 -> 0.02 -> 0.01) — камера
        // ощущалась слишком резкой/дёрганой, не давала игроку спокойно
        // обдумать маршрут (черновой тюнинг «на глазок», без формального
        // issue — финальное значение задаст плейтест баланса, Спринт 10).
        private const float SmoothingFactor = 0.01f;

        // legacy/burmolda_demo.html, tryAct()/returnToAltar(): cameraTargetRow = Math.max(0, r-5).
        // Публичная — нужна TunnelCameraController (калибровка FOV, см.
        // TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow)
        // и тестам вне этого класса, а не только ComputeTargetPosition/
        // ComputeCurrentPitchDegrees здесь.
        public const int TrailingRowsBehindPlayer = 5;

        // Хотфикс: игровой (устоявшийся) Rotation X — обоснование в
        // doc-комментарии класса. Публичная константа, а не только значение
        // по умолчанию параметра конструктора — TunnelCameraController
        // использует её как значение по умолчанию своего инспекторного поля.
        public const float DefaultPitchDegrees = 50f;

        // Top-down интро на старте забега. 90° исключён (сингулярность взгляда
        // строго по вертикали — forward параллелен "up"). Изначально было 85°
        // (максимально top-down), но проверка реальным WorldToViewportPoint
        // показала побочный эффект: FOV откалиброван под устоявшийся pitch=50°
        // и не годится для геометрии почти-вертикального интро — ближняя
        // (стартовая) плитка на 85° раздувается почти во весь кадр, а ряды
        // впереди уходят за верхний край почти сразу (виден только 0-2 ряд из
        // TunnelGridReveal.RowsAheadOfPlayer=8). 70° — верхняя граница
        // изначально рассматриваемого диапазона (65-70°), решение владельца
        // продукта: заметно более top-down, чем устоявшиеся 50°, но без
        // экстремальной раздутости 85°. См. также TunnelCameraController —
        // FOV теперь пересчитывается каждый кадр от текущего
        // интерполированного pitch, не только от устоявшегося.
        public const float DefaultIntroPitchDegrees = 70f;

        // Хотфикс геометрического конфликта (не про pitch): HeightOffset.Z
        // фиксированный на устоявшемся значении (Z=2) на самом старте забега
        // физически уводит камеру ВПЕРЁД от трейлинг-ряда (который в этот
        // момент клампится к 0 и совпадает с самой стартовой плитой) — камера
        // смотрит вниз-вперёд мимо стартовой плиты, а не на неё, и та уходит
        // за НИЖНИЙ край экрана (проверено WorldToViewportPoint: -0.167 при
        // Z=2 фиксированном). Угол (Pitch/IntroPitch) тут ни при чём — чинить
        // нужно позицию, не поворот. Тот же паттерн интерполяции по
        // caughtUpDistance/TrailingRowsBehindPlayer, что уже есть у pitch
        // (см. ComputeCatchUpScale), применён теперь ещё и к Z-компоненте
        // HeightOffset — см. IntroHeightOffsetZ/ComputeTargetPosition.
        // Значение подобрано численно (не на глаз): при playerRow=0
        // (scale=0), IntroPitchDegrees=70° — эффективный Z=-1 даёт
        // viewport.y стартовой плиты 0.344 (цель была 0.25-0.55).
        public const float DefaultIntroHeightOffsetZ = -1f;

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

        private readonly GridTraceTrail _trail;
        private readonly WorldGridProjection _projection;
        private Vector3 _heightOffset;
        private float _pitchDegrees;
        private float _introPitchDegrees;
        private float _introHeightOffsetZ;
        private float _manualForwardOffsetZ;
        private bool _forceSteadyState;
        private bool _disposed;

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

            TargetPosition = ComputeTargetPosition(_trail.CurrentPosition);
            CurrentPosition = TargetPosition;

            // PositionChanged (а не Advanced) — камера должна следовать за
            // игроком и назад, при возврате на уже пройденную плиту (#61),
            // не только вперёд на новых плитках (иначе некуда отступить).
            _trail.PositionChanged += OnPositionChanged;
        }

        /// <summary>
        /// Смещение камеры над/позади игрока. Изменение немедленно
        /// пересчитывает <see cref="TargetPosition"/> от текущей позиции
        /// трейла — не нужно ждать следующего хода игрока, чтобы увидеть
        /// эффект. Z-компонента до устоявшегося режима не применяется
        /// напрямую — см. <see cref="IntroHeightOffsetZ"/>.
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

        /// <summary>Точка, к которой плавно движется камера — со смещением позади игрока (PRD 16).</summary>
        public Vector3 TargetPosition { get; private set; }

        /// <summary>Текущая сглаженная позиция камеры — присваивать Transform.position.</summary>
        public Vector3 CurrentPosition { get; private set; }

        /// <summary>
        /// Целевой (устоявшийся, игровой) наклон камеры (Rotation X),
        /// градусы — то значение, к которому <see cref="TargetRotation"/>
        /// приходит по мере top-down интро (см. doc-комментарий класса), а
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
        /// камеру мимо стартовой плиты, пока трейлинг-ряд ещё клампится к 0).
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
        /// тому, насколько камера уже "нагнала" штатное отставание
        /// (см. <see cref="ComputeCurrentPitchDegrees"/>). Вычисляется живьём
        /// от текущей позиции трейла при каждом обращении — не кэшируется,
        /// поэтому не нужно отдельно реагировать на изменение
        /// PitchDegrees/IntroPitchDegrees, чтобы результат оставался свежим.
        /// Без поворота влево-вправо/крена (yaw/roll всегда 0).
        /// </summary>
        public Quaternion TargetRotation => Quaternion.Euler(ComputeCurrentPitchDegrees(), 0f, 0f);

        /// <summary>Совпадает с <see cref="TargetRotation"/> — поворот не сглаживается отдельно, интерполяция уже даёт плавность по рядам.</summary>
        public Quaternion CurrentRotation => TargetRotation;

        /// <summary>
        /// То же значение, что и угол в <see cref="TargetRotation"/>/<see cref="CurrentRotation"/>
        /// (<c>eulerAngles.x</c>), но напрямую как число градусов — нужно
        /// <see cref="TunnelCameraController"/>, чтобы калибровать FOV
        /// (<see cref="TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees"/>)
        /// от РЕАЛЬНОГО текущего pitch каждый кадр (включая top-down интро), а
        /// не только от устоявшегося <see cref="PitchDegrees"/> — иначе FOV,
        /// откалиброванный под устоявшуюся геометрию, на интро-pitch (близко к
        /// вертикали) даёт слишком узкий кадр: ближняя плитка раздувается,
        /// дальние ряды уходят за край раньше времени.
        /// </summary>
        public float CurrentPitchDegrees => ComputeCurrentPitchDegrees();

        /// <summary>
        /// Продвигает сглаживание позиции на один тик — константа сглаживания
        /// применяется за вызов, а не масштабируется на deltaTime (как и в
        /// прототипе, где draw() вызывается раз за кадр). Поворот не
        /// участвует — он не по времени, а по рядам (см. <see cref="ComputeCurrentPitchDegrees"/>).
        /// </summary>
        public void Tick()
        {
            CurrentPosition += (TargetPosition - CurrentPosition) * SmoothingFactor;
        }

        /// <summary>
        /// Мгновенно (без сглаживания) показывает текущую позицию трейла у
        /// низа экрана — сбрасывает накопленный <see cref="NudgeForward"/> и
        /// ставит <see cref="CurrentPosition"/> сразу в <see cref="TargetPosition"/>.
        /// Вызывать на каждый новый тап (переход "не прижат"->"прижат", см.
        /// <see cref="GridTraceInputController.PressStarted"/>) — иначе после
        /// долгого свайпа вперёд (см. <see cref="NudgeForward"/>) камера на
        /// СЛЕДУЮЩЕМ тапе стартовала бы с прежнего, уже неактуального
        /// накопленного смещения.
        /// </summary>
        public void SnapToTarget()
        {
            _manualForwardOffsetZ = 0f;
            TargetPosition = ComputeTargetPosition(_trail.CurrentPosition);
            CurrentPosition = TargetPosition;
        }

        /// <summary>
        /// Мгновенно переводит НАКЛОН камеры (<see cref="PitchDegrees"/>) в
        /// устоявшийся (игровой, не top-down интро) режим, минуя интро-
        /// интерполяцию по рядам (см. <see cref="ComputeCurrentPitchDegrees"/>)
        /// — плюс мгновенный снап позиции, как <see cref="SnapToTarget"/>.
        ///
        /// <b>Намеренно НЕ форсирует Z-компоненту HeightOffset</b> (см.
        /// <see cref="ComputeTargetPosition"/>) тем же способом — только
        /// угол. Найдено на реальном устройстве (2026-08-14): на ряду 0
        /// устоявшийся HeightOffset.Z=2 физически уводит камеру мимо
        /// стартовой плиты (ровно тот геометрический конфликт, ради
        /// которого и придуман <see cref="IntroHeightOffsetZ"/>, см. его
        /// doc-комментарий) — плита пропадает за нижним краем экрана прямо
        /// на тапе-"кнопке", который должен был её показать. Z-офсет
        /// по-прежнему плавно интерполируется по рядам как обычно.
        ///
        /// Однонаправленно (для угла) — раз включённый режим не возвращается
        /// к интро-интерполяции угла до следующего забега (новый
        /// <see cref="TunnelCameraFollow"/> на <c>RunStarted</c>). Триггер —
        /// тап по стартовой плите (см. <see cref="GridTraceInputController.StartTileTapped"/>):
        /// отдельная "кнопка" вместо обязательного свайпа, чтобы почувствовать
        /// обычный игровой ракурс камеры сразу, прямой запрос владельца продукта.
        /// </summary>
        public void SnapToSteadyState()
        {
            _forceSteadyState = true;
            SnapToTarget();
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
            TargetPosition = ComputeTargetPosition(coordinate);
        }

        private Vector3 ComputeTargetPosition(GridCoordinate playerPosition)
        {
            // #62: камера двигается только вперёд-назад (по Z) — столбец
            // зафиксирован на центре ширины тоннеля, не следует за игроком
            // по X (иначе камера уезжает влево-вправо при диагональном пути).
            var trailingRow = Math.Max(0, playerPosition.Row - TrailingRowsBehindPlayer);
            var followCoordinate = new GridCoordinate(trailingRow, _projection.Width / 2);

            // Z-компонента HeightOffset интерполируется тем же scale, что и
            // pitch (см. ComputeCatchUpScale/DefaultIntroHeightOffsetZ) — X/Y
            // применяются напрямую, конфликт был только по Z.
            var scale = ComputeCatchUpScale(playerPosition.Row);
            var effectiveOffsetZ = Mathf.Lerp(_introHeightOffsetZ, _heightOffset.z, scale);
            // + _manualForwardOffsetZ — накопленный эдж-скролл (NudgeForward),
            // независимая надбавка поверх обычного трейлинга по рядам.
            var effectiveOffset = new Vector3(_heightOffset.x, _heightOffset.y, effectiveOffsetZ + _manualForwardOffsetZ);

            return _projection.ToWorldPosition(followCoordinate) + effectiveOffset;
        }

        /// <summary>
        /// 0 в самый первый момент забега (трейлинг-ряд камеры клампится к 0,
        /// совпадает с игроком), 1 — как только камера нагоняет штатное
        /// отставание (<see cref="TrailingRowsBehindPlayer"/> рядов). Общий
        /// для интерполяции и pitch (<see cref="ComputeCurrentPitchDegrees"/>),
        /// и Z-компоненты HeightOffset (<see cref="ComputeTargetPosition"/>) —
        /// тот же паттерн, что был у старой убранной LookAheadRowsBeyondPlayer-логики точки взгляда.
        /// </summary>
        private float ComputeCatchUpScale(int playerRow)
        {
            var cameraTrailingRow = Math.Max(0, playerRow - TrailingRowsBehindPlayer);
            var caughtUpDistance = playerRow - cameraTrailingRow;
            return Mathf.Clamp01((float)caughtUpDistance / TrailingRowsBehindPlayer);
        }

        private float ComputeCurrentPitchDegrees()
        {
            // См. SnapToSteadyState — форсирует ТОЛЬКО угол. Специально НЕ
            // форсирует Z-компоненту HeightOffset (см. ComputeTargetPosition)
            // тем же способом: найдено на реальном устройстве — на ряду 0
            // устоявшийся Z=2 уводит камеру мимо стартовой плиты (тот самый
            // геометрический конфликт, ради которого и придуман
            // IntroHeightOffsetZ, см. его doc-комментарий) — плита пропадает
            // за нижним краем экрана. Форсировать нужно только "чувство"
            // обычного игрового ракурса (угол), не саму геометрию кадрирования.
            if (_forceSteadyState) return _pitchDegrees;

            var scale = ComputeCatchUpScale(_trail.CurrentPosition.Row);
            return Mathf.Lerp(_introPitchDegrees, _pitchDegrees, scale);
        }
    }
}
