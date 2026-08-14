using System;
using Burmalda.Artifacts;
using Burmalda.Currencies;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.Altar
{
    /// <summary>
    /// Интеграция Алтаря/Ритуала с забегом (PRD раздел 7, issues #19–21,
    /// #81). <see cref="AltarTriggerSystem"/> пересобирается на каждый
    /// забег (нужен свежий <see cref="GridTraceInputController.Trail"/>), а
    /// <see cref="Pool"/>/<see cref="Collection"/> — постоянные, создаются
    /// один раз в <see cref="Awake"/>, как кошельки <see cref="CurrencyController"/>.
    ///
    /// <b>Известный архитектурный долг</b>: постоянное состояние (Пул,
    /// Коллекция, кошельки в <see cref="CurrencyController"/>, будущий
    /// <see cref="IdolLoadout"/>) пока разбросано по независимым
    /// Controller'ам, а не собрано в единый игровой стейт — сведение в одно
    /// место логично сделать в Лагере (Camp, Спринт 8), где эти данные
    /// впервые понадобятся ВМЕСТЕ на одном экране.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AltarController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;
        [SerializeField] private CurrencyController _currency;

        private AltarTriggerSystem _altarTrigger;

        /// <summary>Что разблокировано к выпадению/покупке — постоянно, переживает рестарт забега.</summary>
        public ArtifactPool Pool { get; private set; }

        /// <summary>История использованных артефактов — постоянно.</summary>
        public ArtifactCollection Collection { get; private set; }

        /// <summary>Текущий открытый Ритуал (последний, полученный по <see cref="RitualOpened"/>) — null вне Алтаря.</summary>
        public Ritual ActiveRitual { get; private set; }

        /// <summary>Срабатывает, когда трейл достигает Алтаря и Ритуал открывается (см. <see cref="AltarTriggerSystem"/>).</summary>
        public event Action<Ritual> RitualOpened;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
            if (_currency == null) _currency = GetComponent<CurrencyController>();
            Pool = new ArtifactPool();
            Collection = new ArtifactCollection();
        }

        private void OnEnable()
        {
            if (_input != null) _input.RunStarted += HandleRunStarted;
        }

        private void OnDisable()
        {
            if (_input != null) _input.RunStarted -= HandleRunStarted;
            DisposeTrigger();
        }

        private void Update()
        {
            if (_altarTrigger == null)
            {
                if (_input == null || _input.Grid == null || _input.Trail == null) return;
                if (_currency == null || _currency.RunKeys == null) return;
                RebuildTrigger();
            }
        }

        private void HandleRunStarted() => RebuildTrigger();

        private void RebuildTrigger()
        {
            DisposeTrigger();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;
            if (_currency == null || _currency.RunKeys == null) return;

            ActiveRitual = null;
            _altarTrigger = new AltarTriggerSystem(_input.Grid, _input.Trail, _currency.RunKeys, Pool, () => UnityEngine.Random.value);
            _altarTrigger.RitualOpened += OnRitualOpened;
        }

        private void OnRitualOpened(Ritual ritual)
        {
            ActiveRitual = ritual;
            RitualOpened?.Invoke(ritual);
        }

        private void DisposeTrigger()
        {
            if (_altarTrigger == null) return;
            _altarTrigger.RitualOpened -= OnRitualOpened;
            _altarTrigger.Dispose();
            _altarTrigger = null;
        }
    }
}
