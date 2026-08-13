using Burmalda.Core;
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
    /// горизонтальный FOV вычисляется один раз при (пере)сборке следования
    /// из реальной геометрии старта забега — см. <see cref="TunnelCameraFraming"/>.
    ///
    /// <see cref="_heightOffset"/>/<see cref="_pitchDegrees"/> — единственный
    /// поддерживаемый способ поменять положение/угол камеры. Просто
    /// подвигать Transform этого объекта в Scene view/инспекторе не выйдет
    /// (даже в Play Mode) — Update() ниже каждый кадр перезаписывает
    /// position/rotation результатом Follow. Оба поля читаются заново каждый
    /// кадр (<see cref="TunnelCameraFollow.HeightOffset"/>/<see cref="TunnelCameraFollow.PitchDegrees"/>
    /// теперь settable), так что правки применяются вживую, без рестарта
    /// забега. Исключение — высота (Y) в <see cref="_heightOffset"/> также
    /// участвует в расчёте горизонтального FOV
    /// (<see cref="ComputeGroundDistanceToFirstRow"/>), а это пересчитывается
    /// только на <see cref="RebuildFollow"/> (старт/рестарт забега) — если
    /// поменяли высоту на лету, ширина обзора тоннеля обновится только на
    /// следующий рестарт.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TunnelCameraController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;
        [SerializeField] private Camera _camera;
        // Новое смещение для 3D-камеры от третьего лица — в 2D-прототипе аналога
        // нет (см. issue #8: скоуп увеличен с 2D top-down до 3D третьего лица).
        [SerializeField] private Vector3 _heightOffset = new Vector3(0f, 4f, -1f);
        // Хотфикс 18.4°/29° -> 35° (см. TunnelCameraFollow) -> откат до 29°
        // по прямому запросу владельца продукта: 35° делало плоскую сетку
        // плит визуально похожей на лестницу (артефакт debug-визуала —
        // между соседними кубиками-плитками есть зазор (TunnelDebugVisual.TileScale),
        // и при более отвесном взгляде сверху видна вертикальная грань
        // кубика в этом зазоре — сами плиты остаются плоскими,
        // WorldGridProjection.ToWorldPosition всегда возвращает Y=0).
        // Компромисс тот же, что и раньше: на 29° небо может быть немного
        // видно (см. историю хотфиксов) — вынесено в инспектор именно
        // поэтому, чтобы дальше подбирать угол вживую (Play Mode), не трогая код.
        [SerializeField] private float _pitchDegrees = 29f;

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
            // без этого правка HeightOffset/Pitch во время Play Mode ничего
            // не меняла бы до следующего RunStarted (см. doc-комментарий класса).
            _follow.HeightOffset = _heightOffset;
            _follow.PitchDegrees = _pitchDegrees;

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
            _follow = new TunnelCameraFollow(_input.Trail, _input.Projection, _heightOffset, _pitchDegrees);

            var groundDistanceToRow = ComputeGroundDistanceToFirstRow();
            _desiredHorizontalFovDeg = TunnelCameraFraming.ComputeDesiredHorizontalFovDegrees(
                _heightOffset.y, groundDistanceToRow, _input.Projection.TileSize);
        }

        /// <summary>
        /// Дистанция от стартовой позиции камеры до первой заспавненной
        /// плиты (ряд 0) — вход для <see cref="TunnelCameraFraming"/>.
        /// Берётся из реально вычисленной позиции камеры/сетки, а не
        /// захардкожена.
        /// </summary>
        private float ComputeGroundDistanceToFirstRow()
        {
            var firstRow = new GridCoordinate(0, _input.Projection.Width / 2);
            var firstRowWorldZ = _input.Projection.ToWorldPosition(firstRow).z;
            return Mathf.Abs(firstRowWorldZ - _follow.TargetPosition.z);
        }

        private void DisposeFollow()
        {
            _follow?.Dispose();
            _follow = null;
        }
    }
}
