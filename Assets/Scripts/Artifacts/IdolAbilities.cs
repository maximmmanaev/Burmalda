using System.Collections.Generic;

namespace Burmalda.Artifacts
{
    /// <summary>Способности пяти Идолов (PRD v10 §18.2). Реализации — в задаче 3 спринта v10-5.</summary>
    public static class IdolAbilities
    {
        public static IReadOnlyList<IIdolAbility> All() => new List<IIdolAbility>();
    }
}
