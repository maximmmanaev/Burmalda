using System.Linq;
using NUnit.Framework;

namespace Burmalda.Generation.Tests
{
    public class SegmentTemplateCatalogTests
    {
        // "Гарантия проходимости: сегмент валиден, только если существует
        // путь вход→выход... проверяется на этапе авторинга, не в рантайме"
        // (PRD v7 §21, issue #78) — этот тест и есть та самая проверка на
        // этапе авторинга: падает в CI, если кто-то добавит в каталог
        // непроходимый шаблон.
        [TestCaseSource(nameof(TemplateNames))]
        public void Template_IsTraversable(string name)
        {
            var template = SegmentTemplateCatalog.All.Single(t => t.Name == name);
            Assert.IsTrue(SegmentReachabilityValidator.IsTraversable(template), $"Шаблон '{name}' непроходим.");
        }

        // issue #208: "рычаги появились но стены по прежнему не пускают к
        // ключу" — IsTraversable сознательно не проверяет доступность
        // содержимого ЗА воротами, только основной маршрут в обход них.
        // Этот тест и есть та самая недостающая проверка на этапе авторинга.
        [TestCaseSource(nameof(TemplateNames))]
        public void Template_LeverVaultIsReachable(string name)
        {
            var template = SegmentTemplateCatalog.All.Single(t => t.Name == name);
            Assert.IsTrue(SegmentReachabilityValidator.LeverVaultIsReachable(template),
                $"Шаблон '{name}': тайник за воротами не соединён с остальным сегментом даже при открытых воротах.");
        }

        // Issue #193, критерий 6: "проверка возвратного маршрута (открывашка
        // → Ворота), не только прямого маршрута входа" — на всём реальном каталоге.
        [TestCaseSource(nameof(TemplateNames))]
        public void Template_ReturnRouteToExitExists(string name)
        {
            var template = SegmentTemplateCatalog.All.Single(t => t.Name == name);
            Assert.IsTrue(SegmentReachabilityValidator.ReturnRouteToExitExists(template),
                $"Шаблон '{name}': возврат от тайника за воротами к выходу сегмента невозможен даже при открытых воротах.");
        }

        // Владелец, задача «Стены вместо ловушек» (после разбора: забег
        // дошёл до Яруса 22 простым нажатием вперёд) — правило развёрнуто на
        // противоположное (см. doc-комментарий
        // SegmentReachabilityValidator.HasAvoidableRoute): сложность создают
        // стены и геометрия маршрута, а не количество ловушек. Через ЛЮБОЙ
        // шаблон каталога теперь ОБЯЗАН существовать путь, не задевающий ни
        // одной ловушки — старое требование (путь без ловушек не существует,
        // тир 2+) было буквально противоположным и уже принудило часть
        // старых шаблонов перекрыть весь ряд одним типом ловушки, чтобы
        // закрыть единственный обходной путь — эти шаблоны НЕ починены в
        // этой задаче (см. issue #275), только перечислены и покрыты
        // регрессионным списком ниже: они ЗНАЮТ, что не проходят новое
        // правило, и ждут отдельной задачи на починку раскладки, а не тихо
        // выпадают из проверки.
        //
        // Найдено прогоном по каталогу 2026-09-15 (issue #275, Задача 1):
        // 29 из 51 шаблона (включая AltarTemplate/BossTemplate) не
        // удовлетворяют новому правилу — все 29 содержат хотя бы одну
        // ловушку тира 2+, ряд/площадь которой перекрывает единственный
        // обходной путь. Список ниже — снимок на момент задачи, будущая
        // задача обязана либо чинить раскладку и убирать имя из списка,
        // либо (если владелец решит иначе) документировать почему нет.
        //
        // «караул» вычеркнут той же датой, ПОБОЧНЫМ эффектом ДРУГОЙ задачи
        // («награда никогда не лежит на ловушке»): 7 из 9 триггеров Бомбы
        // этого шаблона были механически удалены там же (см. её комментарий
        // в SegmentTemplateCatalog.cs) — как следствие, у шаблона снова
        // появился путь, не задевающий оставшиеся два. Список этой задачи
        // не редактировался ради самого списка — TemplatesWithoutAvoidableRoute_AreActuallyWithoutAvoidableRoute
        // ниже поймал бы расхождение и без этой правки; запись оставлена
        // как объяснение, а не как немая правка.
        private static readonly string[] TemplatesWithoutAvoidableRoute =
        {
            // тир 2
            "разлом", "коридор-со-стрелами", "пустой-обман", "развилка-цены",
            "ложная-прямая", "жадный-угол", "двойная-цена", "рой-стрел",
            "камни-в-нише", "клинки-по-контуру",
            // тир 3
            "коридор-с-лезвиями", "взрывной-проход", "коридор-лезвий",
            "такт-лезвий-волной", "мины-в-проходе", "разлив-у-развилки",
            // тир 4
            "смешанная-опасность", "решето", "выкуп",
            "стрелы-и-жила", "бомба-и-поток-лавы", "перекрёстный-огонь",
            "камнепад-и-лезвия",
            // тир 5
            "испытание", "щедрый-риск", "мост", "последний-рывок",
            "огненная-теснина",
        };

