using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.Currencies
{
    /// <summary>
    /// Интеграция валют с забегом (PRD раздел 5, issues #12–#14). Временные
    /// (в забеге) накопители — <see cref="RunCoins"/>, <see cref="RunManaCrystals"/>,
    /// <see cref="RunKeys"/> — пересобираются на каждый <see cref="GridTraceInputController.RunStarted"/>,
    /// как и остальные run-системы. Постоянные кошельки — <see cref="Coins"/>,
    /// <see cref="Crystals"/> — создаются один раз в <see cref="Awake"/> и
    /// живут, пока жив этот компонент (переживают рестарт забега).
    ///
    /// Строит СОБСТВЕННЫЙ приватный <see cref="TrailMultiplierSystem"/>, а не
    /// использует значение из <see cref="TrailMultiplierController"/> — оба
    /// компонента независимо подписаны на события трейла, и полагаться на
    /// порядок вызова между ДВУМЯ РАЗНЫМИ MonoBehaviour было бы хрупко
    /// (Unity не гарантирует порядок между произвольными компонентами).
    /// Внутри одного класса порядок конструирования полностью под контролем
    /// (см. <see cref="RebuildRunSystems"/>). Оба инстанса детерминированно
    /// вычисляют одно и то же от одного и того же трейла — избыточность
    /// дешёвая и безопасная, взамен на надёжность.
    ///
    /// <b>Нет автоматической фиксации в постоянные кошельки</b>: "возврат в
    /// лагерь" (Спринт 8, #25/#26) как источник Монет и Реликвия/донат
    /// (Спринт 7/11) как источник Кристаллов ещё не реализованы — методы
    /// <see cref="PersistentWallet.Deposit"/> публичны специально, чтобы эти
    /// будущие спринты могли вызвать их напрямую, без переделки этого класса.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CurrencyController : MonoBehaviour
    {
        /// <summary>Кристаллов Маны за одну плиту-источник (порт <c>runManaCrystals++</c> из прототипа).</summary>
        public const int ManaPerSource = 1;

        /// <summary>Ключей за одну плиту-источник — нет прецедента в прототипе (Ключи введены только в PRD v6), черновое значение. Предмет баланса (Спринт 10, docs/rules/forbidden-actions.md).</summary>
        public const int KeysPerSource = 1;

        [SerializeField] private GridTraceInputController _input;

        private TrailMultiplierSystem _multiplier;
        private TrailCoinSystem _coinSystem;
        private TrailTileCurrencySystem _manaSystem;
        private TrailTileCurrencySystem _keySystem;

        /// <summary>Монеты, собранные за текущий забег — ещё не зафиксированы в <see cref="Coins"/>.</summary>
        public RunCurrencyAccumulator RunCoins { get; private set; }

        /// <summary>Кристаллы Маны текущего забега (очки рекорда/множителя, будущий урон боссу — PRD раздел 8).</summary>
        public RunCurrencyAccumulator RunManaCrystals { get; private set; }

        /// <summary>Ключи текущего забега.</summary>
        public RunCurrencyAccumulator RunKeys { get; private set; }

        /// <summary>Постоянный баланс Монет — переживает рестарт забега.</summary>
        public PersistentWallet Coins { get; private set; }

        /// <summary>Постоянный баланс Кристаллов (донат-валюта) — переживает рестарт забега.</summary>
        public PersistentWallet Crystals { get; private set; }

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
            Coins = new PersistentWallet();
            Crystals = new PersistentWallet();
        }

        private void OnEnable()
        {
            if (_input != null) _input.RunStarted += HandleRunStarted;
        }

        private void OnDisable()
        {
            if (_input != null) _input.RunStarted -= HandleRunStarted;
            DisposeRunSystems();
        }

        private void Update()
        {
            if (_coinSystem == null)
            {
                if (_input == null || _input.Grid == null || _input.Trail == null) return;
                RebuildRunSystems();
            }
        }

        private void HandleRunStarted() => RebuildRunSystems();

        private void RebuildRunSystems()
        {
            DisposeRunSystems();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;

            RunCoins = new RunCurrencyAccumulator();
            RunManaCrystals = new RunCurrencyAccumulator();
            RunKeys = new RunCurrencyAccumulator();

            // Порядок конструирования критичен — см. докстроку TrailCoinSystem.
            _multiplier = new TrailMultiplierSystem(_input.Trail);
            _coinSystem = new TrailCoinSystem(_input.Trail, _multiplier, RunCoins);
            _manaSystem = new TrailTileCurrencySystem(_input.Grid, _input.Trail, t => t.IsManaSource, ManaPerSource, RunManaCrystals);
            _keySystem = new TrailTileCurrencySystem(_input.Grid, _input.Trail, t => t.IsKeySource, KeysPerSource, RunKeys);
        }

        private void DisposeRunSystems()
        {
            _keySystem?.Dispose();
            _keySystem = null;
            _manaSystem?.Dispose();
            _manaSystem = null;
            _coinSystem?.Dispose();
            _coinSystem = null;
            _multiplier?.Dispose();
            _multiplier = null;
        }
    }
}
