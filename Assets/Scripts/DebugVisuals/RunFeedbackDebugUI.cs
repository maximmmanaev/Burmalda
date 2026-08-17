using System.Collections.Generic;
using System.Linq;
using Burmalda.Artifacts;
using Burmalda.Boss;
using Burmalda.D20;
using Burmalda.RunLifecycle;
using Burmalda.RunModifiers;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Issue #110 (PRD §19): дебаг-визуал UX-фидбека — уровень исполнения
    /// как у <see cref="TunnelDebugVisual"/> (примитивы/текст/цвет, не
    /// финальный арт), реализован через <c>OnGUI</c> (не требует Canvas/
    /// .prefab — в проекте пока нигде нет UI-слоя, см. другие системы).
    ///
    /// Три источника фидбека:
    /// - Выбор Знамения — <see cref="OmenCardText"/>, вызывается вручную
    ///   через <see cref="ShowOmenChoice"/> (живого Controller'а, выбирающего
    ///   Знамение в Лагере, в проекте ещё нет — см. doc-комментарий
    ///   <see cref="RunModifiers.RunModifiers"/> — честно задокументированный
    ///   пробел, не эта задача).
    /// - Победа/поражение Босса — подписка на <see cref="BossController.EncounterResolved"/>
    ///   (тот же паттерн, что читает Ярус из <see cref="BossController.DepthTier"/>),
    ///   если <see cref="_bossController"/> задан в инспекторе.
    /// - d20-бросок — подписка на <see cref="RunState.D20Resolved"/> через
    ///   <see cref="RunController"/>, если задан.
    ///
    /// Намеренно НЕ подключено ни к одной .unity-сцене — привязка к
    /// объекту на сцене (как и у остальных Controller'ов) вручную, вне
    /// скоупа автономного агента (docs/rules/forbidden-actions.md).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunFeedbackDebugUI : MonoBehaviour
    {
        private const float MessageDurationSeconds = 3f;

        [SerializeField] private BossController _bossController;
        [SerializeField] private RunController _runController;

        private BossController _wiredBossController;
        private RunController _wiredRunController;
        private RunState _wiredRunState;

        private string _message;
        private Color _messageColor = Color.white;
        private float _messageExpiresAtTime;

        private void Update()
        {
            if (_bossController != _wiredBossController)
            {
                if (_wiredBossController != null) _wiredBossController.EncounterResolved -= HandleBossEncounterResolved;
                _wiredBossController = _bossController;
                if (_wiredBossController != null) _wiredBossController.EncounterResolved += HandleBossEncounterResolved;
            }

            // RunState пересобирается на каждый забег (RunController.RunStarted) —
            // ре-подписка нужна на каждую смену инстанса, не только на смену
            // ссылки на RunController (тот же паттерн, что и BossController
            // с _depthTier.Advanced).
            var currentRunState = _runController != null ? _runController.RunState : null;
            if (currentRunState != _wiredRunState)
            {
                if (_wiredRunState != null) _wiredRunState.D20Resolved -= HandleD20Resolved;
                _wiredRunState = currentRunState;
                if (_wiredRunState != null) _wiredRunState.D20Resolved += HandleD20Resolved;
            }
        }

        private void OnDisable()
        {
            if (_wiredBossController != null) _wiredBossController.EncounterResolved -= HandleBossEncounterResolved;
            if (_wiredRunState != null) _wiredRunState.D20Resolved -= HandleD20Resolved;
            _wiredBossController = null;
            _wiredRunState = null;
        }

        /// <summary>Показать 3 предложенных Знамения — вызывается вручную (см. doc-комментарий класса).</summary>
        public void ShowOmenChoice(IReadOnlyList<OmenId> offered)
        {
            SetMessage(offered != null && offered.Count > 0
                ? "Выбор Знамения:\n" + string.Join("\n", offered.Select(OmenCardText.BuildMessage))
                : "Чистый спуск (Знамений не разблокировано)", Color.white);
        }

        private void HandleBossEncounterResolved(BossEncounterOutcome outcome, Relic relic)
        {
            var tier = _bossController != null && _bossController.DepthTier != null ? _bossController.DepthTier.CurrentTier : 0;
            SetMessage(
                BossVictoryDebugText.BuildMessage(outcome, relic, tier),
                outcome != null && outcome.IsVictory ? Color.white : D20OutcomeDebugText.DeathColor);
        }

        private void HandleD20Resolved(D20Outcome outcome) =>
            SetMessage(D20OutcomeDebugText.BuildMessage(outcome), D20OutcomeDebugText.ResolveColor(outcome));

        private void SetMessage(string message, Color color)
        {
            _message = message;
            _messageColor = color;
            _messageExpiresAtTime = Time.time + MessageDurationSeconds;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_message) || Time.time >= _messageExpiresAtTime) return;

            var style = new GUIStyle(GUI.skin.box) { fontSize = 28, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            var previousColor = GUI.color;
            GUI.color = _messageColor;
            GUI.Box(new Rect(20, 80, Screen.width - 40, 200), _message, style);
            GUI.color = previousColor;
        }
    }
}