        [TestCaseSource(nameof(TemplateNamesExpectedToHaveAvoidableRoute))]
        public void Template_HasAvoidableRoute(string name)
        {
            var template = SegmentTemplateCatalog.All.Single(t => t.Name == name);
            Assert.IsTrue(SegmentReachabilityValidator.HasAvoidableRoute(template),
                $"Шаблон '{name}' (тир {template.DifficultyTier}) не имеет пути вход→выход, избегающего всех ловушек — новое правило избегаемости (задача «Стены вместо ловушек») требует, чтобы такой путь существовал всегда.");
        }

        private static string[] TemplateNamesExpectedToHaveAvoidableRoute() => SegmentTemplateCatalog.All
            .Where(t => !TemplatesWithoutAvoidableRoute.Contains(t.Name))
            .Select(t => t.Name)
            .ToArray();

        // Обратная сторона allowlist'а выше — сам список не должен тихо
        // устареть (шаблон переименовали/удалили, а имя осталось висеть
        // в allowlist, маскируя то, что реальный шаблон снова не
        // проверяется вовсе).
        [Test]
        public void TemplatesWithoutAvoidableRoute_AllNamesExistInCatalog()
        {
            foreach (var name in TemplatesWithoutAvoidableRoute)
                Assert.IsTrue(SegmentTemplateCatalog.All.Any(t => t.Name == name), $"'{name}' из allowlist больше не существует в каталоге.");
        }

        // Обратная гарантия: если раскладку шаблона из allowlist почистят в
        // будущей задаче так, что обходной путь появится, он обязан
        // вернуться под обычную проверку Template_HasAvoidableRoute, а не
        // остаться в allowlist по инерции, маскируя то, что чинить больше
        // нечего.
        [Test]
        public void TemplatesWithoutAvoidableRoute_AreActuallyWithoutAvoidableRoute()
        {
            foreach (var name in TemplatesWithoutAvoidableRoute)
            {
                var template = SegmentTemplateCatalog.All.Single(t => t.Name == name);
                Assert.IsFalse(SegmentReachabilityValidator.HasAvoidableRoute(template),
                    $"'{name}' в allowlist как «нет обходного пути», но на деле уже имеет — вычеркни из этого списка, теперь он покрывается обычной проверкой Template_HasAvoidableRoute.");
            }
        }

        // Задача «награда никогда не лежит на ловушке» (владелец, Спринт
        // «Стены вместо ловушек»): "Плита-источник Маны или Ключей не может
        // одновременно нести ловушку или её триггер." Прогон 2026-09-15
        // нашёл 22 конфликта в 8 шаблонах (все — Бомба/Стрела в радиусе/
        // ряду существующей награды) — все восемь починены сдвигом награды
        // на ближайшую безопасную клетку в ЭТОМ ЖЕ шаблоне (позиции
        // триггеров не трогались — исправление нарушенного инварианта, не
        // авторство раскладок). RewardTrapConflictValidatorTests.cs проверяет
        // саму проверку граничными случаями по каждому типу триггера;
        // этот тест — прогон по реальному каталогу, падает в CI, если
        // конфликт вернётся.
        [TestCaseSource(nameof(TemplateNames))]
        public void Template_HasNoRewardTrapConflicts(string name)
        {
            var template = SegmentTemplateCatalog.All.Single(t => t.Name == name);
            var conflicts = RewardTrapConflictValidator.FindConflicts(template);
            Assert.IsEmpty(conflicts,
                $"Шаблон '{name}': {string.Join("; ", conflicts)}");
        }

        private static string[] TemplateNames() => SegmentTemplateCatalog.All.Select(t => t.Name).ToArray();

        [Test]
        public void All_TemplateNamesAreUnique()
        {
            var names = SegmentTemplateCatalog.All.Select(t => t.Name).ToList();
            Assert.AreEqual(names.Distinct().Count(), names.Count);
        }

        [Test]
        public void All_CoversFullDifficultyRange()
        {
            var difficulties = SegmentTemplateCatalog.All.Select(t => t.DifficultyTier).Distinct().OrderBy(d => d).ToList();
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, difficulties);
        }

        [Test]
        public void All_CoversEveryRewardTag()
        {
            var tags = SegmentTemplateCatalog.All.Select(t => t.RewardTag).Distinct().ToList();
            CollectionAssert.AreEquivalent(
                new[] { SegmentRewardTag.Mana, SegmentRewardTag.Keys, SegmentRewardTag.Coins, SegmentRewardTag.Artifact },
                tags);
        }

        [Test]
        public void ManaVeinTemplate_ActuallyContainsManaSourceTiles()
        {
            var template = SegmentTemplateCatalog.All.Single(t => t.Name == "жила-маны");
            Assert.IsTrue(ContainsTileType(template, SegmentTileType.ManaSource));
        }

