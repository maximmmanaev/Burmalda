namespace Burmalda.Core
{
    /// <summary>
    /// Параметры Ярусов по полосам сложности (PRD v10 §6.1, §7.6, спринт v10-2). Все числа —
    /// ЧЕРНОВЫЕ [CONFIG], финал задаёт плейтест. Номер Яруса и полоса — явные значения, а не
    /// производное от номера ряда.
    /// </summary>
    public static class YarusProgression
    {
        public const int MaxYarus = 10;

        /// <summary>Полоса сложности: Ярусы 1–3 → 1, 4–6 → 2, 7–9 → 3, 10 → 4.</summary>
        public static int DifficultyBand(int yarus)
        {
            var clamped = Clamp(yarus);
            if (clamped <= 3) return 1;
            if (clamped <= 6) return 2;
            return clamped <= 9 ? 3 : 4;
        }

        /// <summary>Длина тоннеля Яруса в рядах, без Алтарей и Святилища [CONFIG, черновик].</summary>
        public static int TunnelRows(int yarus)
        {
            switch (DifficultyBand(yarus))
            {
                case 1: return 90;
                case 2: return 110;
                case 3: return 130;
                default: return 140;
            }
        }

        /// <summary>
        /// Множитель времени жизни пройденной плиты до превращения в Лаву по полосе [CONFIG, черновик]:
        /// чем глубже, тем меньше. Нижняя граница — <see cref="MinTileLifetimeSeconds"/>.
        /// </summary>
        public static float DecayLifetimeMultiplier(int yarus)
        {
            switch (DifficultyBand(yarus))
            {
                case 1: return 0.8f;
                case 2: return 0.7f;
                case 3: return 0.6f;
                default: return 0.55f;
            }
        }

        /// <summary>
        /// «Честное окно» [CONFIG, черновик]: время жизни плиты (сек), ниже которого оно не опускается ни
        /// на какой полосе — игроку всегда хватает времени увидеть и сделать следующий шаг.
        /// </summary>
        public const float MinTileLifetimeSeconds = 1.5f;

        private static int Clamp(int yarus) => yarus < 1 ? 1 : (yarus > MaxYarus ? MaxYarus : yarus);
    }
}
