namespace Burmalda.BossRoom
{
    /// <summary>
    /// Живой множитель дохода Комнаты (PRD v8 §8.3, Спринт 11): «Множитель
    /// начинается с ×1 и действует на доход, а не на накопленное — каждая
    /// следующая Жила приносит больше, чем предыдущая». Гасится при выходе
    /// из Комнаты — экземпляр этого класса живёт ровно один заход в
    /// <see cref="BossRoom"/>, новый на каждый заход.
    /// </summary>
    public sealed class RoomMultiplier
    {
        public const float StartingValue = 1f;

        // PRD v8 §8.2: "Резонанс — +0.5 к множителю Комнаты до конца Комнаты".
        public const float ResonanceBonus = 0.5f;

        // PRD v9 §8.2/8.3: "Эхо — ×2 к текущему множителю". Умножение, не
        // сложение — тот самый "второй уровень роста" (§8.3: "сложение
        // копит базу, умножение её удваивает"), ради которого Эхо и
        // существует отдельно от Резонанса.
        public const float EchoMultiplierFactor = 2f;

        // PRD v8 §8.2: "подтип Резонанса даёт +0.1 множителя в секунду".
        public const float RiftResonancePerSecond = 0.1f;

        public float CurrentMultiplier { get; private set; } = StartingValue;

        /// <summary>Плита-Резонанс собрана — мгновенный прирост (PRD v8 §8.2).</summary>
        public void ApplyResonanceTile() => CurrentMultiplier += ResonanceBonus;

        /// <summary>
        /// Плита-Эхо собрана — мгновенное умножение (PRD v9 §8.2/8.3):
        /// "(1 + 0.5 + 0.5) × 2 = ×4" — порядок имеет значение, Резонансы
        /// ДО Эха складываются, потом всё вместе умножается. Взять Эхо
        /// раньше Резонансов — тот же итог только за счёт уже собранного,
        /// "потерять четверть итога" (§8.3) — это порядок, не эта формула.
        /// </summary>
        public void ApplyEchoTile() => CurrentMultiplier *= EchoMultiplierFactor;

        /// <summary>Разлом подтипа Резонанс тикает, пока стоишь на нём (PRD v8 §8.2).</summary>
        public void TickRiftResonance(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            CurrentMultiplier += RiftResonancePerSecond * deltaSeconds;
        }

        /// <summary>Доход с плиты-Жилы (или тика Разлома подтипа Жила) с текущим множителем, округление вниз (простое приведение к int).</summary>
        public int ApplyToIncome(int baseAmount) => (int)(baseAmount * CurrentMultiplier);
    }
}
