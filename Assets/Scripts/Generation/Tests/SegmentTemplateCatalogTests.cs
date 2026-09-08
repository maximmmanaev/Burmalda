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

        // Владелец, 2026-09-08 («ловушек по-прежнему мало в ощущении»):
        // "плотность ловушечных плит подняли, плотность встреч — нет" —
        // одиночный триггер в сегменте 5×6 обходится по краю. Главное
        // правило — неизбежность, а не количество (см. doc-комментарий
        // SegmentReachabilityValidator.IsToothless). Прогон по каталогу
        // (2026-09-08) нашёл 42 из 42 тир 2+ шаблонов беззубыми — 31 уже
        // содержали ловушку, но обходимую, механически закрыты добавлением
        // ЕЩЁ плит того же уже существующего в шаблоне типа (позиции
        // существующих плит не переставлялись, только добавлялись новые —
        // где нужно было перекрыть весь ряд, ряд перекрыт целиком тем же
        // типом, не смесью). Оставшиеся 11 совсем не содержали ни одной
        // ловушки — вводить тип с нуля для них было бы авторством контента
        // (решение, КАКОЙ из пяти типов вводить), не механической правкой,
        // поэтому пропущены и вынесены в отдельный allowlist ниже: они
        // ЗНАЮТ, что не проходят это правило, и ждут решения владельца, а
        // не тихо выпадают из проверки.
        private static readonly string[] TemplatesWithoutAnyTrapType =
        {
            "зал-с-рычагом", "качели", "предбанник", // тир 2
            "богатый-карман", "ворота-склада", "двор-с-рычагом", "пещера-за-рычагом", "узкий-проход", // тир 3
            "рычаг-в-глубине", // тир 4
            "двойная-жила", "шахта-жадности", // тир 5
        };

        [TestCaseSource(nameof(Tier2AndAboveTemplateNamesWithATrapType))]
        public void Template_Tier2AndAbove_IsNotToothless(string name)
        {
            var template = SegmentTemplateCatalog.All.Single(t => t.Name == name);
            Assert.IsFalse(SegmentReachabilityValidator.IsToothless(template),
                $"Шаблон '{name}' (тир {template.DifficultyTier}) беззубый — существует путь вход→выход, не задевающий ни одной ловушки.");
        }

        private static string[] Tier2AndAboveTemplateNamesWithATrapType() => SegmentTemplateCatalog.All
            .Where(t => t.DifficultyTier >= 2 && !TemplatesWithoutAnyTrapType.Contains(t.Name))
            .Select(t => t.Name)
            .ToArray();

        // Обратная сторона allowlist выше — сам список не должен тихо
        // устареть (шаблон переименовали/удалили, а имя осталось висеть
        // в allowlist, маскируя то, что реальный шаблон снова не
        // проверяется вовсе).
        [Test]
        public void TemplatesWithoutAnyTrapType_AllNamesExistInCatalog()
        {
            foreach (var name in TemplatesWithoutAnyTrapType)
                Assert.IsTrue(SegmentTemplateCatalog.All.Any(t => t.Name == name), $"'{name}' из allowlist больше не существует в каталоге.");
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
