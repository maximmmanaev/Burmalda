using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Пересобирает <see cref="ExplosiveTrapArmingSystem"/> (issue #10) на
    /// каждый забег — по паттерну <c>Burmalda.Decay.TrailDecayController</c>/
    /// <see cref="TunnelObstacleController"/>. Отдельный компонент от
    /// <see cref="TunnelObstacleController"/> (тот отвечает за статичные
    /// препятствия, #9) — разные issue, разная периодичность будущих правок.
    /// Привязка к тому же GameObject, что и <see cref="GridTraceInputController"/> —
    /// вручную пользователем.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExplosiveTrapController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;

        private ExplosiveTrapArmingSystem _arming;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
        }

        private void OnEnable()
        {
            if (_input != null) _input.RunStarted += HandleRunStarted;
        }

        private void OnDisable()
        {
            if (_input != null) _input.RunStarted -= HandleRunStarted;
            DisposeArming();
        }

        private void Update()
        {
            // Ленивая инициализация вместо OnEnable — тот же паттерн, что и
            // в соседних контроллерах (порядок Awake/OnEnable между разными
            // компонентами не гарантирован).
            if (_arming == null)
            {
                if (_input == null || _input.Grid == null || _input.Trail == null) return;
                RebuildArming();
            }
        }

        private void HandleRunStarted() => RebuildArming();

        private void RebuildArming()
        {
            DisposeArming();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;
            _arming = new ExplosiveTrapArmingSystem(_input.Grid, _input.Trail);
        }

        private void DisposeArming()
        {
            _arming?.Dispose();
            _arming = null;
        }
    }
}
