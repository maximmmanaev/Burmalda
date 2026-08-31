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
    /// **Контент-долг**: 26 шаблонов (13 исходных + Партия 2, задача «партии
    /// 1 и 2 + правила отбора» — Партия 1, 14 шаблонов, идёт отдельным
    /// заходом, дожидается постановки от владельца), а не минимум ~30 на
    /// Ярус, требуемый PRD до релиза (issue #78: "минимум ~30 авторских
    /// шаблонов на Ярус до релиза"). Покрывает весь диапазон сложности 1–5
    /// плотнее, чем раньше — достаточно, чтобы селектор перестал видеть
    /// только пустые тир-1 раскладки на старте (см. <see cref="SegmentSelector"/>),
    /// но всё ещё не достаточно для релиза (риск «узнаваемости» шаблонов).
    /// Оставшийся объём — предмет контент-чеклиста релиза (issue #111).
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

            // ==================== Партия 2 (issue #111) ====================
            // 13 шаблонов, авторские раскладки владельца продукта. Правило
            // отбора по тирам и запрет повтора подряд — SegmentSelector
            // (задача «партии 1 и 2 + правила отбора»).

            // Тир 1. Первый длинный сегмент каталога (8 рядов) — контраст
            // темпа: после плотного участка нужна передышка, иначе
            // напряжение перестаёт читаться.
            new SegmentTemplate("длинный-перегон", 1, SegmentRewardTag.Mana, ParseRows(new[]
            {
                ".....",
                ".....",
                ".....",
                "..m..",
                ".....",
                ".....",
                ".....",
                ".....",
            })),

            // Тир 2. Выглядит пустым, одна яма ровно на естественной прямой —
            // учит, что пусто не значит безопасно.
            new SegmentTemplate("пустой-обман", 2, SegmentRewardTag.Mana, ParseRows(new[]
            {
                ".....",
                ".....",
                "..p..",
                ".....",
                "..m..",
            })),

            // Тир 2. Награды по очереди на разных краях, тянут игрока из
            // стороны в сторону.
            new SegmentTemplate("качели", 2, SegmentRewardTag.Mana, ParseRows(new[]
            {
                ".....",
                "m...m",
                "..#..",
                ".m.m.",
                "..#..",
                "..m..",
            })),

            // Тир 2. Ключи по краям, центр пуст — учит держаться стен.
            new SegmentTemplate("предбанник", 2, SegmentRewardTag.Keys, ParseRows(new[]
            {
                ".....",
                "k...k",
                ".....",
                "k...k",
                ".....",
                ".....",
            })),

            // Тир 2. Две полосы: в левой Мана, но прямо над ней триггер —
            // взять можно, только зайдя сбоку. Правая чистая, но пустая.
            new SegmentTemplate("развилка-цены", 2, SegmentRewardTag.Mana, ParseRows(new[]
            {
                ".....",
                "..#..",
                "e.#..",
                "m.#..",
                "..#..",
                ".....",
            })),

            // Тир 3. Плотная россыпь ключей, вход и выход только с правого
            // края — внутри развернуться негде.
            new SegmentTemplate("богатый-карман", 3, SegmentRewardTag.Keys, ParseRows(new[]
            {
                ".....",
                ".###.",
                ".#kk.",
                ".#kk.",
                ".###.",
                ".....",
            })),

            // Тир 3. Рычаг на виду, ворота рядом — простейшая форма
            // механики: знакомит с правилом, не наказывая.
            new SegmentTemplate("двор-с-рычагом", 3, SegmentRewardTag.Keys, ParseRows(new[]
            {
                ".....",
                "...L.",
                "..###",
                "..#gk",
                "..###",
                ".....",
            })),

            // Тир 3. Рычаг в начале, ворота в конце — нажимаешь, ещё не зная,
            // что откроешь.
            new SegmentTemplate("ворота-склада", 3, SegmentRewardTag.Keys, ParseRows(new[]
            {
                ".....",
                ".L...",
                ".....",
                ".....",
                "..###",
                "..#gk",
                "..###",
            })),

            // Тир 4. Ключевой шаблон партии: запертый склад стоит у входа,
            // рычаг лежит глубоко внизу. Чтобы забрать награду, надо пройти
            // мимо, спуститься за рычагом и вернуться назад по уже
            // прогорающему полу — ближайшее к механике Ворот из PRD v9 §4.3,
            // что можно собрать на текущих Lever/LeverGate.
            new SegmentTemplate("рычаг-в-глубине", 4, SegmentRewardTag.Keys, ParseRows(new[]
            {
                "..###",
                "..#gk",
                "..###",
                ".....",
                ".....",
                "..L..",
                ".....",
            })),

            // Тир 4. Два триггера сторожат подход к ключу: сработает любой, и
            // коридор к награде сузится.
            new SegmentTemplate("караул", 4, SegmentRewardTag.Keys, ParseRows(new[]
            {
                ".....",
                ".e.e.",
                ".....",
                "..k..",
                ".....",
                ".....",
            })),

            // Тир 4. Узкий перешеек между лавой, уклоняться некуда.
            new SegmentTemplate("лавовый-коридор", 4, SegmentRewardTag.Mana, ParseRows(new[]
            {
                ".....",
                "ll.ll",
                "ll.ll",
                "ll.ll",
                ".....",
                "..m..",
            })),

            // Тир 5. Четыре ключа, ямы между ними — самая богатая и самая
            // опасная раскладка каталога.
            new SegmentTemplate("щедрый-риск", 5, SegmentRewardTag.Keys, ParseRows(new[]
            {
                ".....",
                ".p.p.",
                ".k.k.",
                ".p.p.",
                ".k.k.",
                ".p.p.",
                ".....",
            })),

            // Тир 5. Диагональная цепь Маны через всю ширину — чтобы собрать
            // всё, маршрут получается максимально длинным, а значит и распад
            // максимальным.
            new SegmentTemplate("двойная-жила", 5, SegmentRewardTag.Mana, ParseRows(new[]
            {
                ".....",
                "m....",
                ".m...",
                "..m..",
                "...m.",
                "....m",
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
