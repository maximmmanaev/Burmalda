using System;
using System.Collections.Generic;
using System.Linq;
using Burmalda.Artifacts;

namespace Burmalda.Altar
{
    /// <summary>Событие аналитики <c>altar_pick</c> (PRD v10 §30): предложенные id, выбранный, замещённый, Ярус.</summary>
    public sealed class AltarPickEvent
    {
        public AltarPickEvent(int yarus, IReadOnlyList<string> offeredIds, string pickedId, string replacedId)
        {
            Yarus = yarus;
            OfferedIds = offeredIds;
            PickedId = pickedId;
            ReplacedId = replacedId;
        }

        public const string Name = "altar_pick";

        public int Yarus { get; }

        public IReadOnlyList<string> OfferedIds { get; }

        public string PickedId { get; }

        /// <summary>Id замещённого предмета; null, если слот был свободен.</summary>
        public string ReplacedId { get; }

        public override string ToString() =>
            $"{Name} yarus={Yarus} offered=[{string.Join(",", OfferedIds)}] picked={PickedId} replaced={ReplacedId ?? "-"}";
    }

    public enum AltarPickState
    {
        /// <summary>Игрок выбирает карточку. Пропустить / закрыть нельзя.</summary>
        Choosing,

        /// <summary>Слоты типа заполнены — игрок выбирает, какой предмет заменить.</summary>
        ChoosingReplacement,

        /// <summary>Ровно одна Реликвия взята.</summary>
        Done,
    }

    /// <summary>
    /// Выбор на Алтаре (PRD v10 §13.2): игрок всегда берёт <b>ровно одну</b> Реликвию из вариантов — без пропуска,
    /// цен, реролла и продажи. Если слоты типа полны, второй шаг — какой предмет заменить (можно вернуться к
    /// карточкам, но не уйти без выбора).
    /// </summary>
    public sealed class AltarPickSession
    {
        private readonly RunRelicBuild _build;
        private readonly int _yarus;

        public AltarPickSession(AltarOffer offer, RunRelicBuild build, int yarus)
        {
            Offer = offer ?? throw new ArgumentNullException(nameof(offer));
            _build = build ?? throw new ArgumentNullException(nameof(build));
            if (offer.Options.Count == 0) throw new ArgumentException("Нечего выбирать: предложение пусто.", nameof(offer));
            _yarus = yarus;
        }

        public AltarOffer Offer { get; }

        public IReadOnlyList<RelicDefinition> Options => Offer.Options;

        public AltarPickState State { get; private set; } = AltarPickState.Choosing;

        /// <summary>Выбранная карточка, ожидающая решения о замене (State == ChoosingReplacement).</summary>
        public RelicDefinition Pending { get; private set; }

        /// <summary>Предметы билда того же типа — кандидаты на замену.</summary>
        public IReadOnlyList<RelicDefinition> ReplaceCandidates =>
            Pending == null ? Array.Empty<RelicDefinition>() : _build.OfType(Pending.Category);

        /// <summary>Выбор завершён (после подтверждения; ровно один раз).</summary>
        public event Action<AltarPickEvent> Completed;

        /// <summary>Нажата карточка. Если слот свободен — Реликвия берётся сразу, иначе нужен выбор замены.</summary>
        public bool Pick(string relicId)
        {
            if (State != AltarPickState.Choosing) return false;
            var option = Options.FirstOrDefault(r => r.Id == relicId);
            if (option == null) return false;

            if (_build.HasFreeSlot(option.Category))
            {
                _build.TryAdd(option);
                Finish(option.Id, null);
            }
            else
            {
                Pending = option;
                State = AltarPickState.ChoosingReplacement;
            }

            return true;
        }

        /// <summary>Второй шаг: выбранный предмет вытесняет <paramref name="dropId"/>.</summary>
        public bool Replace(string dropId)
        {
            if (State != AltarPickState.ChoosingReplacement || Pending == null) return false;
            if (!_build.TryReplace(dropId, Pending)) return false;
            Finish(Pending.Id, dropId);
            return true;
        }

        /// <summary>Назад к карточкам со второго шага (не пропуск: выйти без выбора нельзя).</summary>
        public bool BackToCards()
        {
            if (State != AltarPickState.ChoosingReplacement) return false;
            Pending = null;
            State = AltarPickState.Choosing;
            return true;
        }

        private void Finish(string pickedId, string replacedId)
        {
            State = AltarPickState.Done;
            Completed?.Invoke(new AltarPickEvent(_yarus, Options.Select(o => o.Id).ToList(), pickedId, replacedId));
        }
    }
}
