using System;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Movement;

namespace Burmalda.Camp
{
    /// <summary>
    /// Возврат в лагерь (PRD раздел 10, issue #26): "«Вернуться в лагерь» —
    /// не телепорт: игрок прокладывает новый путь назад" — намеренно НЕ
    /// добавляет новую механику движения: <see cref="GridTraceTrail"/> уже
    /// поддерживает шаги в любом направлении (8-соседство, повторный проход
    /// по не разрушенным плитам, #61) — "путь назад" уже возможен тем же
    /// <see cref="GridTraceTrail.TryAdvanceTo"/>. Этот класс добавляет
    /// только СМЫСЛ: намерение "я возвращаюсь" (<see cref="BeginReturn"/>) и
    /// условие успеха — достижение ряда 0 (стартовый ряд забега, "вход в
    /// лагерь"). Декей продолжает тикать как обычно, отдельно от этого класса.
    ///
    /// "Успешный возврат в лагерь фиксирует монеты... навсегда; Кристаллы
    /// Маны и временные артефакты сгорают" — фиксируются только Монеты
    /// (<paramref name="coinWallet"/>): Кристаллы (донат-валюта) не
    /// собираются на карте в текущей модели валют (v6+, PRD раздел 5) —
    /// v4-формулировка про фиксацию Кристаллов при возврате устарела вместе
    /// с переопределением валют, коммитить нечего. Мана/Ключи и временный
    /// билд Амулетов/Талисманов "сгорают" сами собой — новый забег создаёт
    /// свежие экземпляры (см. Currencies.CurrencyController), явного кода
    /// для "сжигания" не требуется.
    ///
    /// "Смерть по пути назад = потеря всего, накопленного с последнего
    /// зафиксированного Алтаря/лагеря" — <see cref="HandleDeathDuringReturn"/>:
    /// откатывает накопители к чекпоинту (<see cref="CashOutSystem"/>).
    /// Обычная смерть НЕ в процессе возврата этот откат не трогает — она и
    /// так теряет всё естественно (новый забег = новые накопители).
    /// </summary>
    public sealed class ReturnJourneySystem : IDisposable
    {
        private readonly GridTraceTrail _trail;
        private readonly RunCurrencyAccumulator _runCoins;
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
        /// (см. Camp.CampController).
        /// </param>
        public ReturnJourneySystem(GridTraceTrail trail, RunCurrencyAccumulator runCoins, RunCurrencyAccumulator runMana,
            RunCurrencyAccumulator runKeys, PersistentWallet coinWallet, float coinsOnReturnMultiplier = 1f)
        {
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _runCoins = runCoins ?? throw new ArgumentNullException(nameof(runCoins));
            _runMana = runMana ?? throw new ArgumentNullException(nameof(runMana));
            _runKeys = runKeys ?? throw new ArgumentNullException(nameof(runKeys));
            _coinWallet = coinWallet ?? throw new ArgumentNullException(nameof(coinWallet));
            _coinsOnReturnMultiplier = coinsOnReturnMultiplier;

            _trail.PositionChanged += OnPositionChanged;
        }

        /// <summary>Игрок сейчас в процессе возврата (нажал "Вернуться в лагерь", ещё не дошёл).</summary>
        public bool IsReturning { get; private set; }

        /// <summary>Срабатывает при успешном возврате — с зафиксированной суммой Монет.</summary>
        public event Action<int> Returned;

        /// <summary>Начинает возврат в лагерь ("Кнопка «Вернуться в лагерь»", PRD раздел 10).</summary>
        public void BeginReturn()
        {
            IsReturning = true;
        }

        /// <summary>Вызывать при смерти игрока, ПОКА <see cref="IsReturning"/> — откатывает накопители к последнему чекпоинту. Не-op, если возврат не был начат.</summary>
        public void HandleDeathDuringReturn()
        {
            if (!IsReturning) return;

            _runCoins.RevertToCheckpoint();
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
            var committed = (int)Math.Round(_runCoins.Total * _coinsOnReturnMultiplier);
            _coinWallet.Deposit(committed);
            Returned?.Invoke(committed);
        }
    }
}
