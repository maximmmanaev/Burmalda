using System.Collections.Generic;
using System.Linq;
using Burmalda.Artifacts;
using Burmalda.FateRoll;
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
    /// Два источника фидбека:
    /// - Выбор Знамения — <see cref="OmenCardText"/>, вызывается вручную
    ///   через <see cref="ShowOmenChoice"/> (живого Controller'а, выбирающего
    ///   Знамение в Лагере, в проекте ещё нет — см. doc-комментарий
    ///   <see cref="RunModifiers.RunModifiers"/> — честно задокументированный
    ///   пробел, не эта задача).
    /// - Бросок Судьбы — подписка на <see cref="RunState.FateRolled"/> через
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

        [SerializeField] private RunController _runController;

        private RunController _wiredRunController;
        private RunState _wiredRunState;

        private string _message;
        private Color _messageColor = Color.white;
        private float _messageExpiresAtTime;

        private void Update()
        {
            // RunState пересобирается на каждый забег (RunController.RunStarted) —
            // ре-подписка нужна на каждую смену инстанса, не только на смену
            // ссылки на RunController.
            var currentRunState = _runController != null ? _runController.RunState : null;
            if (currentRunState != _wiredRunState)
            {
                if (_wiredRunState != null) _wiredRunState.FateRolled -= HandleFateRolled;
                _wiredRunState = currentRunState;
                if (_wiredRunState != null) _wiredRunState.FateRolled += HandleFateRolled;
            }
        }

        private void OnDisable()
        {
            if (_wiredRunState != null) _wiredRunState.FateRolled -= HandleFateRolled;
            _wiredRunState = null;
        }

        /// <summary>Показать 3 предложенных Знамения — вызывается вручную (см. doc-комментарий класса).</summary>
        public void ShowOmenChoice(IReadOnlyList<OmenId> offered)
        {
            SetMessage(offered != null && offered.Count > 0
                ? "Выбор Знамения:\n" + string.Join("\n", offered.Select(OmenCardText.BuildMessage))
                : "Чистый спуск (Знамений не разблокировано)", Color.white);
        }

        private void HandleFateRolled(FateRollResult result) =>
            SetMessage(FateRollOutcomeText.BuildMessage(result), FateRollOutcomeText.ResolveColor(result.Outcome));

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
