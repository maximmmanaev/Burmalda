using System;
using System.Collections.Generic;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Пул артефактов (PRD раздел 6, issue #18): "все виды, доступные к
    /// выпадению [и покупке в Алтаре]. Никогда не сбрасывается, только
    /// растёт" — в отличие от <see cref="ArtifactCollection"/> (личная
    /// история использования игрока), Пул — общий список ВИДОВ, которые
    /// сейчас в принципе могут выпасть/продаваться в этом прохождении.
    ///
    /// Политика "что разблокировано изначально, а что — только после первой
    /// победы над Боссом" (PRD раздел 6: Талисманы/Амулеты) сюда не зашита —
    /// это решение внешнего вызывающего кода (Спринт 7 вызовет
    /// <see cref="Unlock"/> для каждого Талисмана/Амулета при первой победе).
    /// </summary>
    public sealed class ArtifactPool
    {
        private readonly HashSet<string> _unlockedIds = new HashSet<string>();

        /// <summary>Срабатывает, когда вид артефакта разблокируется впервые.</summary>
        public event Action<string> Unlocked;

        public bool IsUnlocked(string artifactId) => _unlockedIds.Contains(artifactId);

        public IReadOnlyCollection<string> UnlockedIds => _unlockedIds;

        /// <summary>Разблокирует вид артефакта в пуле. Повторные вызовы — не-op.</summary>
        public void Unlock(string artifactId)
        {
            if (string.IsNullOrEmpty(artifactId)) throw new ArgumentException("Id артефакта не может быть пустым.", nameof(artifactId));
            if (!_unlockedIds.Add(artifactId)) return;
            Unlocked?.Invoke(artifactId);
        }
    }
}
