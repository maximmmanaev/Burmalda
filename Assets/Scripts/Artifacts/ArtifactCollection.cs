using System;
using System.Collections.Generic;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Коллекция (PRD раздел 6, issue #18): "применённый хотя бы раз
    /// артефакт фиксируется навсегда, даже если сам инстанс исчезнет" —
    /// история по Id вида артефакта, не по инстансам. Постоянная, как
    /// <c>Currencies.PersistentWallet</c> (живёт в памяти сессии, сохранение
    /// на диск — issue #107, Спринт 9).
    /// </summary>
    public sealed class ArtifactCollection
    {
        private readonly HashSet<string> _recordedIds = new HashSet<string>();

        /// <summary>Срабатывает, когда артефакт фиксируется впервые — не срабатывает повторно.</summary>
        public event Action<string> Recorded;

        public bool Contains(string artifactId) => _recordedIds.Contains(artifactId);

        /// <summary>Все зафиксированные Id — для сохранения/восстановления (issue #107), по аналогии с <see cref="ArtifactPool.UnlockedIds"/>.</summary>
        public IReadOnlyCollection<string> RecordedIds => _recordedIds;

        /// <summary>Фиксирует артефакт как использованный хотя бы раз. Повторные вызовы для уже зафиксированного id — не-op.</summary>
        public void Record(string artifactId)
        {
            if (string.IsNullOrEmpty(artifactId)) throw new ArgumentException("Id артефакта не может быть пустым.", nameof(artifactId));
            if (!_recordedIds.Add(artifactId)) return;
            Recorded?.Invoke(artifactId);
        }
    }
}
