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
    /// разрешения/ориентации без отдельной подписки на события). Сам
    /// горизонтальный FOV вычисляется один раз при (пере)сборке следования —
    /// не от геометрии старта забега (вырожденный случай, см. историю
    /// багов), а аналитически от устоявшегося (steady-state) режима, см.
    /// <see cref="TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow"/>.
    ///
    /// <see cref="_heightOffset"/>/<see cref="_pitchDegrees"/>/<see cref="_introPitchDegrees"/> —
    /// единственный поддерживаемый способ поменять положение/угол камеры.
    /// Просто подвигать Transform этого объекта в Scene view/инспекторе не
    /// выйдет (даже в Play Mode) — Update() ниже каждый кадр перезаписывает
    /// position/rotation результатом Follow. Все поля читаются заново каждый
    /// кадр (<see cref="TunnelCameraFollow.HeightOffset"/>/<see cref="TunnelCameraFollow.PitchDegrees"/>/
    /// <see cref="TunnelCameraFollow.IntroPitchDegrees"/> теперь settable),
    /// так что правки применяются вживую, без рестарта забега. Исключение —
    /// высота (Y) в <see cref="_heightOffset"/> также участвует в расчёте
    /// горизонтального FOV, а это пересчитывается только на
    /// <see cref="RebuildFollow"/> (старт/рестарт забега) — если поменяли
    /// высоту на лету, ширина обзора тоннеля обновится только на следующий
    /// рестарт.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TunnelCameraController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;
        [SerializeField] private Camera _camera;
        // Новое смещение для 3D-камеры от третьего лица — в 2D-прототипе аналога
        // нет (см. issue #8: скоуп увеличен с 2D top-down до 3D третьего лица).
        // Хотфикс (0,4,-1) -> (0,6,2) по прямому запросу владельца продукта,
        // вместе с подъёмом игрового Pitch Degrees (см. ниже).
        [SerializeField] private Vector3 _heightOffset = new Vector3(0f, 6f, 2f);
        // Хотфикс 18.4°/29°/35° -> 29° -> 50° (см. TunnelCameraFollow) — по
        // прямому запросу владельца продукта. Связано константой, а не
        // отдельным литералом (в отличие от предыдущего отката до 29°) —
        // тот откат как раз и показал риск ручной рассинхронизации дефолтов
        // в двух местах, больше не повторяем эту ошибку.
        [SerializeField] private float _pitchDegrees = TunnelCameraFollow.DefaultPitchDegrees;
        // Top-down интро на старте забега — см. TunnelCameraFollow.
        [SerializeField] private float _introPitchDegrees = TunnelCameraFollow.DefaultIntroPitchDegrees;

        private TunnelCameraFollow _follow;
        private float _desiredHorizontalFovDeg;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
            if (_camera == null) _camera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            if (_input != null) _input.RunStarted += HandleRunStarted;
        }

        private void OnDisable()
        {
            if (_input != null) _input.RunStarted -= HandleRunStarted;
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

            _follow.Tick();
            transform.SetPositionAndRotation(_follow.CurrentPosition, _follow.CurrentRotation);

            // Пересчитывается каждый кадр (не только на старте) — дёшево, и
            // покрывает смену разрешения/ориентации без отдельного события.
            if (_camera != null)
            {
                var aspect = (float)Screen.width / Screen.height;
                _camera.fieldOfView = TunnelCameraFraming.ComputeVerticalFovDegrees(_desiredHorizontalFovDeg, aspect);
            }
        }

        private void HandleRunStarted() => RebuildFollow();

        private void RebuildFollow()
        {
            DisposeFollow();
            if (_input == null || _input.Trail == null) return;
            _follow = new TunnelCameraFollow(_input.Trail, _input.Projection, _heightOffset, _pitchDegrees, _introPitchDegrees);

            // Калибровка от устоявшегося (steady-state) режима, не от
            // вырожденного старта забега (см. doc-комментарий класса и
            // TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow) —
            // не требует реального трейла, чистая функция от констант.
            var groundDistanceToRow = TunnelCameraFraming.ComputeSteadyStateGroundDistanceToReferenceRow(
                _heightOffset.z, _input.Projection.TileSize, TunnelCameraFollow.TrailingRowsBehindPlayer);
            // _pitchDegrees (устоявшийся игровой pitch), не текущий
            // интерполированный — ширина обзора калибруется под то, как
            // выглядит игра большую часть времени (после top-down интро),
            // не под сам момент интро.
            _desiredHorizontalFovDeg = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(
                _heightOffset.y, groundDistanceToRow, _pitchDegrees, _input.Projection.TileSize);
        }

        private void DisposeFollow()
        {
            _follow?.Dispose();
            _follow = null;
        }
    }
}
