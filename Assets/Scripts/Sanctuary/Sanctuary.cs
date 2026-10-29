using System;
using Burmalda.Core;

namespace Burmalda.Sanctuary
{
    /// <summary>
    /// Святилище бога (PRD v10 §14): авторский участок в конце Яруса — бег по
    /// обрушающемуся полу к Саркофагу и к Алтарю на выходе. Состояние одного захода:
    /// множитель (старт ×1, гаснет на выходе), накопленная Мана, фронт обрушения,
    /// взят ли Саркофаг. Обрушаются ряды <c>EntryRow+1 … ExitRow-1</c>; вход и Алтарь
    /// на выходе не обрушаются.
    /// </summary>
    public sealed class Sanctuary
    {
        /// <summary>ЧЕРНОВОЕ значение [CONFIG] (§7.6: давление 20–40 с на всё Святилище).</summary>
        public static float DefaultCollapseRowsPerSecond = 0.45f;

        /// <summary>ЧЕРНОВОЕ значение [CONFIG] (§14.2: взятие Саркофага ускоряет обрушение).</summary>
        public static float SarcophagusCollapseMultiplier = 1.6f;

        /// <summary>ЧЕРНОВОЕ значение [CONFIG]: Мана в секунду с Разлома-Жилы до множителя.</summary>
        public const int RiftVeinIncomePerSecond = 10;

        public Sanctuary(int entryRow, int exitRow, float collapseRowsPerSecond)
        {
            if (exitRow <= entryRow) throw new ArgumentOutOfRangeException(nameof(exitRow), exitRow, "Выход из Святилища должен быть дальше входа.");

            EntryRow = entryRow;
            ExitRow = exitRow;
            Multiplier = new SanctuaryMultiplier();
            Collapse = new CollapseFront(entryRow, collapseRowsPerSecond);
        }

        public int EntryRow { get; }

        public int ExitRow { get; }

        public SanctuaryMultiplier Multiplier { get; }

        public CollapseFront Collapse { get; }

        /// <summary>Мана, набранная в Святилище (с учётом множителя в момент сбора); зачисляется на выходе.</summary>
        public int AccumulatedIncome { get; private set; }

        public bool HasExited { get; private set; }

        public bool IsSarcophagusTaken { get; private set; }

        public bool IsActive => !HasExited;

        /// <summary>Игрок взял Саркофаг — обрушение ускорилось.</summary>
        public event Action SarcophagusTaken;

        /// <summary>Эхо подобрано (для празднования на HUD).</summary>
        public event Action EchoCollected;

        /// <summary>Жила: + Мана × текущий множитель.</summary>
        public void CollectVein(int baseAmount)
        {
            if (!IsActive) return;
            AccumulatedIncome += Multiplier.ApplyToIncome(baseAmount);
        }

        /// <summary>Резонанс: +0.5 к множителю.</summary>
        public void CollectResonance()
        {
            if (!IsActive) return;
            Multiplier.ApplyResonanceTile();
        }

        /// <summary>Эхо: ×2 к множителю.</summary>
        public void CollectEcho()
        {
            if (!IsActive) return;
            Multiplier.ApplyEchoTile();
            EchoCollected?.Invoke();
        }

        /// <summary>Пока игрок стоит на Разломе: копится Мана (подтип Жилы) или множитель (подтип Резонанса).</summary>
        public void TickRift(SanctuaryRiftSubtype subtype, float deltaSeconds)
        {
            if (!IsActive) return;
            if (subtype == SanctuaryRiftSubtype.Resonance)
                Multiplier.TickRiftResonance(deltaSeconds);
            else
                AccumulatedIncome += Multiplier.ApplyToIncome((int)(RiftVeinIncomePerSecond * deltaSeconds));
        }

        /// <summary>Игрок поднял Саркофаг (один на Святилище). Повторный вызов — не-op.</summary>
        public void TakeSarcophagus()
        {
            if (!IsActive || IsSarcophagusTaken) return;
            IsSarcophagusTaken = true;
            SarcophagusTaken?.Invoke();
        }

        /// <summary>Продвигает фронт обрушения; после взятия Саркофага — быстрее.</summary>
        public void Tick(float deltaSeconds)
        {
            if (!IsActive) return;
            if (RunEffects.Current.CollapseHeld) return; // Ночь Чернобога: обрушение стоит
            Collapse.Tick(deltaSeconds, IsSarcophagusTaken ? SarcophagusCollapseMultiplier : 1f);
        }

        /// <summary>Последний ряд, который уже обрушен (не дальше <c>ExitRow-1</c>).</summary>
        public int LastCollapsedRow => Math.Min(ExitRow - 1, (int)Math.Floor(Collapse.RowPosition));

        public void ReportPlayerRow(int playerRow)
        {
            if (!IsActive) return;
            if (playerRow >= ExitRow) HasExited = true;
        }
    }
}
