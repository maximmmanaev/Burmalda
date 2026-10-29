using Burmalda.Artifacts;
using Burmalda.Camp;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.Bootstrap
{
    /// <summary>
    /// Идол в забеге (PRD v10 §17, §18.1): собирает на каждый забег заряд (серия чистого прохода), слой эффектов Идола и
    /// систему применения; двойной тап по своей плите (<see cref="GridTraceInputController.DoubleTapOwnTile"/>) активирует
    /// Идола, Рывок/Пробой/Переход ждут цель в режиме выбора (<see cref="GridTraceInputController.TargetingMode"/>).
    /// Идол выбирается в Лагере из собранных; нет Идола — «голый» старт, заряд не копится.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IdolController : MonoBehaviour
    {
        private GridTraceInputController _input;
        private CurrencyController _currency;
        private CampController _camp;
        private GridTraceTrail _builtFor;
        private IdolChargeSystem _charge;
        private IdolRunSystem _run;
        private IdolEffects _effects;

        /// <summary>Заряд текущего забега; null, пока не собран.</summary>
        public IdolChargeSystem Charge => _charge;

        public IdolRunSystem Run => _run;

        public IdolEffects Effects => _effects;

        private void Awake()
        {
            _input = GetComponent<GridTraceInputController>();
            _currency = GetComponent<CurrencyController>();
            _camp = GetComponent<CampController>();
        }

        private void OnDisable() => Teardown();

        private void Update()
        {
            if (_input == null || _input.Trail == null || _currency == null || _currency.Streak == null) return;
            if (_camp == null || _camp.Camp == null) return;
            if (!ReferenceEquals(_builtFor, _input.Trail)) Rebuild();
        }

        private void Rebuild()
        {
            Teardown();
            _builtFor = _input.Trail;
            var progress = _camp.Camp.Progress;
            _effects = new IdolEffects(() => Time.time);
            RunEffects.SetLayer("idol", _effects);
            _charge = new IdolChargeSystem(_currency.Streak, () => progress.ActiveIdolId != null);
            var context = new IdolAbilityContext(_input.Grid, _input.Trail, _effects, () => Time.time);
            _run = new IdolRunSystem(progress, () => _charge.IsFull, () => _charge.TryConsume(), context, IdolAbilities.All());
            _run.StateChanged += OnRunStateChanged;
            _input.DoubleTapOwnTile += OnDoubleTap;
            _input.TargetReleased += OnTargetReleased;
            _input.Trail.PositionChanged += OnPositionChanged;
        }

        private void OnDoubleTap() => _run?.OnDoubleTap();

        private void OnTargetReleased(GridCoordinate aim) => _run?.OnTargetReleased(aim);

        private void OnPositionChanged(GridCoordinate coordinate) => _effects?.NoteStep();

        // Подавление ловушек расходуем в конце кадра: все системы ловушек уже отреагировали на то же перемещение.
        private void LateUpdate() => _effects?.CommitStep();

        private void OnRunStateChanged()
        {
            if (_input != null) _input.TargetingMode = _run != null && _run.IsTargeting;
        }

        private void Teardown()
        {
            if (_input != null)
            {
                _input.DoubleTapOwnTile -= OnDoubleTap;
                _input.TargetReleased -= OnTargetReleased;
                _input.TargetingMode = false;
            }

            if (_builtFor != null) _builtFor.PositionChanged -= OnPositionChanged;
            _builtFor = null;
            _charge?.Dispose();
            _charge = null;
            if (_run != null) _run.StateChanged -= OnRunStateChanged;
            _run = null;
            if (_effects != null && RunEffects.HasLayer("idol", _effects)) RunEffects.SetLayer("idol", null);
            _effects = null;
        }
    }
}
