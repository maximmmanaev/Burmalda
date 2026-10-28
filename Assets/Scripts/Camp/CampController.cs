using System.Collections.Generic;
using Burmalda.Altar;
using Burmalda.Currencies;
using Burmalda.Decay;
using Burmalda.Movement;
using Burmalda.RunLifecycle;
using Burmalda.Sanctuary;
using UnityEngine;

namespace Burmalda.Camp
{
    /// <summary>
    /// Интеграция Лагеря/Эвакуации с забегом (PRD v10 §15). <see cref="Camp"/> —
    /// постоянный (Awake), переиспользует <see cref="CurrencyController.Coins"/> и
    /// <see cref="AltarController.Pool"/>. <see cref="EvacuationSystem"/> —
    /// пересобирается на каждый забег. Окончательная смерть
    /// (<see cref="RunState.Died"/>) теряет всё несомое; подписка сверяется каждый
    /// кадр (<see cref="SyncRunStateSubscription"/>), потому что
    /// <see cref="RunController"/> пересобирает свой RunState по тому же
    /// <see cref="GridTraceInputController.RunStarted"/>, а порядок между
    /// MonoBehaviour не гарантирован.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;
        [SerializeField] private CurrencyController _currency;
        [SerializeField] private AltarController _altar;
        [SerializeField] private RunController _runController;
        [SerializeField] private SanctuaryController _sanctuary;
        [SerializeField] private TrailDecayController _decayController;

        private Camp _camp;
        private EvacuationSystem _evacuation;
        private RunSarcophagi _carried = new RunSarcophagi();
        private SanctuaryController _subscribedSanctuary;
        private RunState _subscribedRunState;

        /// <summary>Постоянный Лагерь — переживает рестарт забега.</summary>
        public Camp Camp => _camp;

        /// <summary>Саркофаги, несомые в текущем забеге (PRD v10 §15.1) — для HUD и ускорения распада.</summary>
        public RunSarcophagi Carried => _carried;

        /// <summary>Система эвакуации текущего забега — для выбора «Вернуться в Лагерь» на Алтаре.</summary>
        public EvacuationSystem Evacuation => _evacuation;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
            if (_currency == null) _currency = GetComponent<CurrencyController>();
            if (_altar == null) _altar = GetComponent<AltarController>();
            if (_runController == null) _runController = GetComponent<RunController>();
            if (_sanctuary == null) _sanctuary = GetComponent<SanctuaryController>();
            if (_decayController == null) _decayController = GetComponent<TrailDecayController>();
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
            if (_subscribedSanctuary != null) _subscribedSanctuary.SarcophagusTaken -= OnSarcophagusTaken;
            _subscribedSanctuary = null;
        }

        private void Update()
        {
            if (_camp == null && CampIsReady()) _camp = new Camp(_currency.Coins, _altar.Pool);
            if (_evacuation == null && RunSystemsReady()) RebuildRunSystems();
            SyncRunStateSubscription();
            SyncSanctuarySubscription();
            // PRD v10 §7.5: несомые Саркофаги ускоряют распад под игроком. Decay пересобирается на каждый забег — выставляем каждый кадр.
            if (_decayController != null && _decayController.Decay != null) _decayController.Decay.CarriedSarcophagi = _carried.Count;
        }

        private void SyncSanctuarySubscription()
        {
            if (ReferenceEquals(_sanctuary, _subscribedSanctuary)) return;
            if (_subscribedSanctuary != null) _subscribedSanctuary.SarcophagusTaken -= OnSarcophagusTaken;
            _subscribedSanctuary = _sanctuary;
            if (_subscribedSanctuary != null) _subscribedSanctuary.SarcophagusTaken += OnSarcophagusTaken;
        }

        // Ранг бога: Святилище Лешего — младший бог (PRD v10 §5.2); с остальными богами ранг придёт из Святилища.
        private void OnSarcophagusTaken() => _carried.Add(new Sarcophagus(SarcophagusRank.Junior));

        private bool CampIsReady() =>
            _currency != null && _currency.Coins != null &&
            _altar != null && _altar.Pool != null;

        private bool RunSystemsReady() =>
            _input != null && _input.Grid != null && _input.Trail != null &&
            _currency != null && _currency.RunManaCrystals != null && _currency.RunKeys != null && _currency.Coins != null;

        private void HandleRunStarted() => RebuildRunSystems();

        private void RebuildRunSystems()
        {
            DisposeRunSystems();
            if (!RunSystemsReady()) return;

            _carried = new RunSarcophagi(); // новый забег — свои Саркофаги
            _evacuation = new EvacuationSystem(_input.Grid, _input.Trail, _currency.RunManaCrystals, _currency.RunKeys, _currency.Coins,
                _carried, DeliverToCamp);
            _evacuation.Returned += OnEvacuated;
        }

        private void DeliverToCamp(IReadOnlyList<Sarcophagus> sarcophagi) => _camp?.AddUnopened(sarcophagi);

        private void DisposeRunSystems()
        {
            if (_evacuation != null) _evacuation.Returned -= OnEvacuated;
            _evacuation = null;
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

        private void OnDied(string reason) => _evacuation?.LoseAll();

        // Забег закончен: ввод блокируется до «Новая экспедиция» (Restart).
        private void OnEvacuated(ReturnConversionResult result) => _input.MarkDead();
    }
}
