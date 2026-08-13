using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Тикает и пересобирает <see cref="TimedTrapSystem"/> (issue #45) на
    /// каждый забег — по паттерну <c>Burmalda.Decay.TrailDecayController</c>
    /// (тоже нужен реальный per-frame Tick, а не только реакция на события
    /// трейла, в отличие от <see cref="TunnelObstacleController"/>/
    /// <see cref="ExplosiveTrapController"/>). Отдельный компонент от
    /// <see cref="ExplosiveTrapController"/> — разные issue. Привязка к тому
    /// же GameObject, что и <see cref="GridTraceInputController"/> —
    /// вручную пользователем.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimedTrapController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;

        private TimedTrapSystem _timedTrap;

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
            DisposeTimedTrap();
        }

        private void Update()
        {
            if (_timedTrap == null)
            {
                if (_input == null || _input.Grid == null || _input.Trail == null) return;
                RebuildTimedTrap();
            }

            _timedTrap.Tick(Time.deltaTime);
        }

        private void HandleRunStarted() => RebuildTimedTrap();

        private void RebuildTimedTrap()
        {
            DisposeTimedTrap();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;
            _timedTrap = new TimedTrapSystem(_input.Grid, _input.Trail);
        }

        private void DisposeTimedTrap()
        {
            _timedTrap?.Dispose();
            _timedTrap = null;
        }
    }
}
