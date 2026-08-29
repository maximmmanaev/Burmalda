using System;
using Burmalda.Altar;
using Burmalda.Artifacts;
using Burmalda.Currencies;
using Burmalda.Movement;
using Burmalda.Progression;
using Burmalda.RunLifecycle;
using UnityEngine;

namespace Burmalda.Boss
{
    /// <summary>
    /// Интеграция Босса с забегом (PRD раздел 8/v7 §22, issues #22, #83).
    /// Переиспользует <see cref="AltarController.Pool"/> — Пул артефактов
    /// один на всё прохождение, а не отдельный на каждую систему (см.
    /// известный архитектурный долг в докстроке <see cref="AltarController"/>).
    /// Причина смерти от Босса сообщается через лямбду, читающую
    /// <see cref="RunController.RunState"/> заново при каждом вызове (см.
    /// докстроку <see cref="BossEncounterSystem"/>) — не захватывает
    /// конкретный инстанс RunState на момент пересборки.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossController : MonoBehaviour
    {
        // Черновая кривая требуемой энергии по Ярусам — предмет баланса
        // (Спринт 10, docs/rules/forbidden-actions.md).
        public const int BaseRequiredEnergy = 5000;
        public const int RequiredEnergyStepPerTier = 1500;

        [SerializeField] private GridTraceInputController _input;
        [SerializeField] private CurrencyController _currency;
        [SerializeField] private AltarController _altar;
        [SerializeField] private RunController _runController;

        private readonly FirstBossVictoryTracker _tracker = new FirstBossVictoryTracker();
        private readonly DepthRecord _depthRecord = new DepthRecord();
        private RunDepthTier _depthTier;
        private BossEncounterSystem _bossEncounter;

        /// <summary>Постоянный флаг первой победы за прохождение — переживает рестарт забега.</summary>
        public FirstBossVictoryTracker Tracker => _tracker;

        /// <summary>Постоянный рекорд глубины (PRD v7 §22) — переживает рестарт забега.</summary>
        public DepthRecord DepthRecord => _depthRecord;

        /// <summary>Ярус Глубины текущего забега.</summary>
        public RunDepthTier DepthTier => _depthTier;

        /// <summary>Срабатывает после разрешения встречи с Боссом.</summary>
        public event Action<BossEncounterOutcome, Relic> EncounterResolved;

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
            DisposeEncounter();
        }

        private void Update()
        {
            if (_bossEncounter == null && IsReady()) RebuildEncounter();
        }

        private bool IsReady() =>
            _input != null && _input.Grid != null && _input.Trail != null &&
            _currency != null && _currency.RunManaCrystals != null &&
            _altar != null && _altar.Pool != null &&
            _runController != null;

        private void HandleRunStarted() => RebuildEncounter();

        private void RebuildEncounter()
        {
            DisposeEncounter();
            if (!IsReady()) return;

            _depthTier = new RunDepthTier();
            _depthTier.Advanced += _depthRecord.ReportTier;

            _bossEncounter = new BossEncounterSystem(
                _input.Grid, _input.Trail, _currency.RunManaCrystals,
                _altar.Pool, _tracker, _depthTier, ReportBossDefeat, RequiredEnergyForTier);
            _bossEncounter.EncounterResolved += OnEncounterResolved;
        }

        // Читает RunController.RunState заново при каждом вызове — не захватывает инстанс на момент пересборки (см. докстроку BossEncounterSystem).
        private void ReportBossDefeat(string reason) => _runController.RunState?.ReportBossDefeat(reason);

        private static int RequiredEnergyForTier(int tier) => BaseRequiredEnergy + tier * RequiredEnergyStepPerTier;

        private void OnEncounterResolved(BossEncounterOutcome outcome, Relic relic) => EncounterResolved?.Invoke(outcome, relic);

        private void DisposeEncounter()
        {
            if (_bossEncounter == null) return;
            _bossEncounter.EncounterResolved -= OnEncounterResolved;
            _bossEncounter.Dispose();
            if (_depthTier != null) _depthTier.Advanced -= _depthRecord.ReportTier;
            _depthTier = null;
            _bossEncounter = null;
        }
    }
}
