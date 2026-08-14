using System;
using System.Collections.Generic;
using Burmalda.Artifacts;

namespace Burmalda.Achievements
{
    /// <summary>
    /// Отслеживание Достижений (PRD v6, раздел 1; issue #50): опрашивает
    /// зарегистрированные <see cref="AchievementDefinition"/> и при первом
    /// выполнении условия — навсегда фиксирует получение, разблокирует
    /// артефакт в <see cref="ArtifactPool"/> и поднимает <see cref="Granted"/>
    /// (хук для "яркого визуального оповещения" — сама нотификация/VFX вне
    /// скоупа, в проекте нет UI-слоя, как и для прочих подобных хуков в
    /// проекте).
    ///
    /// <see cref="Evaluate"/> — опрос, а не подписка на событие: в проекте
    /// нет единого шины событий забега (issue #85, Спринт 13, явно называет
    /// её отсутствие риском для тиров I/II/III); опрос проще и достаточен
    /// для базовой (без тиров) версии — вызывающая сторона сама решает,
    /// как часто опрашивать (после каждого хода трейла, каждый кадр и т.п.).
    ///
    /// Получение — постоянное состояние (как <see cref="ArtifactPool.UnlockedIds"/>),
    /// но пока НЕ подключено к Burmalda.Persistence — в проекте нет ни
    /// одного конкретного <see cref="AchievementDefinition"/> для
    /// регистрации (см. его doc-комментарий) и, соответственно, ни одного
    /// Controller'а, который создавал бы этот трекер в реальной сцене.
    /// <see cref="MarkGranted"/> спроектирован специально для будущего
    /// восстановления из сохранения (см. Persistence.ProgressSnapshot.Apply)
    /// — не триггерит повторный <see cref="Granted"/>/<see cref="ArtifactPool.Unlock"/>,
    /// когда появится реальный каталог и Controller.
    /// </summary>
    public sealed class AchievementTracker
    {
        private readonly ArtifactPool _pool;
        private readonly List<AchievementDefinition> _definitions = new List<AchievementDefinition>();
        private readonly HashSet<string> _grantedIds = new HashSet<string>();

        public AchievementTracker(ArtifactPool pool)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        }

        /// <summary>Срабатывает при первом получении Достижения — хук для "яркого визуального оповещения".</summary>
        public event Action<AchievementDefinition> Granted;

        public IReadOnlyCollection<string> GrantedIds => _grantedIds;

        /// <summary>Регистрирует Достижение для опроса в <see cref="Evaluate"/>. Порядок регистрации не важен.</summary>
        public void Register(AchievementDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            _definitions.Add(definition);
        }

        public bool IsGranted(string achievementId) => _grantedIds.Contains(achievementId);

        /// <summary>
        /// Помечает Достижение полученным без побочных эффектов (не трогает
        /// Пул, не поднимает <see cref="Granted"/>) — для восстановления из
        /// сохранения, когда пул уже восстановлен отдельно. Повторные
        /// вызовы — не-op.
        /// </summary>
        public void MarkGranted(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId)) throw new ArgumentException("Id достижения не может быть пустым.", nameof(achievementId));
            _grantedIds.Add(achievementId);
        }

        /// <summary>
        /// Проверяет условия всех ещё не полученных зарегистрированных
        /// Достижений. При выполнении условия — фиксирует получение,
        /// разблокирует артефакт-награду и поднимает <see cref="Granted"/>.
        /// Уже полученные Достижения повторно не проверяются и не могут
        /// быть получены дважды.
        /// </summary>
        public void Evaluate()
        {
            foreach (var definition in _definitions)
            {
                if (_grantedIds.Contains(definition.Id)) continue;
                if (!definition.ConditionMet()) continue;

                _grantedIds.Add(definition.Id);
                _pool.Unlock(definition.UnlockArtifactId);
                Granted?.Invoke(definition);
            }
        }
    }
}
