using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Тонкий driver debug-визуала тоннеля (issue #58) — создаёт/обновляет
    /// примитивы плит по мере продвижения трейла и хода распада того же
    /// забега, что и <see cref="GridTraceInputController"/>. Debug-инфраструктура
    /// для ручного тестирования, не финальный арт и не система из PRD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TunnelDebugVisualController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;

        private TunnelDebugVisual _visual;

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
            DisposeVisual();
        }

        private void Update()
        {
            // Ленивая инициализация — как в TrailDecayController/TunnelCameraController:
            // порядок Awake/OnEnable между разными компонентами не гарантирован,
            // а Grid/Trail/Projection появляются только в Awake() GridTraceInputController —
            // покрывает первый запуск; рестарты (мир пересоздаётся заново,
            // старые примитивы больше не актуальны) приходят через HandleRunStarted.
            if (_visual == null)
            {
                if (_input == null || _input.Grid == null || _input.Trail == null) return;
                RebuildVisual();
            }

            _visual.Tick();
        }

        private void HandleRunStarted() => RebuildVisual();

        private void RebuildVisual()
        {
            DisposeVisual();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;
            _visual = new TunnelDebugVisual(_input.Grid, _input.Trail, _input.Projection, transform);
        }

        private void DisposeVisual()
        {
            _visual?.Dispose();
            _visual = null;
        }
    }
}
