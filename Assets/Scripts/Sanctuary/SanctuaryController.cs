using System;
using Burmalda.Currencies;
using Burmalda.Movement;
using Burmalda.Progression;
using Burmalda.RunLifecycle;
using UnityEngine;

namespace Burmalda.Sanctuary
{
    /// <summary>
    /// Тонкий driver Святилища (PRD v10 §14) и Яруса забега: пересобирает
    /// <see cref="SanctuaryRunSystem"/> и <see cref="RunDepthTier"/> на каждый
    /// <see cref="GridTraceInputController.RunStarted"/>, тикает обрушение. Прохождение
    /// Святилища продвигает Ярус (<see cref="RunDepthTier.RecordSanctuaryCleared"/>) и
    /// обновляет рекорд глубины. <see cref="DepthRecord"/> постоянный (переживает забег).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SanctuaryController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;
        [SerializeField] private CurrencyController _currency;
        [SerializeField] private RunController _runController;

        private readonly DepthRecord _depthRecord = new DepthRecord();
        private SanctuaryRunSystem _system;
        private RunDepthTier _depthTier;

        /// <summary>Святилище текущего захода — null вне Святилища.</summary>
        public Sanctuary ActiveSanctuary => _system?.ActiveSanctuary;

        /// <summary>Рекорд глубины — постоянный.</summary>
        public DepthRecord DepthRecord => _depthRecord;

        /// <summary>Ярус текущего забега; null до первой сборки.</summary>
        public RunDepthTier DepthTier => _depthTier;

        /// <summary>Игрок взял Саркофаг в Святилище.</summary>
        public event Action SarcophagusTaken;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
            if (_currency == null) _currency = GetComponent<CurrencyController>();
            if (_runController == null) _runController = GetComponent<RunController>();
        }

        private void OnEnable()
        {
            if (_input != null) _input.RunStarted += HandleRunStarted;
        }

        private void OnDisable()
        {
            if (_input != null) _input.RunStarted -= HandleRunStarted;
            DisposeSystem();
        }

        private void Update()
        {
            if (_system == null) RebuildSystem();
            _system?.Tick(Time.deltaTime);
        }

        private void HandleRunStarted() => RebuildSystem();

        private void RebuildSystem()
        {
            DisposeSystem();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;
            if (_currency == null || _currency.RunManaCrystals == null) return;
            if (_runController == null) return;

            _depthTier = new RunDepthTier();
            _depthTier.Advanced += _depthRecord.ReportTier;
            _system = new SanctuaryRunSystem(_input.Grid, _input.Trail, _currency.RunManaCrystals, ReportCollapseHazard);
            _system.Exited += OnExited;
            _system.SarcophagusTaken += OnSarcophagusTaken;
        }

        // Читает RunController.RunState заново при каждом вызове — не захватывает инстанс на момент пересборки.
        private void ReportCollapseHazard() => _runController.RunState?.ReportSanctuaryCollapse();

        private void OnExited(Sanctuary sanctuary) => _depthTier?.RecordSanctuaryCleared();

        private void OnSarcophagusTaken() => SarcophagusTaken?.Invoke();

        private void DisposeSystem()
        {
            if (_system != null)
            {
                _system.Exited -= OnExited;
                _system.SarcophagusTaken -= OnSarcophagusTaken;
                _system.Dispose();
                _system = null;
            }

            if (_depthTier != null)
            {
                _depthTier.Advanced -= _depthRecord.ReportTier;
                _depthTier = null;
            }
        }
    }
}
