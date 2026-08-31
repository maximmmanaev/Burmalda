using System;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Movement;

namespace Burmalda.Camp
{
    /// <summary>
    /// Разбивка Монет, зачисленных при успешном возврате в Лагерь
    /// (PRD v9 раздел 5/10) — отдельно от Маны и отдельно от Ключей, чтобы
    /// вызывающая сторона (результаты забега, HUD) могла показать обе
    /// строки конвертации ("Мана × курс = Монеты", "Ключи × курс = Монеты"),
    /// не пересчитывая формулу самостоятельно.
    /// </summary>
    public readonly struct ReturnConversionResult
    {
        public ReturnConversionResult(int manaCoins, int keysCoins)
        {
            ManaCoins = manaCoins;
            KeysCoins = keysCoins;
        }

        /// <summary>Монеты, полученные конвертацией накопленной Маны.</summary>
        public int ManaCoins { get; }

        /// <summary>Монеты, полученные конвертацией оставшихся Ключей.</summary>
        public int KeysCoins { get; }

        /// <summary>Итоговая сумма Монет, зачисленная в постоянный кошелёк.</summary>
        public int TotalCoins => ManaCoins + KeysCoins;
    }

    /// <summary>
    /// Возврат в лагерь (PRD v9 раздел 10, issue #26): "«Вернуться в лагерь» —
    /// не телепорт: игрок прокладывает новый путь назад" — намеренно НЕ
    /// добавляет новую механику движения: <see cref="GridTraceTrail"/> уже
    /// поддерживает шаги в любом направлении (8-соседство, повторный проход
    /// по не разрушенным плитам, #61) — "путь назад" уже возможен тем же
    /// <see cref="GridTraceTrail.TryAdvanceTo"/>. Этот класс добавляет
    /// только СМЫСЛ: намерение "я возвращаюсь" (<see cref="BeginReturn"/>) и
    /// условие успеха — достижение ряда 0 (стартовый ряд забега, "вход в
    /// лагерь"). Декей продолжает тикать как обычно, отдельно от этого класса.
    ///
    /// <b>Единственная точка конвертации в игре</b> (v9, задача по экономике
    /// "Мана как доход забега, Монеты только в Лагере"): в забеге Монет не
    /// существует — при успешном возврате накопленная Мана и оставшиеся
    /// Ключи конвертируются в Монеты по курсу (<see cref="ManaToCoinsRate"/>,
    /// <see cref="KeysToCoinsRate"/>) и зачисляются в <paramref name="coinWallet"/> одной
    /// суммой (<see cref="ReturnConversionResult.TotalCoins"/>). Курсы —
    /// стартовые значения, уточняются на плейтесте: курс Ключей должен
    /// оставаться заметно менее выгодным, чем их покупательная способность
    /// в Алтаре — иначе копить Ключи вместо трат становится осмысленной
    /// стратегией и убивает петлю Ритуала (PRD v9 §4).
    ///
    /// Кэш-аут на Алтаре (<see cref="CashOutSystem"/>) фиксирует Ману/Ключи
    /// как чекпоинт прогресса, но НЕ конвертирует их в Монеты — это делает
    /// только этот класс, и только при успешном достижении Лагеря.
    ///
    /// "Смерть по пути назад = потеря всего, накопленного с последнего
    /// зафиксированного Алтаря/лагеря" — <see cref="HandleDeathDuringReturn"/>:
    /// откатывает накопители к чекпоинту (<see cref="CashOutSystem"/>). При
    /// смерти конвертации в Монеты не происходит вовсе — действуют обычные
    /// правила d20 (PRD раздел 9). Обычная смерть НЕ в процессе возврата
    /// этот откат не трогает — она и так теряет всё естественно (новый
    /// забег = новые накопители).
    /// </summary>
    public sealed class ReturnJourneySystem : IDisposable
    {
        /// <summary>
        /// Курс «Мана → Монеты» при возврате: делится на это число (20 Маны
        /// за 1 Монету). Стартовое значение, уточняется на плейтесте —
        /// намеренно НЕ <c>const</c>, а изменяемое статическое поле: живой
        /// рычаг для debug-панели (<c>DebugVisuals.EconomyDebugPanel</c>),
        /// читается напрямую в <see cref="OnPositionChanged"/>, не
        /// захватывается в конструкторе — правки применяются мгновенно, даже
        /// посреди уже идущего возврата. Выведен из ритма забега (PRD v9
        /// §5): забег на 2–3 минуты даёт ≈2400 Маны ≈ 120 Монет — 4–5
        /// успешных забегов на один заметный апгрейд Лагеря (ориентир 500
        /// Монет).
        /// </summary>
        public static float ManaToCoinsRate = 20f;

