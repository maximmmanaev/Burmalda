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
        // (TunnelCameraAnchor). По умолчанию ВЫКЛЮЧЕН — выключенное
        // состояние обязано давать побайтово прежнее поведение, см.
        // doc-комментарий Update() ниже.
        [SerializeField] private bool _useScreenAnchor;
        [SerializeField] private float _anchorViewportY = 0.32f;

        private TunnelCameraFollow _follow;

        /// <summary>Тумблер (якорь по вьюпорту) — публично для debug-панели.</summary>
        public bool UseScreenAnchor
        {
            get => _useScreenAnchor;
            set => _useScreenAnchor = value;
        }

        /// <summary>Доля высоты кадра (снизу) для якоря игрока — публично для debug-панели.</summary>
        public float AnchorViewportY
        {
            get => _anchorViewportY;
            set => _anchorViewportY = value;
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
            // Issue #153/#155: перенесено ВЫШЕ Tick() (раньше считалось в
            // конце Update(), после применения позиции) — анкор-производной
            // TrailingDistance ниже нужен ГОТОВЫЙ vFOV этого кадра ДО того,
            // как позиция камеры пересчитается на этом же кадре.
            if (_camera != null)
            {
                // Калибровка ШИРИНЫ (не якоря!) намеренно остаётся на
                // ФИКСИРОВАННОМ TrailingRowsBehindPlayer, а не на
                // анкор-производной TrailingDistance ниже — иначе FOV входил
                // бы в вычисление дистанции-от-якоря, а дистанция-от-якоря
                // обратно в FOV (через vFOV), зацикливая калибровку без
                // явной необходимости (см. doc-комментарий TunnelCameraAnchor).
                var groundDistanceToRow = TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow(
                    _heightOffset.z, _input.Projection.TileSize, TunnelCameraFollow.TrailingRowsBehindPlayer);
                var desiredHorizontalFovDeg = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(
                    _heightOffset.y, groundDistanceToRow, _follow.CurrentPitchDegrees, _input.Projection.TileSize);

                var aspect = (float)Screen.width / Screen.height;
                var vFov = TunnelCameraFraming.ComputeVerticalFovDegrees(desiredHorizontalFovDeg, aspect);
                _camera.fieldOfView = vFov;

                // Тумблер выключен по умолчанию — TrailingDistance тогда
                // НЕ трогается вообще (сеттер даже не вызывается), Follow
                // держит свой дефолт (TrailingRowsBehindPlayer·tileSize) —
                // побайтово прежнее поведение, см. doc-комментарий класса.
                if (_useScreenAnchor)
                {
                    _follow.TrailingDistance = TunnelCameraAnchor.ComputeTrailingDistanceForAnchor(
                        _heightOffset.y, _follow.CurrentPitchDegrees, vFov, _anchorViewportY);
                }
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

            _follow.Tick();

            transform.SetPositionAndRotation(_follow.CurrentPosition, _follow.CurrentRotation);
        }

        private void HandleRunStarted() => RebuildFollow();

        // Новый тап (переход "не прижат"->"прижат") — сбрасывает накопленный
        // эдж-скролл (NudgeForward), БЕЗ мгновенного снапа позиции (2026-08-14,
        // реально сломанный сценарий на устройстве, не гипотеза: снап на
        // каждом новом тапе читался как рывок камеры на несколько клеток —
        // см. doc-комментарии SnapToTarget/ResetManualForwardOffset в
        // TunnelCameraFollow). Обычное продвижение теперь всегда идёт через
        // плавный Tick()/SmoothingFactor, без исключения на новый тап.
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
