using System;
using System.Collections.Generic;

namespace Burmalda.RunModifiers
{
    /// <summary>
    /// Пул разблокированных Знамений (PRD v7 §20, issue #84) — по аналогии
    /// с <c>Artifacts.ArtifactPool</c>, но ключ — <see cref="OmenId"/> (enum,
    /// закрытый список из 6), а не строковый Id. Никогда не сбрасывается,
    /// только растёт.
    ///
    /// Источники разблокировки — "Достижения тира III (issue #85, Спринт
    /// 13), Ярусы Глубины (Спринт 8), бесплатная дорожка Пропуска
    /// Экспедиции (Спринт 13)" — сюда не зашиты: ни PRD, ни issue #84 не
    /// называют, КАКОЙ конкретно Ярус разблокирует КАКОЕ конкретно
    /// Знамение — решение внешнего вызывающего кода, как только появится
    /// содержательное правило (2 из 3 источников — Достижения тира III и
    /// Пропуск — вообще ещё не реализованы, Спринт 13).
    /// </summary>
    public sealed class OmenPool
    {
        private readonly HashSet<OmenId> _unlocked = new HashSet<OmenId>();

        /// <summary>Срабатывает, когда Знамение разблокируется впервые.</summary>
        public event Action<OmenId> Unlocked;

        public bool IsUnlocked(OmenId omenId) => _unlocked.Contains(omenId);

        public IReadOnlyCollection<OmenId> UnlockedIds => _unlocked;

        /// <summary>Разблокирует Знамение в пуле. Повторные вызовы — не-op.</summary>
        public void Unlock(OmenId omenId)
        {
            if (!_unlocked.Add(omenId)) return;
            Unlocked?.Invoke(omenId);
        }
    }
}
