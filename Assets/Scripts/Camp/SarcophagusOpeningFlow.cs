using System;

namespace Burmalda.Camp
{
    /// <summary>Фаза экрана открытия Саркофагов в Лагере (PRD v10 §22.3).</summary>
    public enum SarcophagusOpeningPhase
    {
        /// <summary>Экран не показан.</summary>
        Idle,

        /// <summary>Закрытый Саркофаг ждёт открытия.</summary>
        Closed,

        /// <summary>Только что открыт — видно содержимое.</summary>
        Opened,

        /// <summary>Все открыты (или открывать нечего) — осталась кнопка «Новая экспедиция».</summary>
        Finished
    }

    /// <summary>
    /// Порядок открытия Саркофагов по одному после эвакуации (PRD v10 §22.3): закрыт → открыт →
    /// следующий … → конец → «Новая экспедиция». Чистая логика поверх <see cref="Camp"/>;
    /// экран только отображает <see cref="Phase"/>. Рекламы между возвращением и открытием нет.
    /// </summary>
    public sealed class SarcophagusOpeningFlow
    {
        private readonly Camp _camp;

        public SarcophagusOpeningFlow(Camp camp) => _camp = camp ?? throw new ArgumentNullException(nameof(camp));

        public SarcophagusOpeningPhase Phase { get; private set; }

        /// <summary>Содержимое последнего открытого Саркофага (осмысленно в фазе Opened).</summary>
        public SarcophagusContents LastContents { get; private set; }

        /// <summary>Сколько Саркофагов ещё не открыто.</summary>
        public int Remaining => _camp.UnopenedSarcophagi.Count;

        public void Begin() => Phase = _camp.UnopenedSarcophagi.Count > 0 ? SarcophagusOpeningPhase.Closed : SarcophagusOpeningPhase.Finished;

        public void Reset() => Phase = SarcophagusOpeningPhase.Idle;

        /// <summary>Главная кнопка экрана: открыть / к следующему.</summary>
        public void Advance()
        {
            switch (Phase)
            {
                case SarcophagusOpeningPhase.Closed:
                    var opened = _camp.OpenNext();
                    if (!opened.HasValue)
                    {
                        Phase = SarcophagusOpeningPhase.Finished;
                        return;
                    }

                    LastContents = opened.Value;
                    Phase = SarcophagusOpeningPhase.Opened;
                    break;

                case SarcophagusOpeningPhase.Opened:
                    Phase = _camp.UnopenedSarcophagi.Count > 0 ? SarcophagusOpeningPhase.Closed : SarcophagusOpeningPhase.Finished;
                    break;
            }
        }
    }
}
