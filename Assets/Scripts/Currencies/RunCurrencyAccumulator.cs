using System;

namespace Burmalda.Currencies
{
    /// <summary>
    /// Временный (в забеге) накопитель одной валюты (PRD раздел 5) — общая
    /// реализация для Кристаллов Маны, Ключей и Монет текущего забега:
    /// все три структурно одинаковы (растут при сборе, обнуляются с новым
    /// забегом — новый экземпляр создаётся заново, а не сбрасывается).
    /// Конкретная роль (Мана/Ключи/Монеты) задаётся тем, ЧТО именно
    /// прибавляет к нему <see cref="TrailCoinSystem"/>/<see cref="TrailTileCurrencySystem"/>,
    /// не самим классом. Траты — <see cref="Spend"/> (issue #19/#20/#21,
    /// Спринт 6: Ключи в Ритуале на Алтаре). Кэш-аут (PRD раздел 10, issue
    /// #25) — <see cref="Checkpoint"/>/<see cref="RevertToCheckpoint"/>:
    /// "смерть по пути назад = потеря всего, накопленного с последнего
    /// зафиксированного Алтаря/лагеря" — откат, а не обнуление, обычная
    /// (не в пути назад) смерть теряет всё естественно — новый экземпляр
    /// создаётся заново при следующем забеге, откат для нее не нужен.
    /// </summary>
    public sealed class RunCurrencyAccumulator
    {
        private int _checkpoint;

        /// <summary>Накопленное за забег количество.</summary>
        public int Total { get; private set; }

        /// <summary>Срабатывает при каждом успешном изменении (пополнение или трата) — с новым итогом.</summary>
        public event Action<int> Changed;

        /// <summary>Пополняет накопитель. Неположительные значения игнорируются (защитно — источники пока только начисляют).</summary>
        public void Add(int amount)
        {
            if (amount <= 0) return;
            Total += amount;
            Changed?.Invoke(Total);
        }

        /// <summary>Списывает <paramref name="amount"/>, если накоплено достаточно. Возвращает false и не меняет Total иначе (в т.ч. для неположительной суммы) — по аналогии с <c>Currencies.PersistentWallet.Spend</c>.</summary>
        public bool Spend(int amount)
        {
            if (amount <= 0) return false;
            if (amount > Total) return false;

            Total -= amount;
            Changed?.Invoke(Total);
            return true;
        }

        /// <summary>Фиксирует текущий Total как чекпоинт (issue #25 — кэш-аут на Алтаре/в Лагере).</summary>
        public void Checkpoint()
        {
            _checkpoint = Total;
        }

        /// <summary>Откатывает Total к последнему зафиксированному чекпоинту (0, если <see cref="Checkpoint"/> ещё не вызывался в этом забеге).</summary>
        public void RevertToCheckpoint()
        {
            if (Total == _checkpoint) return;
            Total = _checkpoint;
            Changed?.Invoke(Total);
        }
    }
}
