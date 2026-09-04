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
    /// 'k' KeySource, 'v' GateVaultKeySource (тайник за Воротами — сумма из
    /// <see cref="SegmentTemplate.GateVaultPurchases"/>, не 'k'), 'A' Altar,
    /// 'B' Boss.
    ///
    /// **Контент-долг**: 40 шаблонов (13 исходных + Партия 2, 13 шаблонов +
    /// Партия 1, 14 шаблонов — обе из задачи «партии 1 и 2 + правила
    /// отбора», Партия 1 написана раньше, интегрирована позже: очередь
    /// задач перетасовалась) — уже выше минимума ~30 на Ярус, требуемого
    /// PRD до релиза (issue #78: "минимум ~30 авторских шаблонов на Ярус
    /// до релиза"). Покрывает весь диапазон сложности 1–5 плотнее, чем
    /// раньше — достаточно, чтобы селектор перестал видеть только пустые
    /// тир-1 раскладки на старте (см. <see cref="SegmentSelector"/>).
    /// Открытый вопрос той же задачи: часть исходных 13 шаблонов помечена
    /// тегом <see cref="SegmentRewardTag.Coins"/> — валюта, которая в
    /// забеге больше не начисляется (PRD v9, экономика "Мана как доход
    /// забега, Монеты только в Лагере") — решение, что с этим делать
    /// (перевести на <see cref="SegmentRewardTag.Mana"/> или завести
    /// нейтральный тег "без награды"), за владельцем. Точный релизный
    /// объём/узнаваемость шаблонов — по-прежнему предмет контент-чеклиста
    /// релиза (issue #111).
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

            // Найдено на реальном билде (2026-09-01, живой забег до Яруса 6):
            // 'e' на (1,1) целился в (2,1)='l' — Tile.MarkLethalTrap(Explosion)
            // поверх уже стоящей Lava бросало InvalidOperationException в
            // рантайме (Tile.GuardAgainstConflictingRole, docs/wiki/changelog.md,
            // задача «двойные флаги на плитах»). Минимальный фикс — 'e'
            // сдвинут на одну клетку вправо (col1→col2, был открытым полом),
            // теперь целится в (2,2)='.'; Lava/Blade-триггер не тронуты.
            // ValidateTriggerTargetsDoNotConflict (Generation.SegmentTemplate)
            // теперь ловит этот класс ошибки на этапе авторинга.
            new SegmentTemplate("испытание", 5, SegmentRewardTag.Artifact, ParseRows(new[]
            {
                ".....",
                "#.ea#",
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
            //
            // Найдено на реальном билде (2026-09-01, живой забег до Яруса 6):
            // на первый взгляд похоже на конфликт (Мана прямо под клеткой,
            // куда бьёт взрыв), но это ТА ЖЕ намеренная механика "триггер
            // уничтожает награду под собой", что и у «выкуп»/«последний-
            // рывок» ниже (владелец, прямое подтверждение) — Tile.
            // TransitionToLethalTrap (рантайм-переход, не MarkLethalTrap)
            // спокойно превращает Ману в ловушку при срабатывании. Раскладка
            // НЕ менялась.
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
            // issue #208: клетка ворот примыкала к стенам со всех сторон,
            // кроме самого ключа за ней — при открытых воротах подойти к
            // ним было физически неоткуда. Один символ в стене (col2 у
            // ворот) открывает подход именно К ВОРОТАМ, не к ключу напрямую
            // — рычаг остаётся обязательным, карман больше не замкнут сам на себя.
            // Владелец, 2026-09-04: рычаг↔Ворота 2 ряда — короткий крюк,
            // тайник на 1 покупку ('v' = GateVaultKeySource, не 'k' — сумма
            // из GateVaultPricing, не общий KeysPerSource).
            new SegmentTemplate("двор-с-рычагом", 3, SegmentRewardTag.Keys, ParseRows(new[]
            {
                ".....",
                "...L.",
                "..###",
                "...gv",
                "..###",
                ".....",
            }), gateVaultPurchases: 1.0),

            // Тир 3. Рычаг в начале, ворота в конце — нажимаешь, ещё не зная,
            // что откроешь.
            // issue #208: та же правка, что у "двор-с-рычагом" — подход к
            // воротам сбоку, ключ по-прежнему достижим только через них.
            // Владелец, 2026-09-04: рычаг↔Ворота 4 ряда — длинный крюк,
            // тайник на 1.5 покупки.
            new SegmentTemplate("ворота-склада", 3, SegmentRewardTag.Keys, ParseRows(new[]
            {
                ".....",
                ".L...",
                ".....",
                ".....",
                "..###",
                "...gv",
                "..###",
            }), gateVaultPurchases: 1.5),

            // Тир 4. Ключевой шаблон партии: запертый склад стоит у входа,
            // рычаг лежит глубоко внизу. Чтобы забрать награду, надо пройти
            // мимо, спуститься за рычагом и вернуться назад по уже
            // прогорающему полу — ближайшее к механике Ворот из PRD v9 §4.3,
            // что можно собрать на текущих Lever/LeverGate.
            // issue #208: та же правка — подход к воротам сбоку (col2),
            // не к ключу напрямую.
            // Владелец, 2026-09-04: рычаг↔Ворота 4 ряда — длинный крюк,
            // тайник на 1.5 покупки.
            new SegmentTemplate("рычаг-в-глубине", 4, SegmentRewardTag.Keys, ParseRows(new[]
            {
                "..###",
                "...gv",
                "..###",
                ".....",
                ".....",
                "..L..",
                ".....",
            }), gateVaultPurchases: 1.5),

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

            // ==================== Партия 1 (issue #111) ====================
            // 14 шаблонов, авторские раскладки владельца продукта — написана
            // раньше Партии 2, интегрирована позже (очередь задач
            // перетасовалась). Отдельная секция ниже, а не переставлена
            // хронологически внутрь Партии 2 выше — порядок добавления в
            // каталог не несёт игрового смысла (SegmentSelector выбирает по
            // тиру, не по позиции в списке), сохранён для истории коммитов.

            // Тир 1. Первое решение в игре: две полосы, две валюты, взять обе не выйдет.
            new SegmentTemplate("выбор-полосы", 1, SegmentRewardTag.Mana, ParseRows(new[]
            {
                ".....",
                "m...k",
                ".....",
                "m...k",
                ".....",
            })),

            // Тир 1. Прямая дорога перекрыта по центру, учит сворачивать.
            new SegmentTemplate("первый-крюк", 1, SegmentRewardTag.Mana, ParseRows(new[]
            {
                ".....",
                "..#..",
                ".m.k.",
                "..#..",
                ".....",
            })),

            // Тир 2. Центральная полоса выглядит удобной и наказывает: оба
            // триггера бьют по ней.
            new SegmentTemplate("ложная-прямая", 2, SegmentRewardTag.Mana, ParseRows(new[]
            {
                ".....",
                "..e..",
                ".....",
                "..e..",
                ".....",
            })),

            // Тир 2. Ключ кажется замурованным, достижим только с правого края.
            new SegmentTemplate("жадный-угол", 2, SegmentRewardTag.Keys, ParseRows(new[]
            {
                ".....",
                "..pp.",
                "..pk.",
                "..pp.",
                ".....",
            })),

            // Тир 2. Обе жилы стоят между ямами.
            new SegmentTemplate("двойная-цена", 2, SegmentRewardTag.Mana, ParseRows(new[]
            {
                ".....",
                ".p.p.",
                ".m.m.",
                ".p.p.",
                ".....",
            })),

            // Тир 3. Карман справа изолирован стеной, рычаг у входа: решать
            // надо заранее, ещё не видя содержимого.
            new SegmentTemplate("пещера-за-рычагом", 3, SegmentRewardTag.Keys, ParseRows(new[]
            {
                ".....",
                ".L.#.",
                "...#g",
                "...#k",
                "...#.",
            })),

            // Тир 3. Лезвия по краям, затем по центру: ритм, а не реакция.
            new SegmentTemplate("коридор-лезвий", 3, SegmentRewardTag.Mana, ParseRows(new[]
            {
                ".....",
                "b...b",
                ".....",
                ".b.b.",
                ".....",
            })),

            // Тир 3. Вход в лавовое поле один, с правого края.
            new SegmentTemplate("ключ-за-лавой", 3, SegmentRewardTag.Keys, ParseRows(new[]
            {
                ".....",
                ".lll.",
                ".llk.",
                ".lll.",
                ".....",
            })),

            // Тир 4. Проходится только зигзагом, то есть с максимальным
            // распадом за спиной.
            new SegmentTemplate("решето", 4, SegmentRewardTag.Mana, ParseRows(new[]
            {
                ".....",
                "p.p.p",
                ".....",
                "p.p.p",
                "..m..",
            })),

            // Тир 4. Триггер стоит над ключом: прямой путь уничтожает
            // собственную награду. Подтверждено владельцем (2026-09-01) как
            // намеренная механика, не ошибка авторинга — Core.Tile.
            // TransitionToLethalTrap (рантайм-переход, не MarkLethalTrap)
            // спокойно превращает KeySource в LethalTrap при срабатывании,
            // Generation.SegmentTemplate.ValidateTriggerTargetsDoNotConflict
            // намеренно не проверяет эту пару ролей.
            new SegmentTemplate("выкуп", 4, SegmentRewardTag.Keys, ParseRows(new[]
            {
                ".....",
                ".e...",
                ".k...",
                ".....",
                ".....",
            })),

            // Тир 4. Богатая середина, стрелы по краям.
            new SegmentTemplate("стрелы-и-жила", 4, SegmentRewardTag.Mana, ParseRows(new[]
            {
                ".....",
                "a.m.a",
                ".....",
                ".m.m.",
                ".....",
            })),

            // Тир 5. Единственная проходимая колонна, уклоняться некуда.
            new SegmentTemplate("мост", 5, SegmentRewardTag.Mana, ParseRows(new[]
            {
                ".....",
                "pp.pp",
                "pp.pp",
                "pp.pp",
                "..m..",
            })),

            // Тир 5. Два ключа, над каждым триггер, между ними лава. Та же
            // намеренная механика "триггер уничтожает награду", что у
            // «выкуп» выше — подтверждено владельцем, см. её комментарий.
            new SegmentTemplate("последний-рывок", 5, SegmentRewardTag.Keys, ParseRows(new[]
            {
                ".....",
                ".l.l.",
                ".e.e.",
                ".k.k.",
                ".....",
            })),

            // Тир 5. Богатый ствол со стенами: зашёл — идёшь до конца.
            new SegmentTemplate("шахта-жадности", 5, SegmentRewardTag.Keys, ParseRows(new[]
            {
                ".....",
                ".#k#.",
                ".#k#.",
                ".#k#.",
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
                    'v' => SegmentTileType.GateVaultKeySource,
                    'A' => SegmentTileType.Altar,
                    'B' => SegmentTileType.Boss,
                    _ => SegmentTileType.Open
                };

            return tiles;
        }
    }
}