        // Владелец, 2026-09-04 («Алтари и вход в Комнату — убрать из
        // случайного пула»): оба шаблона больше не в All (детерминированный
        // поток вместо лотереи SegmentSelector, см. doc-комментарий
        // SegmentTemplateCatalog.AltarTemplate) — обращаемся к отдельным
        // полям, не к All.Single(...).
        [Test]
        public void AltarHallTemplate_ActuallyContainsAltarTile()
        {
            Assert.IsTrue(ContainsTileType(SegmentTemplateCatalog.AltarTemplate, SegmentTileType.Altar));
        }

        [Test]
        public void BossArenaTemplate_ActuallyContainsBossTile()
        {
            Assert.IsTrue(ContainsTileType(SegmentTemplateCatalog.BossTemplate, SegmentTileType.Boss));
        }

        // Правка по итогам ручной проверки владельца (2026-09-04): само
        // отсутствие Алтаря/Босса в случайном пуле — тоже критерий приёмки,
        // не только то, что отдельные поля существуют и содержат нужную роль.
        [Test]
        public void All_DoesNotContainAltarOrBossTemplates()
        {
            Assert.IsFalse(SegmentTemplateCatalog.All.Any(t => ContainsTileType(t, SegmentTileType.Altar)),
                "Алтарь должен ставиться только детерминированным потоком (SegmentRowProvider), не случайным отбором.");
            Assert.IsFalse(SegmentTemplateCatalog.All.Any(t => ContainsTileType(t, SegmentTileType.Boss)),
                "Вход в Комнату Босса должен ставиться только детерминированным потоком (SegmentRowProvider), не случайным отбором.");
        }

        // Владелец, 2026-09-05 («новые ловушки встречаются слишком редко»):
        // символы w/x/t/r/f были заведены (issues #213-#217), но сидели
        // всего в 3 шаблонах из тогдашних 43 (~7% каталога) — за забег
        // игрок их почти не видел. Ориентир владельца — примерно четверть
        // каталога содержит хотя бы одну ловушку нового поведения, и все
        // пять типов представлены (не просто общий процент за счёт одного
        // популярного типа).
        private static readonly SegmentTileType[] NewBehaviorTrapTypes =
        {
            SegmentTileType.ArrowWaveTrigger, SegmentTileType.BombTrigger, SegmentTileType.BladeTactTrigger,
            SegmentTileType.FallingRockTrigger, SegmentTileType.LavaWaveTrigger
        };

        [Test]
        public void All_AtLeastAQuarterOfTemplates_ContainANewBehaviorTrap()
        {
            var withNewTrap = SegmentTemplateCatalog.All.Count(t => NewBehaviorTrapTypes.Any(type => ContainsTileType(t, type)));
            var share = (double)withNewTrap / SegmentTemplateCatalog.All.Count;

            Assert.GreaterOrEqual(share, 0.2, $"Только {withNewTrap} из {SegmentTemplateCatalog.All.Count} шаблонов ({share:P0}) содержат ловушку нового поведения — меньше ориентира владельца (~четверть).");
        }

        [TestCaseSource(nameof(NewBehaviorTrapTypes))]
        public void All_EveryNewBehaviorTrapType_AppearsInAtLeastTwoTemplates(SegmentTileType type)
        {
            var count = SegmentTemplateCatalog.All.Count(t => ContainsTileType(t, type));
            Assert.GreaterOrEqual(count, 2, $"{type} встречается только в {count} шаблон(ах) — должен быть представлен в нескольких, не единожды.");
        }

        [Test]
        public void KeyScatterTemplate_ActuallyContainsKeySourceTiles()
        {
            var template = SegmentTemplateCatalog.All.Single(t => t.Name == "россыпь-ключей");
            Assert.IsTrue(ContainsTileType(template, SegmentTileType.KeySource));
        }

        // Владелец, 2026-09-04 («размер награды за Воротами»): тайники за
        // Воротами используют GateVaultKeySource (сумма из
        // GateVaultPurchases), не общий KeySource — все три шаблона с
        // Lever/LeverGate в каталоге переведены на новый тип.
        [TestCase("двор-с-рычагом", 1.0)]
        [TestCase("ворота-склада", 1.5)]
        [TestCase("рычаг-в-глубине", 1.5)]
        public void LeverTemplate_UsesGateVaultKeySource_WithExpectedPurchases(string name, double expectedPurchases)
        {
            var template = SegmentTemplateCatalog.All.Single(t => t.Name == name);

            Assert.IsTrue(ContainsTileType(template, SegmentTileType.GateVaultKeySource), $"Шаблон '{name}' должен использовать GateVaultKeySource, не обычный KeySource.");
            Assert.AreEqual(expectedPurchases, template.GateVaultPurchases);
        }

        private static bool ContainsTileType(SegmentTemplate template, SegmentTileType type)
        {
            for (var r = 0; r < template.RowCount; r++)
                for (var c = 0; c < template.Width; c++)
                    if (template.TileAt(r, c) == type)
                        return true;
            return false;
        }
    }
}
