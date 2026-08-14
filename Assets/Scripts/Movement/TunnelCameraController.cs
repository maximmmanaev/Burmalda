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
    /// (пере)сборке следования) — от ТЕКУЩЕГО интерполированного pitch
    /// (<see cref="TunnelCameraFollow.CurrentPitchDegrees"/>), не только от
    /// устоявшегося <see cref="_pitchDegrees"/>. Найдено проверкой реальным
    /// WorldToViewportPoint: FOV, откалиброванный один раз под устоявшийся
    /// pitch, ломает геометрию во время top-down интро (сильно другой pitch)
    /// — ближняя (стартовая) плитка раздувается почти во весь кадр, дальние
    /// ряды уходят за верхний край раньше времени. `groundDistanceToRow` от
    /// вырожденного старта забега не берём и здесь — по-прежнему аналитически
    /// от устоявшегося (steady-state) режима, см.
    /// <see cref="TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow"/>
    /// (эта часть от pitch не зависит, только от HeightOffset.Z).
    ///
    /// <see cref="_heightOffset"/>/<see cref="_pitchDegrees"/>/<see cref="_introPitchDegrees"/>/
    /// <see cref="_introHeightOffsetZ"/> — единственный поддерживаемый способ
    /// поменять положение/угол камеры. Просто подвигать Transform этого
    /// объекта в Scene view/инспекторе не выйдет (даже в Play Mode) —
    /// Update() ниже каждый кадр перезаписывает position/rotation результатом
    /// Follow. Все поля читаются заново каждый кадр
    /// (<see cref="TunnelCameraFollow.HeightOffset"/>/<see cref="TunnelCameraFollow.PitchDegrees"/>/
    /// <see cref="TunnelCameraFollow.IntroPitchDegrees"/>/<see cref="TunnelCameraFollow.IntroHeightOffsetZ"/>
    /// теперь settable), так что правки применяются вживую, без рестарта
    /// забега — включая высоту (Y) в <see cref="_heightOffset"/>, теперь тоже
    /// каждый кадр (см. выше). <see cref="_introHeightOffsetZ"/> чинит
    /// отдельный геометрический конфликт (не про угол) — см. TunnelCameraFollow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TunnelCameraController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;
        [SerializeField] private Camera _camera;
        // Новое смещение для 3D-камеры от третьего лица — в 2D-прототипе аналога
        // нет (см. issue #8: скоуп увеличен с 2D top-down до 3D третьего лица).
        // Хотфикс (0,4,-1) -> (0,6,2) по прямому запросу владельца продукта.
        // Была короткая ошибочная попытка синхронизировать этот дефолт с
        // рассинхронизированным значением (0,6,-1), которое залипло в
        // SampleScene.unity ещё до этого хотфикса (Inspector-значение, once
        // сериализовано, не следует за новым инициализатором поля само по
        // себе) — отменено: (0,6,2) провалидирован тестом на «плитка игрока
        // ниже центра» (см. TunnelCameraViewportFramingTests), сцена сама
        // была устаревшей, не эталоном. Сцена приведена в соответствие
        // (SampleScene.unity), не наоборот.
        [SerializeField] private Vector3 _heightOffset = new Vector3(0f, 6f, 2f);
        // Хотфикс 18.4°/29°/35° -> 29° -> 50° (см. TunnelCameraFollow) — по
        // прямому запросу владельца продукта. Связано константой, а не
        // отдельным литералом (в отличие от предыдущего отката до 29°) —
        // тот откат как раз и показал риск ручной рассинхронизации дефолтов
        // в двух местах, больше не повторяем эту ошибку.
        [SerializeField] private float _pitchDegrees = TunnelCameraFollow.DefaultPitchDegrees;
        // Top-down интро на старте забега — см. TunnelCameraFollow.
        [SerializeField] private float _introPitchDegrees = TunnelCameraFollow.DefaultIntroPitchDegrees;
        // Отдельный геометрический фикс (не про угол) — см. TunnelCameraFollow.
        // IntroHeightOffsetZ/DefaultIntroHeightOffsetZ: устоявшийся HeightOffset.Z
        // на самом старте забега уводит камеру мимо стартовой плиты.
        [SerializeField] private float _introHeightOffsetZ = TunnelCameraFollow.DefaultIntroHeightOffsetZ;

        // Эдж-скролл (2026-08-14, прямой запрос владельца продукта): пока
        // палец держится на экране и поднимается к верхней границе нижней
        // трети (normalizedY = screenY/Screen.height >= 1/3), камера едет
        // вперёд сама — водить пальцем нужно только по нижней трети экрана,
        // не тянуться выше. Черновые значения (порог, скорость) — предмет
        // плейтеста баланса (Спринт 10), важен сам факт наличия механики.
        [SerializeField] private float _edgeScrollThresholdNormalizedY = 1f / 3f;
        [SerializeField] private float _edgeScrollSpeed = 3f; // мировых единиц/с

        private TunnelCameraFollow _follow;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
            if (_camera == null) _camera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            if (_input != null)
            {
                _input.RunStarted += HandleRunStarted;
                _input.PressStarted += HandlePressStarted;
                _input.StartTileTapped += HandleStartTileTapped;
            }
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.RunStarted -= HandleRunStarted;
                _input.PressStarted -= HandlePressStarted;
                _input.StartTileTapped -= HandleStartTileTapped;
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

            // Эдж-скролл — см. doc-комментарий полей выше. Палец держится и
            // поднялся к верхней границе нижней трети экрана -> двигаем
            // камеру вперёд напрямую (NudgeForward — без сглаживания Tick(),
            // отклик должен быть мгновенным).
            if (_input.IsPressed)
            {
                var normalizedY = _input.CurrentScreenPosition.y / Screen.height;
                if (normalizedY >= _edgeScrollThresholdNormalizedY)
                    _follow.NudgeForward(_edgeScrollSpeed * Time.deltaTime);
            }

            _follow.Tick();
            transform.SetPositionAndRotation(_follow.CurrentPosition, _follow.CurrentRotation);

            // Пересчитывается каждый кадр, как и aspect ниже — дёшево, и
            // покрывает не только смену разрешения/ориентации, но и
            // top-down интро (текущий pitch меняется кадр к кадру, см.
            // doc-комментарий класса). groundDistanceToRow от pitch не
            // зависит (только от HeightOffset.Z), пересчёт этой части
            // каждый кадр — не необходимость, а просто следствие того, что
            // вся формула теперь считается заново, не кэшируется.
            if (_camera != null)
            {
                var groundDistanceToRow = TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow(
                    _heightOffset.z, _input.Projection.TileSize, TunnelCameraFollow.TrailingRowsBehindPlayer);
                var desiredHorizontalFovDeg = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(
                    _heightOffset.y, groundDistanceToRow, _follow.CurrentPitchDegrees, _input.Projection.TileSize);

                var aspect = (float)Screen.width / Screen.height;
                _camera.fieldOfView = TunnelCameraFraming.ComputeVerticalFovDegrees(desiredHorizontalFovDeg, aspect);
            }
        }

        private void HandleRunStarted() => RebuildFollow();

        // Новый тап (переход "не прижат"->"прижат") — мгновенно показать
        // текущую позицию трейла у низа экрана, см. doc-комментарий SnapToTarget.
        private void HandlePressStarted() => _follow?.SnapToTarget();

        // Тап по стартовой плите-"кнопке" — сразу игровой (не top-down
        // интро) режим камеры, см. doc-комментарий SnapToSteadyState.
        private void HandleStartTileTapped() => _follow?.SnapToSteadyState();

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
