using System.Collections.Generic;
using System.Linq;
using Burmalda.Artifacts;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Чистая функция форматирования состава билда для <see cref="RunHudOverlay"/>
    /// (issue "Задача 2 — отладочный HUD") — вынесена из MonoBehaviour по
    /// тому же принципу, что <see cref="TileDebugColor.Resolve"/>: логика
    /// тестируется без сцены/OnGUI.
    /// </summary>
    public static class RunHudFormatting
    {
        /// <summary>Названия Реликвий заданного типа, в порядке получения, через ", ". "—", если ни одной нет.</summary>
        public static string FormatRelicNames(IReadOnlyList<RelicDefinition> items, ArtifactCategory category)
        {
            var names = items.Where(a => a.Category == category).Select(a => a.Name).ToList();
            return names.Count > 0 ? string.Join(", ", names) : "—";
        }
    }
}
