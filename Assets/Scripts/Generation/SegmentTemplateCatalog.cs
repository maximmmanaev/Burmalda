using System.Collections.Generic;

namespace Burmalda.Generation
{
    /// <summary>
    /// Каталог авторских шаблонов сегментов (PRD v7 §21, issue #78).
    /// Раскладка задаётся компактной ASCII-сеткой (см. <see cref="ParseRows"/>)
    /// — читаемо и достаточно для текущего этапа, пока нет визуального
    /// редактора уровней; строки идут от входа (первая строка) к выходу
    /// (последняя).
    ///
    /// Символ → <see cref="SegmentTileType"/>: '.' Open, '#' Blocked, 'p' Pit,
    /// 'l' Lava, 'e' ExplosiveTrigger, 'a' TimedTrapArrowTrigger,
    /// 'b' TimedTrapBladeTrigger, 'L' Lever, 'g' LeverGate, 'm' ManaSource,
    /// 'k' KeySource, 'A' Altar, 'B' Boss.
    ///
    /// **Контент-долг**: 11 шаблонов, а не минимум ~30 на Ярус, требуемый
    /// PRD до релиза (issue #78: "минимум ~30 авторских шаблонов на Ярус до
    /// релиза"). Покрывает все именованные в PRD примеры («зал с рычагом»,
    /// «коридор с лезвиями», «разлом», «россыпь Ключей», «жила Маны»,
    /// «пустой перегон») и весь диапазон сложности 1–5 — достаточно, чтобы
    /// система генерации была функциональна и тестируема, но не достаточно
    /// для релиза (риск «узнаваемости» шаблонов). Оставшийся объём — предмет
    /// контент-чеклиста релиза (Спринт 10, docs/wiki/roadmap.md).
    ///
    /// Проходимость каждого шаблона проверена на этапе авторинга — см.
    /// <c>SegmentTemplateCatalogTests.AllTemplates_AreTraversable</c> (issue
    /// #78: "проверяется на этапе авторинга, не в рантайме").
    /// </summary>
    public static class SegmentTemplateCatalog
    {
        public static IReadOnlyList<SegmentTemplate> All { get; } = new List<SegmentTemplate>
        {
            new SegmentTemplate("пустой-перегон", 1, SegmentRewardTag.Coins, ParseRows(new[]
            {
                ".....",
                ".....",
                ".....",
                ".....",
                ".....",
            })),

            new SegmentTemplate("зал-алтаря", 1, SegmentRewardTag.Coins, ParseRows(new[]
            {
                ".....",
                "..A..",
                ".....",
                ".....",
                ".....",
            })),

            new SegmentTemplate("жила-маны", 1, SegmentRewardTag.Mana, ParseRows(new[]
            {
                ".....",
                ".m.m.",
                ".....",
                ".m.m.",
                ".....",
            })),

            new SegmentTemplate("россыпь-ключей", 1, SegmentRewardTag.Keys, ParseRows(new[]
            {
                ".....",
                "..k..",
                ".k.k.",
                "..k..",
                ".....",
            })),

            new SegmentTemplate("разлом", 2, SegmentRewardTag.Coins, ParseRows(new[]
            {
                ".....",
                "..p..",
                ".p.p.",
                "..p..",
                ".....",
            })),

            new SegmentTemplate("коридор-со-стрелами", 2, SegmentRewardTag.Coins, ParseRows(new[]
            {
                ".....",
                "..a..",
                ".....",
                "..a..",
                ".....",
            })),

            new SegmentTemplate("зал-с-рычагом", 2, SegmentRewardTag.Artifact, ParseRows(new[]
            {
                ".....",
                "..L.#",
                "...#g",
                ".....",
                ".....",
            })),

            new SegmentTemplate("коридор-с-лезвиями", 3, SegmentRewardTag.Coins, ParseRows(new[]
            {
                ".....",
                "..b..",
                ".....",
                "..b..",
                ".....",
            })),

            new SegmentTemplate("взрывной-проход", 3, SegmentRewardTag.Coins, ParseRows(new[]
            {
                ".....",
                "..e..",
                ".....",
                "..e..",
                ".....",
            })),

            new SegmentTemplate("узкий-проход", 3, SegmentRewardTag.Coins, ParseRows(new[]
            {
                ".....",
                "##...",
                "...##",
                "##...",
                ".....",
            })),

            new SegmentTemplate("смешанная-опасность", 4, SegmentRewardTag.Coins, ParseRows(new[]
            {
                ".....",
                "#.p.#",
                "l.b.l",
                "#.p.#",
                ".....",
            })),

            new SegmentTemplate("испытание", 5, SegmentRewardTag.Artifact, ParseRows(new[]
            {
                ".....",
                "#e.a#",
                "#l.b#",
                ".....",
                ".....",
            })),

            new SegmentTemplate("арена-босса", 5, SegmentRewardTag.Artifact, ParseRows(new[]
            {
                ".....",
                ".....",
                "..B..",
                ".....",
                ".....",
            })),
        };

        private static SegmentTileType[,] ParseRows(string[] rows)
        {
            var rowCount = rows.Length;
            var width = rows[0].Length;
            var tiles = new SegmentTileType[rowCount, width];

            for (var r = 0; r < rowCount; r++)
            for (var c = 0; c < width; c++)
                tiles[r, c] = rows[r][c] switch
                {
                    '.' => SegmentTileType.Open,
                    '#' => SegmentTileType.Blocked,
                    'p' => SegmentTileType.Pit,
                    'l' => SegmentTileType.Lava,
                    'e' => SegmentTileType.ExplosiveTrigger,
                    'a' => SegmentTileType.TimedTrapArrowTrigger,
                    'b' => SegmentTileType.TimedTrapBladeTrigger,
                    'L' => SegmentTileType.Lever,
                    'g' => SegmentTileType.LeverGate,
                    'm' => SegmentTileType.ManaSource,
                    'k' => SegmentTileType.KeySource,
                    'A' => SegmentTileType.Altar,
                    'B' => SegmentTileType.Boss,
                    _ => SegmentTileType.Open
                };

            return tiles;
        }
    }
}
