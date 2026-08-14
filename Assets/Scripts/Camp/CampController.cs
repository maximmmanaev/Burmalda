using Burmalda.Altar;
using Burmalda.Currencies;
using Burmalda.Movement;
using Burmalda.RunLifecycle;
using UnityEngine;

namespace Burmalda.Camp
{
    /// <summary>
    /// Интеграция Лагеря/Возврата/Кэш-аута с забегом (PRD разделы 10/11,
    /// issues #25–27). <see cref="Camp"/> — постоянный (Awake), переиспользует
    /// <see cref="CurrencyController.Coins"/>/<see cref="CurrencyController.Crystals"/>
    /// и <see cref="AltarController.Pool"/> вместо своих инстансов (тот же
    /// принцип, что у <c>Boss.BossController</c>). <see cref="CashOutSystem"/>/
    /// <see cref="ReturnJourneySystem"/> — пересобираются на каждый забег.
    ///
    /// Подписка <see cref="ReturnJourneySystem.HandleDeathDuringReturn"/> на
    /// <see cref="RunState.Died"/> сверяется каждый кадр
    /// (<see cref="SyncRunStateSubscription"/>), а не подписывается один раз
    /// при пересборке — <see cref="RunController"/> пересобирает свой
    /// RunState по тому же <see cref="GridTraceInputController.RunStarted"/>,
    /// и порядок между разными MonoBehaviour не гарантирован (тот же риск,
    /// что уже решался колбэком в <c>Boss.BossEncounterSystem</c>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;
        [SerializeField] private CurrencyController _currency;
        [SerializeField] private AltarController _altar;
        [SerializeField] private RunController _runController;

        private Camp _camp;
        private CashOutSystem _cashOut;
        private ReturnJourneySystem _journey;
        private RunState _subscribedRunState;

        /// <summary>Постоянный Лагерь — переживает рестарт забега.</summary>
        public Camp Camp => _camp;

        /// <summary>Система возврата текущего забега — для будущей кнопки "Вернуться в лагерь".</summary>
        public ReturnJourneySystem Journey => _journey;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
            if (_currency == null) _currency = GetComponent<CurrencyController>();
            if (_altar == null) _altar = GetComponent<AltarController>();
            if (_runController == null) _runController = GetComponent<RunController>();
        }

        private void OnEnable()
        {
            if (_input != null) _input.RunStarted += HandleRunStarted;
        }

        private void OnDisable()
        {
            if (_input != null) _input.RunStarted -= HandleRunStarted;
            DisposeRunSystems();
            UnsubscribeFromRunState();
        }

        private void Update()
        {
            if (_camp == null && CampIsReady()) _camp = new Camp(_currency.Coins, _currency.Crystals, _altar.Pool);
            if (_cashOut == null && RunSystemsReady()) RebuildRunSystems();
            SyncRunStateSubscription();
        }

        private bool CampIsReady() =>
            _currency != null && _currency.Coins != null && _currency.Crystals != null &&
            _altar != null && _altar.Pool != null;

        private bool RunSystemsReady() =>
            _input != null && _input.Grid != null && _input.Trail != null &&
            _currency != null && _currency.RunCoins != null && _currency.RunManaCrystals != null && _currency.RunKeys != null && _currency.Coins != null;

        private void HandleRunStarted() => RebuildRunSystems();

        private void RebuildRunSystems()
        {
            DisposeRunSystems();
            if (!RunSystemsReady()) return;

            _cashOut = new CashOutSystem(_input.Grid, _input.Trail, _currency.RunCoins, _currency.RunManaCrystals, _currency.RunKeys);
            _journey = new ReturnJourneySystem(_input.Trail, _currency.RunCoins, _currency.RunManaCrystals, _currency.RunKeys, _currency.Coins);
        }

        private void DisposeRunSystems()
        {
            _cashOut?.Dispose();
            _cashOut = null;
            _journey?.Dispose();
            _journey = null;
        }

        private void SyncRunStateSubscription()
        {
            var current = _runController != null ? _runController.RunState : null;
            if (ReferenceEquals(current, _subscribedRunState)) return;

            UnsubscribeFromRunState();
            _subscribedRunState = current;
            if (_subscribedRunState != null) _subscribedRunState.Died += OnDied;
        }

        private void UnsubscribeFromRunState()
        {
            if (_subscribedRunState == null) return;
            _subscribedRunState.Died -= OnDied;
            _subscribedRunState = null;
        }

        private void OnDied(string reason) => _journey?.HandleDeathDuringReturn();
    }
}
