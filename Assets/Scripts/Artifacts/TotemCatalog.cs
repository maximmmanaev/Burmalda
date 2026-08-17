using System.Collections.Generic;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Каталог именованных экземпляров Тотема (design-time данные, по
    /// аналогии с <see cref="ArtifactCatalog"/>) — ровно 4 штуки, по одному
    /// на каждое значение <see cref="TotemAbilityType"/> (PRD раздел 12: 4
    /// активные способности, закрытый список). Отдельного авторства не
    /// требуется — названия уже даны самим PRD (doc-комментарии
    /// <see cref="TotemAbilityType"/>), здесь только оборачиваются в
    /// экземпляры <see cref="Totem"/>, которых раньше не было вообще (0
    /// именованных экземпляров, см. docs/wiki/sprint10-content-audit.md).
    /// </summary>
    public static class TotemCatalog
    {
        public static IReadOnlyList<Totem> Totems { get; } = new List<Totem>
        {
            new Totem("totem-dash", "Рывок", TotemAbilityType.Dash),
            new Totem("totem-breach", "Пробой", TotemAbilityType.Breach),
            new Totem("totem-invulnerability", "Неуязвимость", TotemAbilityType.Invulnerability),
            new Totem("totem-second-wind", "Второе дыхание", TotemAbilityType.SecondWind),
        };
    }
}
