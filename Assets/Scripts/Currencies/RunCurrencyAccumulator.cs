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
    /// Спринт 6: Ключи в Ритуале на Алтаре). Эвакуация и
    /// окончательная смерть (PRD v10 §15) обнуляют накопитель — <see cref="Clear"/>;
    /// чекпоинта на Алтаре нет.
    /// </summary>
    public sealed class RunCurrencyAccumulator
    {
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

        /// <summary>Обнуляет накопленное (эвакуация или окончательная смерть, PRD v10 §15).</summary>
        public void Clear()
        {
            if (Total == 0) return;
            Total = 0;
            Changed?.Invoke(Total);
        }
    }
}
