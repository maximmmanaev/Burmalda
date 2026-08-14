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
    /// не самим классом. Траты (Ключи в Алтаре, Спринт 6) — не в этой задаче,
    /// добавятся отдельным методом позже.
    /// </summary>
    public sealed class RunCurrencyAccumulator
    {
        /// <summary>Накопленное за забег количество.</summary>
        public int Total { get; private set; }

        /// <summary>Срабатывает при каждом успешном пополнении — с новым итогом.</summary>
        public event Action<int> Changed;

        /// <summary>Пополняет накопитель. Неположительные значения игнорируются (защитно — источники пока только начисляют).</summary>
        public void Add(int amount)
        {
            if (amount <= 0) return;
            Total += amount;
            Changed?.Invoke(Total);
        }
    }
}
