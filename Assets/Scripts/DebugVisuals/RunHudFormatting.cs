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
        /// <summary>Id артефактов заданной категории, в порядке получения, через ", ". "—", если ни одного нет.</summary>
        public static string FormatArtifactIds(IReadOnlyList<Artifact> acquired, ArtifactCategory category)
        {
            var ids = acquired.Where(a => a.Category == category).Select(a => a.Id).ToList();
            return ids.Count > 0 ? string.Join(", ", ids) : "—";
        }
    }
}
