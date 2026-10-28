using System;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Movement;

namespace Burmalda.Camp
{
    /// <summary>Результат конвертации добычи в Монеты при эвакуации.</summary>
    public readonly struct ReturnConversionResult
    {
        public ReturnConversionResult(int manaCoins, int keysCoins)
        {
            ManaCoins = manaCoins;
            KeysCoins = keysCoins;
        }

        public int ManaCoins { get; }

        public int KeysCoins { get; }

        public int TotalCoins => ManaCoins + KeysCoins;
    }

    /// <summary>
    /// Эвакуация (PRD v10 §15.2, §15.3): на ЛЮБОМ Алтаре выбор «Вернуться в
    /// Лагерь» — мгновенный переход, без пешего возврата и без чекпоинта Маны
    /// (Алтарь Ману не фиксирует, §11). Мана и Ключи конвертируются в Монеты
    /// (пока линейно, нелинейная конвертация §26 — позже), накопители забега
    /// обнуляются. Окончательная смерть (<see cref="LoseAll"/>) теряет всё
    /// несомое и Монет не даёт.
    ///
    /// Заменяет <c>ReturnJourneySystem</c> (пеший возврат до ряда 0) и
    /// <c>CashOutSystem</c> (чекпоинт на Алтаре) из PRD v9.
    /// </summary>
    public sealed class EvacuationSystem
    {
        /// <summary>Курс Мана→Монеты. ЧЕРНОВОЕ значение [CONFIG], дебаг-панель экономики.</summary>
        public static float ManaToCoinsRate = 20f;

        /// <summary>Курс Ключи→Монеты. ЧЕРНОВОЕ значение [CONFIG] (намеренно невыгодный, §11).</summary>
        public static float KeysToCoinsRate = 1f;

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly RunCurrencyAccumulator _runMana;
        private readonly RunCurrencyAccumulator _runKeys;
        private readonly PersistentWallet _coinWallet;
        private readonly float _coinsOnReturnMultiplier;

        public EvacuationSystem(TunnelGrid grid, GridTraceTrail trail, RunCurrencyAccumulator runMana,
            RunCurrencyAccumulator runKeys, PersistentWallet coinWallet, float coinsOnReturnMultiplier = 1f)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _runMana = runMana ?? throw new ArgumentNullException(nameof(runMana));
            _runKeys = runKeys ?? throw new ArgumentNullException(nameof(runKeys));
            _coinWallet = coinWallet ?? throw new ArgumentNullException(nameof(coinWallet));
            _coinsOnReturnMultiplier = coinsOnReturnMultiplier;
        }

        /// <summary>Игрок уже эвакуировался в этом забеге.</summary>
        public bool HasEvacuated { get; private set; }

        /// <summary>Игрок стоит на Алтаре и ещё не эвакуировался — «Вернуться в Лагерь» доступно.</summary>
        public bool CanEvacuate =>
            !HasEvacuated && _grid.TryGetTile(_trail.CurrentPosition, out var tile) && tile.IsAltar;

        /// <summary>Поднимается при успешной эвакуации, после начисления Монет.</summary>
        public event Action<ReturnConversionResult> Returned;

        /// <summary>
        /// Мгновенная эвакуация. Не-op (false), если игрок не на Алтаре или уже
        /// эвакуировался. Конвертирует Ману и Ключи в Монеты и обнуляет их.
        /// </summary>
        public bool Evacuate()
        {
            if (!CanEvacuate) return false;
            HasEvacuated = true;

            var manaCoins = (int)Math.Round(_runMana.Total / ManaToCoinsRate * _coinsOnReturnMultiplier);
            var keysCoins = (int)Math.Round(_runKeys.Total / KeysToCoinsRate * _coinsOnReturnMultiplier);
            var result = new ReturnConversionResult(manaCoins, keysCoins);

            _runMana.Clear();
            _runKeys.Clear();
            _coinWallet.Deposit(result.TotalCoins);
            Returned?.Invoke(result);
            return true;
        }

        /// <summary>Окончательная смерть (§15.3): теряются вся Мана и Ключи, Монеты не начисляются.</summary>
        public void LoseAll()
        {
            _runMana.Clear();
            _runKeys.Clear();
        }
    }
}
