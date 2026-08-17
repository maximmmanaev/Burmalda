using System;

namespace Burmalda.BossRoom
{
    /// <summary>
    /// Волна преследования Босса (PRD v8 §8.4, Спринт 11): «наступающая
    /// сзади волна, а не противник с очками здоровья... Касание волны =
    /// смерть». Наступает от плиты позади игрока в сторону выхода из
    /// Комнаты (Row растёт) с постоянной скоростью.
    /// </summary>
    public sealed class BossWave
    {
        // Черновое значение — сколько рядов позади игрока волна стартует в
        // момент входа в Комнату. Предмет баланса (Спринт 18), не
        // выводится из PRD — там нет числа, только "наступает сзади".
        public const int DefaultStartingMarginRows = 3;

        public BossWave(int startingRow, float rowsPerSecond)
        {
            if (rowsPerSecond <= 0f) throw new ArgumentOutOfRangeException(nameof(rowsPerSecond), rowsPerSecond, "Скорость волны должна быть положительной.");
            RowPosition = startingRow;
            RowsPerSecond = rowsPerSecond;
        }

        /// <summary>Текущий ряд волны — растёт по ходу Комнаты, может быть дробным между тиками.</summary>
        public float RowPosition { get; private set; }

        /// <summary>
        /// Скорость волны, рядов/сек — постоянна для Яруса/Типа Босса,
        /// объявлена на Афише, не подкручивается под ситуацию игрока
        /// (PRD v8 §8.4, §13.4).
        /// </summary>
        public float RowsPerSecond { get; }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            RowPosition += RowsPerSecond * deltaSeconds;
        }

        /// <summary>Волна достигла или прошла ряд игрока — касание, смерть (PRD v8 §8.4).</summary>
        public bool HasCaught(int playerRow) => RowPosition >= playerRow;
    }
}
