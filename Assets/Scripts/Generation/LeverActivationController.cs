using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.Generation
{
    /// <summary>
    /// Пересобирает <see cref="LeverActivationSystem"/> (issue #51) на
    /// каждый забег — по паттерну <see cref="ExplosiveTrapController"/>/
    /// <see cref="TimedTrapController"/> (реагирует на события трейла, не
    /// нужен per-frame Tick). Привязка к тому же GameObject, что и
    /// <see cref="GridTraceInputController"/> — вручную пользователем.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LeverActivationController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;

        private LeverActivationSystem _lever;

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
            DisposeLever();
        }

        private void Update()
        {
            if (_lever == null)
            {
                if (_input == null || _input.Grid == null || _input.Trail == null) return;
                RebuildLever();
            }
        }

        private void HandleRunStarted() => RebuildLever();

        private void RebuildLever()
        {
            DisposeLever();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;
            _lever = new LeverActivationSystem(_input.Grid, _input.Trail);
        }

        private void DisposeLever()
        {
            _lever?.Dispose();
            _lever = null;
        }
    }
}
