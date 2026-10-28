using System;
using Burmalda.Decay;
using Burmalda.FateRoll;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.RunLifecycle
{
    /// <summary>
    /// Тонкий driver <see cref="RunState"/> (по паттерну TrailDecayController/
    /// TunnelCameraController/TunnelDebugVisualController): создаёт/пересобирает
    /// её по <see cref="GridTraceInputController.RunStarted"/>, при смерти
    /// вызывает <see cref="GridTraceInputController.MarkDead"/>. Пока игрок решает
    /// про Бросок Судьбы (PRD v10 §16) ввод заблокирован; решение и сам бросок
    /// принимает RunState, а этот класс собирает его на реальных кубиках
    /// (<c>UnityEngine.Random.Range</c>) и применяет результат к вводу.
    /// UI экрана смерти/кнопки рестарта — отдельные debug-driver'ы.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;
        [SerializeField] private TrailDecayController _decayController;

        private RunState _runState;

        /// <summary>
        /// Переопределяет источник одного кубика d6 (1..6) — по умолчанию реальный
        /// <c>UnityEngine.Random.Range(1, 7)</c>. Существует ТОЛЬКО ради тестов, которым
        /// нужен гарантированный исход (внедрённый источник случайности вместо
        /// процесс-wide <c>UnityEngine.Random</c>). Устанавливать ДО первого
        /// <see cref="Update"/>/<see cref="HandleRunStarted"/> — на уже собранный
        /// <see cref="RunState"/> не влияет.
        /// </summary>
        public Func<int> RollDieOverride { get; set; }

        /// <summary>Причина последней смерти — для будущего UI, пока никак не отображается.</summary>
        public string LastDeathReason { get; private set; }

        /// <summary>Срабатывает после смерти в забеге (платформа Яндекс Игр размечает конец геймплея).</summary>
        public event Action<string> RunDied;

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

            var rollDie = RollDieOverride ?? (() => UnityEngine.Random.Range(1, 7)); // верхняя граница исключена: 1..6
            _runState = new RunState(_input.Grid, _input.Trail, _decayController.Decay, rollDie);
            _runState.Died += OnDied;
            _runState.FateRollOffered += OnFateRollOffered;
            _runState.FateRolled += OnFateRolled;
        }

        private void OnFateRollOffered(int rollIndex) => _input.MarkDead(); // ввод стоит, пока игрок решает

        private void OnFateRolled(FateRollResult result)
        {
            if (result.Outcome != FateOutcome.Death) _input.ResumeAfterFate();
        }

        private void OnDied(string reason)
        {
            LastDeathReason = reason;
            _input.MarkDead();
            RunDied?.Invoke(reason);
        }

        private void DisposeRunState()
        {
            if (_runState == null) return;
            _runState.Died -= OnDied;
            _runState.FateRollOffered -= OnFateRollOffered;
            _runState.FateRolled -= OnFateRolled;
            _runState.Dispose();
            _runState = null;
        }
    }
}
