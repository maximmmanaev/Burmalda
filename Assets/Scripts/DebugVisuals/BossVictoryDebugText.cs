using Burmalda.Artifacts;
using Burmalda.Boss;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Issue #110 (PRD §19, «Победа над Боссом»): debug-текст на
    /// <see cref="BossEncounterSystem.EncounterResolved"/>. Намеренно НЕ
    /// упоминает порог/накопленную энергию (<see cref="BossEncounterOutcome.AccumulatedMana"/>/
    /// <see cref="BossEncounterOutcome.RequiredEnergy"/>) — по прямому
    /// требованию задачи: механика порога энергии заменяется Комнатой Босса
    /// в PRD v8 (см. docs/wiki/roadmap.md, таблица миграции), а Ярус и
    /// Реликвия остаются актуальны и после перехода — этот текст переживёт
    /// миграцию без правок.
    /// </summary>
    public static class BossVictoryDebugText
    {
        public static string BuildMessage(BossEncounterOutcome outcome, Relic relic, int tier)
        {
            if (outcome == null) return string.Empty;

            if (!outcome.IsVictory)
                return $"Босс: поражение (Ярус {tier})";

            var relicText = relic != null ? relic.Name : "без Реликвии";
            return $"Босс: победа! Ярус {tier}. Получено: {relicText}";
        }
    }
}
