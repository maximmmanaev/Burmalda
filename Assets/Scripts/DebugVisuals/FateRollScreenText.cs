using Burmalda.FateRoll;

namespace Burmalda.DebugVisuals
{
    /// <summary>Подписи экрана Броска Судьбы (PRD v10 §16.1, §31): шансы секторов и что на кону. Чистые функции.</summary>
    public static class FateRollScreenText
    {
        public static string Title(int rollIndex) => $"Бросок Судьбы {RomanNumeral(rollIndex)}";

        public static string Death(FateChances c) => $"Смерть {Percent(c.DeathPercent)}";

        public static string Altar(FateChances c) => $"К Алтарю {Percent(c.AltarPercent)}";

        public static string InPlace(FateChances c) => $"На месте {Percent(c.InPlacePercent)}";

        public static string Stake(int mana, int keys, int sarcophagi) =>
            sarcophagi > 0
                ? $"На кону: Мана {mana}, Ключи {keys}, Саркофагов {sarcophagi}"
                : $"На кону: Мана {mana}, Ключи {keys}";

        public static string Percent(double value) => $"{(int)System.Math.Round(value)}%";

        private static string RomanNumeral(int index) => index switch { 1 => "I", 2 => "II", 3 => "III", _ => index.ToString() };
    }
}
