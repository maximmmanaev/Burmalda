using System.Collections.Generic;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Каталог видов артефактов, которые умеет описывать игра (design-time
    /// данные — отличается от <see cref="ArtifactPool"/>, который хранит,
    /// что уже РАЗБЛОКИРОВАНО в конкретном прохождении). Пока содержит
    /// только 5 примеров Амулетов/Талисманов, явно перечисленных в issue
    /// #17 как минимум для критериев приёмки — не полный релизный набор
    /// (12–15 Амулетов/Талисманов, roadmap.md, «Скоуп релиза»), остальной
    /// объём — контент-задача по мере баланса (Спринт 10).
    ///
    /// Числовые эффекты (иммунитет к 2 стрелам, 2d20, +10 Маны, ×2 Ключей/
    /// Маны) не применяются автоматически — Алтарь как источник выдачи
    /// (Спринт 6) и часть зависимых систем (d20 — Спринт 7) ещё не
    /// реализованы; <see cref="Amulet.EffectDescription"/>/<see cref="Talisman.EffectDescription"/>
    /// хранят текстовое описание для будущей витрины/UI.
    /// </summary>
    public static class ArtifactCatalog
    {
        public static IReadOnlyList<Amulet> Amulets { get; } = new List<Amulet>
        {
            new Amulet("amulet-trap-immunity", "Иммунитет к ловушкам",
                "Даёт иммунитет на попадание двух стрел", new[] { ArtifactTag.Defense }),

            new Amulet("amulet-second-chance", "Второй шанс",
                "При d20-броске после смерти бросаются 2d20, засчитывается больший результат", new[] { ArtifactTag.Defense }),
        };

        public static IReadOnlyList<Talisman> Talismans { get; } = new List<Talisman>
        {
            new Talisman("talisman-mana-every-third-tile", "Жила щедрости",
                "Каждая 3-я плитка приносит на 10 Кристаллов Маны больше", new[] { ArtifactTag.Mana }),

            new Talisman("talisman-double-keys", "Хватка Ключника",
                "Плитка с Ключом приносит в 2 раза больше Ключей", new[] { ArtifactTag.Keys }),

            new Talisman("talisman-double-mana", "Двойная Жила",
                "Увеличивает получаемые Кристаллы Маны в 2 раза", new[] { ArtifactTag.Mana }),
        };
    }
}
