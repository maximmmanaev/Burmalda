using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Тикает следование камеры каждый кадр и применяет результат к своему
    /// Transform (PRD 16) — камера от третьего лица сверху-сзади за трейлом
    /// того же забега, что и <see cref="GridTraceInputController"/>.
    /// Привязка этого компонента к Main Camera на сцене — вручную пользователем,
    /// здесь только логика следования.
    ///
    /// Хотфикс: держит устойчивую ширину обзора тоннеля
    /// (<see cref="TunnelCameraFraming.DesiredVisibleTiles"/> плиток) на любом
    /// аспекте экрана — пересчитывает Camera.fieldOfView (вертикальный) из
    /// желаемого горизонтального FOV каждый кадр (дёшево, покрывает смену
    /// разрешения/ориентации без отдельной подписки на события).
    ///
    /// Горизонтальный FOV тоже пересчитывается КАЖДЫЙ кадр (не только на
    /// (пере)сборке следования) — от ТЕКУЩЕГО pitch (<see cref="TunnelCameraFollow.CurrentPitchDegrees"/>),
    /// не только от устоявшегося <see cref="_pitchDegrees"/> — иначе FOV,
    /// откалиброванный один раз под устоявшийся pitch, ломает геометрию во
    /// время top-down интро/твина (сильно другой pitch) — ближняя (стартовая)
    /// плитка раздувается почти во весь кадр, дальние ряды уходят за верхний
    /// край раньше времени. `groundDistanceToRow` от вырожденного старта
    /// забега не берём и здесь — по-прежнему аналитически от устоявшегося
    /// (steady-state) режима, см.
    /// <see cref="TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow"/>
    /// (эта часть от pitch не зависит, только от HeightOffset.Z).
    ///
    /// <see cref="_heightOffset"/>/<see cref="_pitchDegrees"/>/<see cref="_introPitchDegrees"/>/
    /// <see cref="_introHeightOffsetZ"/> — единственный поддерживаемый способ
    /// поменять положение/угол камеры. Просто подвигать Transform этого
    /// объекта в Scene view/инспекторе не выйдет (даже в Play Mode) —
    /// Update() ниже каждый кадр перезаписывает position/rotation результатом
    /// Follow. Все поля читаются заново каждый кадр, так что правки
    /// применяются вживую, без рестарта забега.
    ///
    /// <b>Архитектура интро (2026-08-14, прямой запрос владельца продукта):</b>
    /// row-based интерполяция интро -> устоявшийся режим убрана целиком (см.
    /// doc-комментарий <see cref="TunnelCameraFollow"/>) — вместо неё явный
    /// тап-триггер (<see cref="GridTraceInputController.RunConfirmed"/>,
    /// тап по собственной текущей позиции игрока) запускает короткий
    /// time-based твин, который этот класс продвигает каждый кадр через
    /// <see cref="TunnelCameraFollow.AdvanceIntroTween"/>.
    ///
    /// <b>Заморозка на время примеривания (issue #157, перенос схемы A):</b>
    /// пока <see cref="GridTraceInputController.IsPressed"/> истинно,
    /// <see cref="Update"/> не трогает камеру вообще — часть механики ввода
    /// (PRD 4.1: "камера обновляется только между шагами"), не оптимизация.
    ///
    /// <b>Дефолт камеры/ввода, задача 1 (владелец продукта принял запись с
    /// устройства):</b> якорь по вьюпорту + непрерывное следование с
    /// компенсацией скорости + жёсткий/мягкий кламп (issue #153/#155/#158)
    /// — единственный механизм устоявшегося следования, включён ВСЕГДА, без
    /// тумблера-переключателя на старый экспоненциально-сглаженный путь
    /// (тот путь удалён из <see cref="TunnelCameraFollow"/> целиком — см. её
    /// doc-комментарий). <see cref="AnchorViewportY"/>/<see cref="ToleranceViewportFraction"/>/
    /// <see cref="SoftBoundaryStartFraction"/> остаются настраиваемыми из
    /// debug-панели (<c>DebugVisuals.CameraAnchorDebugPanel</c>) — меняются
    /// только их дефолты (32%/12%/60%), не сам факт применения.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TunnelCameraController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;
        [SerializeField] private Camera _camera;
        // Новое смещение для 3D-камеры от третьего лица — в 2D-прототипе аналога
        // нет (см. issue #8: скоуп увеличен с 2D top-down до 3D третьего лица).
        // Хотфикс (0,4,-1) -> (0,6,2) по прямому запросу владельца продукта.
        //
        // z: 2 -> 0 (2026-08-14): TrailingRowsBehindPlayer тихо поменяли 5->3
        // отдельной правкой без пересчёта этого z — дистанция камера-игрок
        // (TrailingRowsBehindPlayer*tileSize - z) просела с исходных 3 до 1,
        // плитка прижалась к нижнему краю кадра (row=0 при playerRow=0:
        // viewport.y 0.324 -> 0.065, реально измерено через WorldToViewportPoint,
        // не на глаз). z=0 восстанавливает исходную дистанцию 3 (5-2 = 3-0) —
        // не своё отдельное значение, а компенсация чужого рассинхрона.
        // Row=0 и устоявшееся состояние теперь буквально одно и то же число
        // (см. TunnelCameraFollow.ComputeTargetPosition — без клампа
        // трейлинг-ряда к 0 дистанция камера-игрок не зависит от Row) —
        // TunnelCameraViewportFramingTests это же подтверждает.
        [SerializeField] private Vector3 _heightOffset = new Vector3(0f, 6f, 0f);
        // Хотфикс 18.4°/29°/35° -> 29° -> 50° (см. TunnelCameraFollow) — по
        // прямому запросу владельца продукта.
        [SerializeField] private float _pitchDegrees = TunnelCameraFollow.DefaultPitchDegrees;
        // Top-down интро на старте забега — см. TunnelCameraFollow.
        [SerializeField] private float _introPitchDegrees = TunnelCameraFollow.DefaultIntroPitchDegrees;
        // Отдельный геометрический фикс (не про угол) — см. TunnelCameraFollow.
        // IntroHeightOffsetZ/DefaultIntroHeightOffsetZ: устоявшийся HeightOffset.Z
        // на самом старте забега уводит камеру мимо стартовой плиты.
        [SerializeField] private float _introHeightOffsetZ = TunnelCameraFollow.DefaultIntroHeightOffsetZ;

        // Issue #153/#155: якорь по вьюпорту вместо TrailingRowsBehindPlayer
        // (TunnelCameraAnchor) + непрерывное следование с компенсацией
        // скорости (TunnelCameraFollow.AdvanceContinuousAnchorFollow) + жёсткий
        // кламп доли экрана в двусторонней полосе [anchor-backTolerance,
        // anchor+tolerance] (задача 1, переформулировка issue A.3). Задача 1
        // (владелец продукта принял на записи с устройства) сделала это
        // единственным механизмом устоявшегося следования — тумблер-
        // переключатель на старый экспоненциально-сглаженный путь убран
        // вместе с самим этим путём (см. doc-комментарий TunnelCameraFollow),
        // значения ниже — просто настройки, не предмет вкл/выкл.
        //
        // 32% -> 46% (задача 1, подтверждено владельцем продукта на
        // устройстве вместе с двусторонней полосой ниже).
        [SerializeField] private float _anchorViewportY = 0.46f;
        // issue A.3: верхняя граница клампа — anchor+tolerance. Настраиваемая,
        // как и anchor (issue #155 явно просит обе).
        [SerializeField] private float _toleranceViewportFraction = 0.12f;
        // Задача 1: нижняя граница клампа СНИЗУ от anchor — anchor-backTolerance.
        // Раньше нижней границей был сам anchor (плитка не могла опуститься
        // ниже — защита от ухода из-под пальца при протяжке в старой схеме
        // ввода), в схеме A (issue #157) палец между шагами поднимается,
        // защита не нужна — см. doc-комментарий TunnelCameraFollow.AdvanceContinuousAnchorFollow.
        [SerializeField] private float _backToleranceViewportFraction = 0.10f;

        // Issue #158: точка начала нарастания мягкой границы, доля расстояния
        // от anchor до КАЖДОГО края полосы (задача 1: теперь применяется к
        // обеим сторонам, не только к верхней) — единственный настраиваемый
        // параметр самой кривой (см. TunnelCameraSoftBoundary).
        [SerializeField] private float _softBoundaryStartFraction = TunnelCameraSoftBoundary.DefaultStartFraction;

        private TunnelCameraFollow _follow;

        /// <summary>Доля высоты кадра (снизу) для якоря игрока — публично для debug-панели.</summary>
        public float AnchorViewportY
        {
            get => _anchorViewportY;
            set => _anchorViewportY = value;
        }

        /// <summary>Допуск СВЕРХУ над anchor для жёсткого клампа (issue A.3) — публично для debug-панели.</summary>
        public float ToleranceViewportFraction
        {
            get => _toleranceViewportFraction;
            set => _toleranceViewportFraction = value;
        }

        /// <summary>Допуск СНИЗУ от anchor для жёсткого клампа (задача 1, issue A.3) — публично для debug-панели.</summary>
        public float BackToleranceViewportFraction
        {
            get => _backToleranceViewportFraction;
            set => _backToleranceViewportFraction = value;
        }

        /// <summary>Точка начала нарастания мягкой границы (issue #158), доля расстояния от anchor до каждого края полосы — публично для debug-панели.</summary>
        public float SoftBoundaryStartFraction
        {
            get => _softBoundaryStartFraction;
            set => _softBoundaryStartFraction = value;
        }

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
            if (_camera == null) _camera = GetComponent<Camera>();

            // 2026-08-18 (диагностика, не гипотеза): SampleScene.unity уже
            // сериализует _introPitchDegrees=85/_introHeightOffsetZ=0 на этом
            // компоненте — расходится с документированными в коде
            // DefaultIntroPitchDegrees=70/DefaultIntroHeightOffsetZ (когда-то
            // -1, тюнится сейчас). Сериализованное значение побеждает
            // C#-дефолт поля при загрузке сцены — правка константы в коде
            // сама по себе не меняла поведение билда, пока эти два поля не
            // перезатёрты явно здесь. .unity трогать нельзя
            // (forbidden-actions.md), поэтому код — источник истины.
            _introPitchDegrees = TunnelCameraFollow.DefaultIntroPitchDegrees;
            _introHeightOffsetZ = TunnelCameraFollow.DefaultIntroHeightOffsetZ;
        }

        private void OnEnable()
        {
            if (_input != null)
            {
                _input.RunStarted += HandleRunStarted;
                _input.PressStarted += HandlePressStarted;
                _input.RunConfirmed += HandleRunConfirmed;
            }
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.RunStarted -= HandleRunStarted;
                _input.PressStarted -= HandlePressStarted;
                _input.RunConfirmed -= HandleRunConfirmed;
            }
            DisposeFollow();
        }

        private void Update()
        {
            // Ленивая инициализация вместо OnEnable: порядок Awake/OnEnable
            // между разными компонентами не гарантирован (см. TrailDecayController),
            // а Trail появляется только в Awake() GridTraceInputController —
            // покрывает первый запуск; рестарты приходят через HandleRunStarted.
            if (_follow == null)
            {
                if (_input == null || _input.Trail == null) return;
                RebuildFollow();
            }

            // Issue #157 (перенос схемы A в продукт): камера обновляется
            // ТОЛЬКО между шагами — часть механики ("плита под пальцем
            // гарантированно та, на которую игрок смотрит", PRD 4.1), не
            // оптимизация. Пока палец на экране (примеривание ИЛИ ожидание
            // лока сразу после нажатия — GridTraceInputController.IsPressed
            // отражает сырое состояние пальца, см. её doc-комментарий), эта
            // функция не трогает ни FOV, ни позицию/поворот, ни твин интро —
            // полный ранний выход, ничего не читается и не пишется дальше.
            if (_input != null && _input.IsPressed) return;

            // Прокидываем текущие значения инспектора в Follow каждый кадр —
            // без этого правка HeightOffset/Pitch/IntroPitch во время Play
            // Mode ничего не меняла бы до следующего RunStarted (см.
            // doc-комментарий класса).
            _follow.HeightOffset = _heightOffset;
            _follow.PitchDegrees = _pitchDegrees;
            _follow.IntroPitchDegrees = _introPitchDegrees;
            _follow.IntroHeightOffsetZ = _introHeightOffsetZ;

            // Пересчитывается каждый кадр — покрывает не только смену
            // разрешения/ориентации, но и ход твина интро (текущий pitch
            // меняется кадр к кадру, см. doc-комментарий класса).
            // groundDistanceToRow от pitch не зависит (только от
            // HeightOffset.Z), пересчёт этой части каждый кадр — не
            // необходимость, а просто следствие того, что вся формула
            // теперь считается заново, не кэшируется.
            //
            // Issue #153/#155: перенесено ВЫШЕ AdvanceContinuousAnchorFollow
            // (раньше считалось в конце Update(), после применения позиции) —
            // анкор-производным дистанциям ниже нужен ГОТОВЫЙ vFOV этого
            // кадра ДО того, как позиция камеры пересчитается на этом же кадре.
            var targetTrailingDistance = 0f;
            var minTrailingDistance = 0f;
            var maxTrailingDistance = 0f;
            if (_camera != null)
            {
                // Калибровка ШИРИНЫ (не якоря!) намеренно остаётся на
                // ФИКСИРОВАННОМ TrailingRowsBehindPlayer, а не на
                // анкор-производных дистанциях ниже — иначе FOV входил бы в
                // их вычисление, а они обратно в FOV (через vFOV), зацикливая
                // калибровку без явной необходимости (см. doc-комментарий
                // TunnelCameraAnchor).
                var groundDistanceToRow = TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow(
                    _heightOffset.z, _input.Projection.TileSize, TunnelCameraFollow.TrailingRowsBehindPlayer);
                var desiredHorizontalFovDeg = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(
                    _heightOffset.y, groundDistanceToRow, _follow.CurrentPitchDegrees, _input.Projection.TileSize);

                var aspect = (float)Screen.width / Screen.height;
                var vFov = TunnelCameraFraming.ComputeVerticalFovDegrees(desiredHorizontalFovDeg, aspect);
                _camera.fieldOfView = vFov;

                // Задача 1: точка покоя (anchor) — цель, к которой стремится
                // непрерывное следование, СТРОГО ВНУТРИ двусторонней полосы
                // клампа (уже не сама нижняя граница, см. doc-комментарий
                // TunnelCameraFollow.AdvanceContinuousAnchorFollow).
                targetTrailingDistance = TunnelCameraAnchor.ComputeTrailingDistanceForAnchor(
                    _heightOffset.y, _follow.CurrentPitchDegrees, vFov, _anchorViewportY);
                minTrailingDistance = TunnelCameraAnchor.ComputeTrailingDistanceForAnchor(
                    _heightOffset.y, _follow.CurrentPitchDegrees, vFov, _anchorViewportY - _backToleranceViewportFraction);
                maxTrailingDistance = TunnelCameraAnchor.ComputeTrailingDistanceForAnchor(
                    _heightOffset.y, _follow.CurrentPitchDegrees, vFov, _anchorViewportY + _toleranceViewportFraction);

                // Lerp по IntroTweenProgress01 — ЗАЩИЩАЕТ top-down интро от
                // этой правки (реально сломанный сценарий, найден на
                // устройстве при съёмке видео для issue #153: без Lerp'а
                // анкор-геометрия для устоявшегося Pitch применялась и во
                // время интро, на сильно другом Pitch — камеру уводило мимо
                // стартовой плиты). Питается TrailingDistance, которая
                // управляет геометрией во время интро (AdvanceIntroTween
                // читает её через ComputeTargetPosition) — после интро её
                // тут же перезаписывает AdvanceContinuousAnchorFollow.
                var fixedTrailingDistance = TunnelCameraFollow.TrailingRowsBehindPlayer * _input.Projection.TileSize;
                _follow.TrailingDistance = Mathf.Lerp(fixedTrailingDistance, targetTrailingDistance, _follow.IntroTweenProgress01);
            }

            // Твин интро -> устоявшийся режим (см. doc-комментарий класса) —
            // не-op, пока ConfirmRun не вызван, и не-op после того, как твин
            // уже отыграл целиком.
            _follow.AdvanceIntroTween(Time.deltaTime);

            // Эдж-скролл УБРАН (2026-08-18, прямой запрос владельца продукта —
            // отменяет решение от 2026-08-14): двигал камеру от самого факта
            // тапа в нижней трети экрана, даже мимо сетки — читалось как баг
            // ("камера двигается непонятно от чего") и объясняло второй
            // репортнутый баг ("замедление через несколько секунд" —
            // TunnelCameraFollow.MaxManualForwardOffset=4 при скорости 3
            // ед/с достигался за ~1.3с непрерывного удержания, дальше
            // NudgeForward переставал что-либо добавлять). Камера теперь
            // двигается СТРОГО от продвижения GridTraceTrail (см.
            // TunnelCameraFollow.OnPositionChanged), ничего больше.
            // NudgeForward/ResetManualForwardOffset/MaxManualForwardOffset в
            // TunnelCameraFollow не удалены — не вызываются отсюда, история/
            // потенциальный будущий возврат, как и SnapToTarget.

            // Пока top-down интро ещё идёт, AdvanceIntroTween выше уже
            // хард-синкает CurrentPosition=TargetPosition каждый кадр —
            // ничего больше делать не нужно. Как только твин отыграл целиком,
            // непрерывное следование с компенсацией скорости + жёсткий/мягкий
            // кламп (см. doc-комментарий AdvanceContinuousAnchorFollow)
            // подхватывает CurrentPosition — единственный путь устоявшегося
            // следования (задача 1, старый альтернативный путь удалён).
            if (_follow.IntroTweenProgress01 >= 1f)
                _follow.AdvanceContinuousAnchorFollow(Time.deltaTime, targetTrailingDistance, minTrailingDistance, maxTrailingDistance, _softBoundaryStartFraction);

            transform.SetPositionAndRotation(_follow.CurrentPosition, _follow.CurrentRotation);
        }

        private void HandleRunStarted() => RebuildFollow();

        // Новый тап (переход "не прижат"->"прижат") — сбрасывает накопленный
        // эдж-скролл (NudgeForward), БЕЗ мгновенного снапа позиции (2026-08-14,
        // реально сломанный сценарий на устройстве, не гипотеза: снап на
        // каждом новом тапе читался как рывок камеры на несколько клеток —
        // см. doc-комментарии SnapToTarget/ResetManualForwardOffset в
        // TunnelCameraFollow). Обычное продвижение теперь всегда идёт через
        // плавный AdvanceContinuousAnchorFollow, без исключения на новый тап.
        private void HandlePressStarted() => _follow?.ResetManualForwardOffset();

        // Тап по собственной текущей позиции игрока ("кнопка") — запускает
        // твин в устоявшийся игровой режим, см. doc-комментарий ConfirmRun.
        private void HandleRunConfirmed() => _follow?.ConfirmRun();

        private void RebuildFollow()
        {
            DisposeFollow();
            if (_input == null || _input.Trail == null) return;
            _follow = new TunnelCameraFollow(_input.Trail, _input.Projection, _heightOffset, _pitchDegrees, _introPitchDegrees, _introHeightOffsetZ);
        }

        private void DisposeFollow()
        {
            _follow?.Dispose();
            _follow = null;
        }
    }
}