        /// <summary>
        /// Курс «Ключи → Монеты» при возврате: делится на это число (1 Ключ
        /// за 1 Монету). Стартовое значение, уточняется на плейтесте, тот же
        /// принцип, что <see cref="ManaToCoinsRate"/>. Намеренно
        /// символический — против апгрейда за 500 Монет остаток Ключей не
        /// даёт ничего заметного, копить их вместо трат в Алтаре бессмысленно
        /// (PRD v9 §4/§5).
        /// </summary>
        public static float KeysToCoinsRate = 1f;

        private readonly GridTraceTrail _trail;
        private readonly RunCurrencyAccumulator _runMana;
        private readonly RunCurrencyAccumulator _runKeys;
        private readonly PersistentWallet _coinWallet;
        private readonly float _coinsOnReturnMultiplier;
        private bool _disposed;

        /// <param name="coinsOnReturnMultiplier">
        /// Множитель зафиксированных Монет (PRD v7 §20, Знамение «Голодный
        /// Босс»: "Монеты ×2 при возврате в Лагерь"). По умолчанию 1
        /// (нейтрально) — Camp.asmdef намеренно не получает зависимость от
        /// RunModifiers, значение читает и передаёт вызывающая сторона
        /// (см. Camp.CampController). В отличие от <see cref="ManaToCoinsRate"/>/
        /// <see cref="KeysToCoinsRate"/> — это НЕ глобальная балансная
        /// константа, а модификатор конкретного забега, поэтому остаётся
        /// параметром конструктора, а не статическим полем.
        /// </param>
        public ReturnJourneySystem(GridTraceTrail trail, RunCurrencyAccumulator runMana, RunCurrencyAccumulator runKeys,
            PersistentWallet coinWallet, float coinsOnReturnMultiplier = 1f)
        {
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _runMana = runMana ?? throw new ArgumentNullException(nameof(runMana));
            _runKeys = runKeys ?? throw new ArgumentNullException(nameof(runKeys));
            _coinWallet = coinWallet ?? throw new ArgumentNullException(nameof(coinWallet));
            _coinsOnReturnMultiplier = coinsOnReturnMultiplier;

            _trail.PositionChanged += OnPositionChanged;
        }

        /// <summary>Игрок сейчас в процессе возврата (нажал "Вернуться в лагерь", ещё не дошёл).</summary>
        public bool IsReturning { get; private set; }

        /// <summary>Срабатывает при успешном возврате — с разбивкой зачисленных Монет.</summary>
        public event Action<ReturnConversionResult> Returned;

        /// <summary>Начинает возврат в лагерь ("Кнопка «Вернуться в лагерь»", PRD раздел 10).</summary>
        public void BeginReturn()
        {
            IsReturning = true;
        }

        /// <summary>Вызывать при смерти игрока, ПОКА <see cref="IsReturning"/> — откатывает накопители к последнему чекпоинту. Конвертации в Монеты при этом не происходит. Не-op, если возврат не был начат.</summary>
        public void HandleDeathDuringReturn()
        {
            if (!IsReturning) return;

            _runMana.RevertToCheckpoint();
            _runKeys.RevertToCheckpoint();
            IsReturning = false;
        }

        /// <summary>Отписывается от трейла. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.PositionChanged -= OnPositionChanged;
            _disposed = true;
        }

        private void OnPositionChanged(GridCoordinate coordinate)
        {
            if (!IsReturning) return;
            if (coordinate.Row > 0) return;

            IsReturning = false;

            var manaCoins = (int)Math.Round(_runMana.Total / ManaToCoinsRate * _coinsOnReturnMultiplier);
            var keysCoins = (int)Math.Round(_runKeys.Total / KeysToCoinsRate * _coinsOnReturnMultiplier);
            var result = new ReturnConversionResult(manaCoins, keysCoins);

            _coinWallet.Deposit(result.TotalCoins);
            Returned?.Invoke(result);
        }
    }
}
