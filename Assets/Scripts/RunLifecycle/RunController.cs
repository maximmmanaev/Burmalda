using Burmalda.Decay;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.RunLifecycle
{
    /// <summary>
    /// Тонкий driver <see cref="RunState"/> (по паттерну TrailDecayController/
    /// TunnelCameraController/TunnelDebugVisualController): создаёт/пересобирает
    /// её по <see cref="GridTraceInputController.RunStarted"/>, при смерти
    /// вызывает <see cref="GridTraceInputController.MarkDead"/> — сама решение
    /// о смерти принимает RunState, этот класс только применяет результат к
    /// вводу. UI экрана смерти/кнопки рестарта в проекте ещё нет — <see cref="GridTraceInputController.Restart"/>
    /// нужно вызывать вручную (например, из будущей кнопки), как и привязка
    /// камеры к сцене в #8.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;
        [SerializeField] private TrailDecayController _decayController;

        private RunState _runState;

        /// <summary>Причина последней смерти — для будущего UI, пока никак не отображается.</summary>
        public string LastDeathReason { get; private set; }

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
            if (_decayController == null) _decayController = GetComponent<TrailDecayController>();
        }

        private void OnEnable()
        {
            if (_input != null) _input.RunStarted += HandleRunStarted;
        }

        private void OnDisable()
        {
            if (_input != null) _input.RunStarted -= HandleRunStarted;
            DisposeRunState();
        }

        private void Update()
        {
            // Ленивая инициализация — как в остальных driver'ах: покрывает
            // первый запуск (порядок Awake/OnEnable между компонентами не
            // гарантирован), рестарты приходят через HandleRunStarted.
            if (_runState == null) RebuildRunState();
        }

        private void HandleRunStarted() => RebuildRunState();

        private void RebuildRunState()
        {
            DisposeRunState();

            if (_input == null || _decayController == null) return;
            if (_input.Trail == null || _decayController.Decay == null) return;

            _runState = new RunState(_input.Trail, _decayController.Decay);
            _runState.Died += OnDied;
        }

        private void OnDied(string reason)
        {
            LastDeathReason = reason;
            _input.MarkDead();
        }

        private void DisposeRunState()
        {
            if (_runState == null) return;
            _runState.Died -= OnDied;
            _runState.Dispose();
            _runState = null;
        }
    }
}
