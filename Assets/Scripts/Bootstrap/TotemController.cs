using Burmalda.Artifacts;
using Burmalda.Camp;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Generation;
using Burmalda.Movement;
using Burmalda.RunLifecycle;
using UnityEngine;

namespace Burmalda.Bootstrap
{
    /// <summary>
    /// Тотем в забеге (PRD v10 §17, §19): слой эффектов «totem», раскрытие ловушек Маангом, ход конём Ваабижеши (аим на плиту
    /// на ход коня вне режима цели Идола) и бонус Мигизи к броску Судьбы (<see cref="RunState.FateModifier"/>, шансы на экране
    /// броска считаются с ним). Тотем выбирается в Лагере из собранных.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TotemController : MonoBehaviour
    {
        private GridTraceInputController _input;
        private CurrencyController _currency;
        private CampController _camp;
        private RunController _runController;
        private SegmentGenerationController _segments;
        private GridTraceTrail _builtFor;
        private TotemEffects _effects;
        private TotemRevealSystem _reveal;

        public TotemEffects Effects => _effects;

        private void Awake()
        {
            _input = GetComponent<GridTraceInputController>();
            _currency = GetComponent<CurrencyController>();
            _camp = GetComponent<CampController>();
            _runController = GetComponent<RunController>();
            _segments = GetComponent<SegmentGenerationController>();
        }

        private void OnDisable() => Teardown();

        private void Update()
        {
            if (_input == null || _input.Trail == null || _camp == null || _camp.Camp == null) return;
            if (!ReferenceEquals(_builtFor, _input.Trail)) Rebuild();
            // Мигизи: бонус к сумме броска (§16.1). RunState пересобирается на каждый забег — выставляем каждый кадр.
            var run = _runController != null ? _runController.RunState : null;
            if (run != null && _effects != null) run.FateModifier = _effects.FateBonus;
        }

        private void LateUpdate() => _effects?.CommitStep();

        private void Rebuild()
        {
            Teardown();
            _builtFor = _input.Trail;
            var progress = _camp.Camp.Progress;
            progress.AutoEquipIfEmpty();
            _effects = new TotemEffects(progress, () => _segments != null ? _segments.CurrentYarus : 1);
            RunEffects.SetLayer("totem", _effects);
            _reveal = new TotemRevealSystem(_input.Grid, _input.Trail, _effects, row => _segments?.EnsureCoveredThrough(row));
            _input.AlternativeMove = TryKnightMove;
            _input.Trail.PositionChanged += OnPositionChanged;
        }

        private bool TryKnightMove(GridCoordinate target) =>
            _effects != null && _effects.TryKnightMove(_input.Grid, _input.Trail, target, _currency != null && _currency.Streak != null ? _currency.Streak.Streak : 0);

        private void OnPositionChanged(GridCoordinate coordinate) => _effects?.NoteStep();

        private void Teardown()
        {
            if (_input != null) _input.AlternativeMove = null;
            if (_builtFor != null) _builtFor.PositionChanged -= OnPositionChanged;
            _builtFor = null;
            _reveal?.Dispose();
            _reveal = null;
            if (_effects != null && RunEffects.HasLayer("totem", _effects)) RunEffects.SetLayer("totem", null);
            _effects = null;
        }
    }
}
