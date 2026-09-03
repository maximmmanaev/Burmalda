using System;
using Burmalda.D20;
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
    /// о смерти (в т.ч. бросок d20, issue #24) принимает RunState, этот
    /// класс только применяет результат к вводу и собирает
    /// <see cref="D20Trial"/> на реальном кубике (<c>UnityEngine.Random.Range</c>,
    /// не завязан на воспроизводимость — в отличие от seeded-RNG сегментной
    /// генерации, d20-бросок не требует детерминизма). UI экрана смерти/
    /// кнопки рестарта в проекте ещё нет — <see cref="GridTraceInputController.Restart"/>
    /// нужно вызывать вручную (например, из будущей кнопки), как и привязка
    /// камеры к сцене в #8.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;
        [SerializeField] private TrailDecayController _decayController;

        private RunState _runState;

        /// <summary>
        /// Переопределяет источник броска d20 (issue #225) — по умолчанию
        /// null, реальный кубик <c>UnityEngine.Random.Range(1, 21)</c>, как и
        /// раньше (в игре д20 намеренно не детерминирован, см. doc-комментарий
        /// класса). Существует ТОЛЬКО ради тестов, которым нужен
        /// гарантированный исход (например,
        /// <c>IntegrationTests.CoreLoopIntegrationTests</c>: та же схема, что
        /// уже применяется для <see cref="Burmalda.Core.TunnelObstacleGenerator"/>/
        /// <see cref="Burmalda.Altar.AltarTriggerSystem"/> — внедрённый источник
        /// случайности вместо процесс-wide <c>UnityEngine.Random</c>, которое
        /// делает результат теста зависимым от того, сколько раз этот RNG
        /// вызвали ДРУГИЕ тесты, отработавшие раньше в том же прогоне).
        /// Устанавливать ДО первого <see cref="Update"/>/<see cref="HandleRunStarted"/>
        /// (до того, как <see cref="RebuildRunState"/> создаст
        /// <see cref="D20Trial"/>) — на уже собранный <see cref="RunState"/>
        /// не влияет.
        /// </summary>
        public Func<int> RollD20Override { get; set; }

        /// <summary>Причина последней смерти — для будущего UI, пока никак не отображается.</summary>
        public string LastDeathReason { get; private set; }

        /// <summary>Текущий <see cref="RunState"/> — для смежных систем, которым нужно сообщать о смерти напрямую (например, <c>Boss.BossEncounterSystem.ReportBossDefeat</c>). Null до первого забега.</summary>
        public RunState RunState => _runState;

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
            if (_input.Grid == null || _input.Trail == null || _decayController.Decay == null) return;

            var rollD20 = RollD20Override ?? (() => UnityEngine.Random.Range(1, 21)); // Range(1,21) — верхняя граница исключена, даёт 1..20
            var d20 = new D20Trial(rollD20);
            _runState = new RunState(_input.Grid, _input.Trail, _decayController.Decay, d20);
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
