using System;
using System.Collections.Generic;

namespace Burmalda.Camp
{
    /// <summary>
    /// Саркофаги, несомые в текущем забеге (PRD v10 §15.1): взятый Саркофаг «тяжёлый» —
    /// ускоряет распад (§7.5). Эвакуация переносит их в Лагерь, окончательная смерть —
    /// теряет. Новый экземпляр на каждый забег.
    /// </summary>
    public sealed class RunSarcophagi
    {
        private readonly List<Sarcophagus> _items = new List<Sarcophagus>();

        public int Count => _items.Count;

        public IReadOnlyList<Sarcophagus> Items => _items;

        /// <summary>Срабатывает при каждом изменении набора.</summary>
        public event Action Changed;

        public void Add(Sarcophagus sarcophagus)
        {
            _items.Add(sarcophagus);
            Changed?.Invoke();
        }

        public void Clear()
        {
            if (_items.Count == 0) return;
            _items.Clear();
            Changed?.Invoke();
        }
    }
}
