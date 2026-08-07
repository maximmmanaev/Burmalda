using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Тикает следование камеры каждый кадр и применяет результат к своему
    /// Transform (PRD 16) — камера от третьего лица сверху-сзади за трейлом
    /// того же забега, что и <see cref="GridTraceInputController"/>.
    /// Привязка этого компонента к Main Camera на сцене — вручную пользователем,
    /// здесь только логика следования.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TunnelCameraController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;
        // Новое смещение для 3D-камеры от третьего лица — в 2D-прототипе аналога
        // нет (см. issue #8: скоуп увеличен с 2D top-down до 3D третьего лица).
        [SerializeField] private Vector3 _heightOffset = new Vector3(0f, 4f, -1f);

        private TunnelCameraFollow _follow;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
        }

        private void OnDisable()
        {
            _follow?.Dispose();
            _follow = null;
        }

        private void Update()
        {
            // Ленивая инициализация вместо OnEnable: порядок Awake/OnEnable
            // между разными компонентами не гарантирован (см. TrailDecayController),
            // а Trail появляется только в Awake() GridTraceInputController.
            if (_follow == null)
            {
                if (_input == null || _input.Trail == null) return;
                _follow = new TunnelCameraFollow(_input.Trail, _input.Projection, _heightOffset);
            }

            _follow.Tick();
            transform.SetPositionAndRotation(_follow.CurrentPosition, _follow.CurrentRotation);
        }
    }
}
