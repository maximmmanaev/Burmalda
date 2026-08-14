namespace Burmalda.Altar
{
    /// <summary>
    /// Индикатор энергии до Босса (PRD v7 §7, issue #81): "витрина
    /// показывает «Кристаллов Маны: 3 200 / нужно 5 200». Тот же индикатор
    /// — постоянно в HUD забега". Даёт только цифру дефицита, не маршрут к
    /// ней (PRD §18). Требуемое значение — забота вызывающей стороны: кривая
    /// энергии по Ярусам ещё не существует (Ярусы Глубины — Спринт 8, сам
    /// Босс — Спринт 7), эта функция намеренно не знает, откуда берётся
    /// требуемое число.
    /// </summary>
    public static class ManaToBossIndicator
    {
        /// <summary>Сколько Кристаллов Маны ещё не хватает до порога Босса. 0, если уже достаточно (см. PRD v7 §8.2, Перелив энергии).</summary>
        public static int ComputeDeficit(int currentMana, int requiredMana)
        {
            var deficit = requiredMana - currentMana;
            return deficit > 0 ? deficit : 0;
        }
    }
}
