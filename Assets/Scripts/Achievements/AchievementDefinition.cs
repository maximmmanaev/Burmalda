using System;

namespace Burmalda.Achievements
{
    /// <summary>
    /// Достижение (PRD v6, раздел 1; issue #50): "Выполнение условия даёт
    /// игроку достижение — яркое визуальное оповещение — и разблокирует
    /// артефакт в пуле". Условие — предикат, а не декларативные данные:
    /// содержательные условия ссылаются на произвольные игровые системы
    /// (Алтарь, Боссы, ловушки/рычаги — из текста issue #50), общего
    /// формата для них в проекте нет и не появится до issue #85 (Спринт 13,
    /// "один общий источник событий забега" для тиров I/II/III).
    ///
    /// Каталог из конкретных 10 Достижений (объём релиза, docs/wiki/roadmap.md)
    /// НЕ создан — ни PRD, ни issue #50 не называют ни одного конкретного
    /// условия/названия/артефакта-награды; авторство такого контента
    /// остаётся за product owner/геймдизайном, а не агентом (см.
    /// docs/rules/forbidden-actions.md). Здесь реализован только сам
    /// механизм (этот класс + <see cref="AchievementTracker"/>).
    /// </summary>
    public sealed class AchievementDefinition
    {
        public AchievementDefinition(string id, string name, string unlockArtifactId, Func<bool> conditionMet)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id достижения не может быть пустым.", nameof(id));
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Название достижения не может быть пустым.", nameof(name));
            if (string.IsNullOrEmpty(unlockArtifactId)) throw new ArgumentException("Id разблокируемого артефакта не может быть пустым.", nameof(unlockArtifactId));

            Id = id;
            Name = name;
            UnlockArtifactId = unlockArtifactId;
            ConditionMet = conditionMet ?? throw new ArgumentNullException(nameof(conditionMet));
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>Вид артефакта, разблокируемый в <see cref="Artifacts.ArtifactPool"/> при получении.</summary>
        public string UnlockArtifactId { get; }

        /// <summary>Условие получения — читает текущее состояние произвольных игровых систем.</summary>
        public Func<bool> ConditionMet { get; }
    }
}
