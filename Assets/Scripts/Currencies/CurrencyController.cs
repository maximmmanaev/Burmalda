using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.Currencies
{
    /// <summary>
    /// Интеграция валют с забегом (PRD v9 раздел 5, issues #12–#14, задача
    /// по экономике "Мана как доход забега, Монеты только в Лагере").
    /// Временные (в забеге) накопители — <see cref="RunManaCrystals"/>,
    /// <see cref="RunKeys"/> — пересобираются на каждый
    /// <see cref="GridTraceInputController.RunStarted"/>, как и остальные
    /// run-системы. Постоянный кошелёк — <see cref="Coins"/> — создаётся один
    /// раз в <see cref="Awake"/> и живёт, пока жив этот компонент (переживает
    /// рестарт забега).
    ///
    /// <b>В забеге Монеты больше не копятся и не показываются</b> (v9): в v8
    /// здесь был отдельный <c>RunCoins</c>-накопитель и <c>TrailCoinSystem</c>
    /// — оба удалены. Обычная плита теперь начисляет Ману через
    /// <see cref="TrailManaIncomeSystem"/> (переименованный
    /// <c>TrailCoinSystem</c>, формула не менялась). Монеты появляются
    /// только один раз — конвертацией Маны/Ключей при успешном возврате в
    /// Лагерь (<c>ReturnJourneySystem</c>), в постоянный <see cref="Coins"/>.
    ///
    /// <b>Кристаллы (донат-валюта) удалены из игры полностью</b> (v9,
    /// Блокер 2 задачи по экономике) — постоянного кошелька <c>Crystals</c>
    /// в этом классе больше нет.
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
    /// <b>Нет автоматической фиксации в постоянный кошелёк</b>: "возврат в
    /// лагерь" (Спринт 8, #25/#26) как источник Монет реализован в
    /// <c>ReturnJourneySystem</c>, не здесь — метод
    /// <see cref="PersistentWallet.Deposit"/> публичен специально, чтобы этот
    /// спринт мог вызвать его напрямую, без переделки этого класса.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CurrencyController : MonoBehaviour
    {
        /// <summary>
        /// Кристаллов Маны за одну плиту-источник (порт
        /// <c>runManaCrystals++</c> из прототипа, значение поднято с 1 до
        /// 100 задачей по экономике v9 — источник должен быть на порядок
        /// щедрее обычной плиты (<see cref="TrailManaIncomeSystem.BaseManaPerTile"/> = 10),
        /// иначе крюк к нему не окупается и маршрутное решение "к источнику
        /// или напрямик" исчезает — PRD v9 §4/§5).
        ///
        /// Стартовое значение, уточняется на плейтесте — намеренно НЕ
        /// <c>const</c>, а изменяемое статическое поле: живой рычаг для
        /// debug-панели (<c>DebugVisuals.EconomyDebugPanel</c>). Читается
        /// заново в <see cref="RebuildRunSystems"/> при каждой пересборке
        /// (каждый забег) — изменение применяется со следующего забега, не
        /// внутри уже идущего (тот же грануляр, что у <c>KeysPerSource</c>).
        /// </summary>
        public static int ManaPerSource = 100;

        /// <summary>
        /// Ключей за одну обычную плиту-источник на пути (задача «видимые
        /// рычаги, инвариант лавы, размер награды за Воротами», владелец,
        /// 2026-09-04, закрывает настоящий пробел — раньше нигде не было
        /// задано, тихо стояла заглушка 1). Стартовое значение 15, **не**
        /// <c>const</c> — изменяемое статическое поле, live-рычаг для
        /// debug-панели, уточняется на плейтесте (тот же приём, что
        /// <see cref="ManaPerSource"/>). Ориентир владельца: за забег игрок
        /// должен набирать примерно на одну-две покупки в Алтаре (порядка
        /// 100–150 Ключей при текущей плотности источников в каталоге
        /// сегментов) — не привязано к цене формулой, читай doc-комментарий
        /// <c>Generation.GateVaultPricing</c> о том, почему эта связь ручная,
        /// не автоматическая.
        /// </summary>
        public static int KeysPerSource = 15;

        [SerializeField] private GridTraceInputController _input;

        private TrailMultiplierSystem _multiplier;
        private TrailManaIncomeSystem _manaIncomeSystem;
        private TrailTileCurrencySystem _manaSystem;
        private TrailTileCurrencySystem _keySystem;

        /// <summary>Кристаллы Маны текущего забега — рабочий доход забега (v9): обычные плиты + плиты-источники.</summary>
        public RunCurrencyAccumulator RunManaCrystals { get; private set; }

        /// <summary>Ключи текущего забега.</summary>
        public RunCurrencyAccumulator RunKeys { get; private set; }

        /// <summary>Постоянный баланс Монет — переживает рестарт забега. Пополняется только конвертацией при возврате в Лагерь (v9).</summary>
        public PersistentWallet Coins { get; private set; }

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
            Coins = new PersistentWallet();
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
            if (_manaIncomeSystem == null)
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

            RunManaCrystals = new RunCurrencyAccumulator();
            RunKeys = new RunCurrencyAccumulator();

            // Порядок конструирования критичен — см. докстроку TrailManaIncomeSystem.
            _multiplier = new TrailMultiplierSystem(_input.Trail);
            _manaIncomeSystem = new TrailManaIncomeSystem(_input.Trail, _multiplier, RunManaCrystals);
            _manaSystem = new TrailTileCurrencySystem(_input.Grid, _input.Trail, t => t.IsManaSource, ManaPerSource, RunManaCrystals);
            // Func<Tile,int>-перегрузка, не единый KeysPerSource на все
            // источники — тайник за Воротами (Tile.KeySourceAmount) несёт
            // свою явную сумму, обычные источники на пути падают на общий
            // KeysPerSource (см. её doc-комментарий).
            _keySystem = new TrailTileCurrencySystem(_input.Grid, _input.Trail, t => t.IsKeySource, t => t.KeySourceAmount ?? KeysPerSource, RunKeys);
        }

        private void DisposeRunSystems()
        {
            _keySystem?.Dispose();
            _keySystem = null;
            _manaSystem?.Dispose();
            _manaSystem = null;
            _manaIncomeSystem?.Dispose();
            _manaIncomeSystem = null;
            _multiplier?.Dispose();
            _multiplier = null;
        }
    }
}
