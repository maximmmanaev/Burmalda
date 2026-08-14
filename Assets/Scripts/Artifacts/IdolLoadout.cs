using System;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// 2 постоянных слота Идолов (PRD раздел 6: "2 слота"). Постоянная, как
    /// <c>Currencies.PersistentWallet</c> — не пересобирается между забегами.
    /// </summary>
    public sealed class IdolLoadout
    {
        public const int SlotCount = 2;

        private readonly Idol[] _slots = new Idol[SlotCount];

        public Idol GetSlot(int index)
        {
            ValidateIndex(index);
            return _slots[index];
        }

        public void Equip(int index, Idol idol)
        {
            ValidateIndex(index);
            _slots[index] = idol ?? throw new ArgumentNullException(nameof(idol));
        }

        public void Unequip(int index)
        {
            ValidateIndex(index);
            _slots[index] = null;
        }

        private static void ValidateIndex(int index)
        {
            if (index < 0 || index >= SlotCount)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Индекс слота должен быть в диапазоне [0, {SlotCount - 1}].");
        }
    }
}
