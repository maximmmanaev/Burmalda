using System;
using Burmalda.Altar;
using Burmalda.Artifacts;
using Burmalda.Currencies;
using Burmalda.Movement;
using Burmalda.RunLifecycle;
using UnityEngine;

namespace Burmalda.Boss
{
    /// <summary>
    /// Интеграция Босса с забегом (PRD раздел 8, issue #22). Переиспользует
    /// <see cref="AltarController.Pool"/> — Пул артефактов один на всё
    /// прохождение, а не отдельный на каждую систему (см. известный
    /// архитектурный долг в докстроке <see cref="AltarController"/>: сведение
    /// в единый стейт — задача Лагеря, Спринт 8; до тех пор Boss ссылается
    /// на существующий инстанс Altar, а не заводит свой). Причина смерти от
    /// Босса сообщается через лямбду, читающую <see cref="RunController.RunState"/>
    /// заново при каждом вызове (см. докстроку <see cref="BossEncounterSystem"/>) —
    /// не захватывает конкретный инстанс RunState на момент пересборки.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossController : MonoBehaviour
    {
        // Черновая кривая требуемой энергии по рядам — та же заглушка
        // Яруса Глубины, что и SegmentGenerationController.RowsPerDifficultyStep.
        // Предмет баланса (Спринт 10, docs/rules/forbidden-actions.md).
        public const int BaseRequiredEnergy = 5000;
        public const int RowsPerEnergyStep = 40;
        public const int RequiredEnergyStepAmount = 500;

        [SerializeField] private GridTraceInputController _input;
        [SerializeField] private CurrencyController _currency;
        [SerializeField] private AltarController _altar;
        [SerializeField] private RunController _runController;

        private readonly FirstBossVictoryTracker _tracker = new FirstBossVictoryTracker();
        private BossEncounterSystem _bossEncounter;

        /// <summary>Постоянный флаг первой победы за прохождение — переживает рестарт забега.</summary>
        public FirstBossVictoryTracker Tracker => _tracker;

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
            _currency != null && _currency.RunManaCrystals != null && _currency.RunCoins != null &&
            _altar != null && _altar.Pool != null &&
            _runController != null;

        private void HandleRunStarted() => RebuildEncounter();

        private void RebuildEncounter()
        {
            DisposeEncounter();
            if (!IsReady()) return;

            _bossEncounter = new BossEncounterSystem(
                _input.Grid, _input.Trail, _currency.RunManaCrystals, _currency.RunCoins,
                _altar.Pool, _tracker, ReportBossDefeat, RequiredEnergyForRow);
            _bossEncounter.EncounterResolved += OnEncounterResolved;
        }

        // Читает RunController.RunState заново при каждом вызове — не захватывает инстанс на момент пересборки (см. докстроку BossEncounterSystem).
        private void ReportBossDefeat(string reason) => _runController.RunState?.ReportBossDefeat(reason);

        private static int RequiredEnergyForRow(int row) =>
            BaseRequiredEnergy + row / RowsPerEnergyStep * RequiredEnergyStepAmount;

        private void OnEncounterResolved(BossEncounterOutcome outcome, Relic relic) => EncounterResolved?.Invoke(outcome, relic);

        private void DisposeEncounter()
        {
            if (_bossEncounter == null) return;
            _bossEncounter.EncounterResolved -= OnEncounterResolved;
            _bossEncounter.Dispose();
            _bossEncounter = null;
        }
    }
}
