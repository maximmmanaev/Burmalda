using System;
using System.Collections.Generic;
using Burmalda.Artifacts;
using Burmalda.Core;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.Altar
{
    /// <summary>
    /// Интеграция Алтаря с забегом (PRD v10 §13: выбор Реликвии, issues #19–21). <see cref="AltarTriggerSystem"/> пересобирается на каждый
    /// забег (нужен свежий <see cref="GridTraceInputController.Trail"/>), а
    /// <see cref="Pool"/>/<see cref="Collection"/> — постоянные, создаются
    /// один раз в <see cref="Awake"/>, как кошельки <see cref="CurrencyController"/>.
    ///
    /// <b>Известный архитектурный долг</b>: постоянное состояние (Пул,
    /// Коллекция, кошельки в <see cref="CurrencyController"/>, будущий
    /// набор Идола/Тотема) пока разбросано по независимым
    /// Controller'ам, а не собрано в единый игровой стейт — сведение в одно
    /// место логично сделать в Лагере (Camp, Спринт 8), где эти данные
    /// впервые понадобятся ВМЕСТЕ на одном экране.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AltarController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;

        private AltarTriggerSystem _altarTrigger;
        private VaultBonusTracker _vaultBonus;
        private AltarPickFlow _pickFlow;

        /// <summary>Что разблокировано к выпадению/покупке — постоянно, переживает рестарт забега.</summary>
        public ArtifactPool Pool { get; private set; }

        /// <summary>История использованных артефактов — постоянно.</summary>
        public ArtifactCollection Collection { get; private set; }

        /// <summary>Срабатывает, когда трейл впервые достигает Алтаря (см. <see cref="AltarTriggerSystem"/>).</summary>
        public event Action<GridCoordinate> AltarReached;

        /// <summary>Билд забега из Реликвий (PRD v10 §13.4) — новый на каждый забег.</summary>
        public RunRelicBuild Build { get; private set; } = new RunRelicBuild();

        /// <summary>Эффекты Реликвий билда (PRD v10 §20.2) — их читают системы игры через <see cref="RunEffects.Current"/>.</summary>
        public RelicEffectState Effects { get; private set; }

        /// <summary>Что Алтарь знает о забеге (Ярус, Афиша, сид); задаёт композиционный корень. Без него — Ярус 1, без Афиши.</summary>
        public IAltarContext Context { get; set; }

        /// <summary>Идёт выбор Реликвии на Алтаре; null, если выбора нет. Экран закрыть нельзя — только взять Реликвию.</summary>
        public AltarPickSession ActiveSession { get; private set; }

        /// <summary>Афиша Алтаря, на котором стоит игрок (PRD v10 §13.3); null вне Алтаря. Сбрасывается первым же шагом прочь.</summary>
        public AltarAfisha ActiveAfisha { get; private set; }

        /// <summary>Афиша появилась или исчезла.</summary>
        public event Action<AltarAfisha> AfishaChanged;

        /// <summary>Открылся выбор «1 из 2 (3)» — показать экран.</summary>
        public event Action<AltarPickSession> PickOffered;

        /// <summary>Реликвия взята — событие аналитики <c>altar_pick</c> (PRD v10 §30).</summary>
        public event Action<AltarPickEvent> PickCompleted;

        /// <summary>Дополнительных вариантов от тайников для следующего Алтаря (маркер в HUD).</summary>
        public int PendingVaultOptions => _vaultBonus != null ? _vaultBonus.PendingExtraOptions : 0;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
            Pool = new ArtifactPool();
            foreach (var id in RelicCatalog.StarterIds) Pool.Unlock(id); // с самого начала открыты три Редкие (§20.1)
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
                RebuildTrigger();
            }
        }

        private void HandleRunStarted() => RebuildTrigger();

        private void RebuildTrigger()
        {
            DisposeTrigger();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;

            Build = new RunRelicBuild();
            Effects = new RelicEffectState(() => Build, () => Time.time, () => Context != null ? Context.CurrentYarus : 1);
            RunEffects.SetLayer("relics", Effects);
            ActiveSession = null;
            _vaultBonus = new VaultBonusTracker(_input.Grid, _input.Trail);
            _pickFlow = new AltarPickFlow(Pool, Build, _vaultBonus, new ContextProxy(this));
            _altarTrigger = new AltarTriggerSystem(_input.Grid, _input.Trail);
            _altarTrigger.AltarReached += OnAltarReached;
            _afishaTrail = _input.Trail;
            _afishaTrail.Advanced += OnTrailAdvanced;
        }

        private GridTraceTrail _afishaTrail;
        private GridCoordinate _afishaAltar;

        private void OnTrailAdvanced(GridCoordinate coordinate)
        {
            if (ActiveAfisha == null || coordinate.Equals(_afishaAltar)) return;
            ActiveAfisha = null; // игрок ушёл с Алтаря
            AfishaChanged?.Invoke(null);
        }

        private void OnAltarReached(GridCoordinate coordinate)
        {
            AltarReached?.Invoke(coordinate);

            _afishaAltar = coordinate;
            ActiveAfisha = _pickFlow?.AfishaFor(coordinate);
            AfishaChanged?.Invoke(ActiveAfisha);

            var session = _pickFlow?.OnAltarReached(coordinate);
            if (session == null) return;
            ActiveSession = session;
            session.Completed += OnPickCompleted;
            PickOffered?.Invoke(session);
        }

        private void OnPickCompleted(AltarPickEvent pick)
        {
            ActiveSession = null;
            Debug.Log(pick.ToString()); // аналитика altar_pick: SDK площадки подключается отдельно (в репозитории его нет)
            PickCompleted?.Invoke(pick);
        }

        // Контекст можно подменить после создания потока (композиционный корень задаёт его позже) — поток читает актуальный.
        private sealed class ContextProxy : IAltarContext
        {
            private readonly AltarController _owner;

            public ContextProxy(AltarController owner) => _owner = owner;

            public bool TryGetSpot(GridCoordinate coordinate, out int yarus, out int ordinal)
            {
                if (_owner.Context != null) return _owner.Context.TryGetSpot(coordinate, out yarus, out ordinal);
                yarus = 1;
                ordinal = 1;
                return false;
            }

            public IReadOnlyCollection<ThreatType> AfishaThreats(int shrineYarus) =>
                _owner.Context != null ? _owner.Context.AfishaThreats(shrineYarus) : Array.Empty<ThreatType>();

            public int RunSeed => _owner.Context != null ? _owner.Context.RunSeed : 0;

            public int CurrentYarus => _owner.Context != null ? _owner.Context.CurrentYarus : 1;
        }

        private void DisposeTrigger()
        {
            if (_altarTrigger == null) return;
            if (_afishaTrail != null) _afishaTrail.Advanced -= OnTrailAdvanced;
            _afishaTrail = null;
            ActiveAfisha = null;
            if (Effects != null && RunEffects.HasLayer("relics", Effects)) RunEffects.SetLayer("relics", null);
            _altarTrigger.AltarReached -= OnAltarReached;
            _altarTrigger.Dispose();
            _altarTrigger = null;
            _vaultBonus?.Dispose();
            _vaultBonus = null;
            _pickFlow = null;
        }
    }
}
