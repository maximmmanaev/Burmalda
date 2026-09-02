using System;
using Burmalda.Core;

namespace Burmalda.BossRoom
{
    /// <summary>
    /// Комната Босса (PRD v8 §8, Спринт 11) — короткий забег на побег:
    /// вход, побег со сбором Жил/Резонансов под наступающей волной, выход.
    /// Один экземпляр — один заход; заново создаётся на каждый вход в
    /// Комнату (по достижении <c>Tile.IsBoss</c>), как и остальные
    /// временные состояния забега (см. <c>RunModifiers.RunModifiers</c>).
    ///
    /// Оркестрирует <see cref="RoomMultiplier"/>/<see cref="BossWave"/>, но
    /// не знает о <c>GridTraceTrail</c>/<c>Tile</c> — вызывающая сторона
    /// (интеграция с забегом, Спринт 11) сообщает о продвижении и тиканье
    /// времени, как <c>Decay.TrailDecaySystem</c> не знает про
    /// <c>Movement.GridTraceInputController</c>.
    /// </summary>
    public sealed class BossRoom
    {
        public BossRoom(int entryRow, int exitRow, float waveRowsPerSecond, int waveStartingMarginRows = BossWave.DefaultStartingMarginRows)
        {
            if (exitRow <= entryRow) throw new ArgumentOutOfRangeException(nameof(exitRow), exitRow, "Выход из Комнаты должен быть дальше входа.");

            EntryRow = entryRow;
            ExitRow = exitRow;
            Multiplier = new RoomMultiplier();
            Wave = new BossWave(entryRow - waveStartingMarginRows, waveRowsPerSecond);
        }

        public int EntryRow { get; }

        /// <summary>Ряд, пересечение которого гасит множитель и завершает Комнату (PRD v8 §8.1, фаза «Выход»).</summary>
        public int ExitRow { get; }

        public RoomMultiplier Multiplier { get; }

        public BossWave Wave { get; }

        /// <summary>Счёт Комнаты — сумма дохода с собранных Жил/тиков Разлома-Жилы, уже с применённым множителем на момент сбора.</summary>
        public int AccumulatedIncome { get; private set; }

        /// <summary>Пересёк порог выхода живым (PRD v8 §8.1, «выход»).</summary>
        public bool HasExited { get; private set; }

        /// <summary>Волна догнала игрока (PRD v8 §8.4, «касание волны = смерть»).</summary>
        public bool HasBeenCaughtByWave { get; private set; }

        /// <summary>Комната ещё активна — не покинута ни через выход, ни через касание волны.</summary>
        public bool IsActive => !HasExited && !HasBeenCaughtByWave;

        /// <summary>Плита-Жила собрана — доход по текущему множителю (PRD v8 §8.2).</summary>
        public void CollectVein(int baseAmount)
        {
            if (!IsActive) return;
            AccumulatedIncome += Multiplier.ApplyToIncome(baseAmount);
        }

        /// <summary>Плита-Резонанс собрана (PRD v8 §8.2).</summary>
        public void CollectResonance()
        {
            if (!IsActive) return;
            Multiplier.ApplyResonanceTile();
        }

        /// <summary>Плита-Эхо собрана — ×2 к множителю (PRD v9 §8.2/8.3).</summary>
        public void CollectEcho()
        {
            if (!IsActive) return;
            Multiplier.ApplyEchoTile();
        }

        /// <summary>Тик Разлома — подтип-Жила даёт доход, подтип-Резонанс даёт множитель, оба по реальному времени (PRD v8 §8.2).</summary>
        public void TickRift(BossRoomRiftSubtype subtype, float deltaSeconds)
        {
            if (!IsActive) return;
            if (subtype == BossRoomRiftSubtype.Resonance)
            {
                Multiplier.TickRiftResonance(deltaSeconds);
            }
            else
            {
                // Подтип-Жила: доход/сек — то же черновое базовое значение,
                // что и обычная плита-Жила за один сбор (PRD не даёт
                // отдельного числа для тиканья секунды), предмет баланса.
                AccumulatedIncome += Multiplier.ApplyToIncome((int)(RoomVeinIncomePerSecond * deltaSeconds));
            }
        }

        // Черновое значение — доход/сек с тикающей Жилы Разлома. PRD не
        // называет число, только "тикает накопление". Предмет баланса
        // (Спринт 18).
        public const int RoomVeinIncomePerSecond = 10;

        /// <summary>Продвигает волну на deltaSeconds реальных секунд (PRD v8 §8.4).</summary>
        public void TickWave(float deltaSeconds)
        {
            if (!IsActive) return;
            Wave.Tick(deltaSeconds);
        }

        /// <summary>
        /// Сообщить текущий ряд игрока — проверяет касание волны и выход.
        /// Волна проверяется первой: если она догнала ровно на ряду выхода,
        /// это смерть, а не спасение впритык (PRD v8 §8.4 — волна не
        /// подкручивается под ситуацию игрока).
        /// </summary>
        public void ReportPlayerRow(int playerRow)
        {
            if (!IsActive) return;

            if (Wave.HasCaught(playerRow))
            {
                HasBeenCaughtByWave = true;
                return;
            }

            if (playerRow >= ExitRow)
                HasExited = true;
        }
    }
}
