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

        [Test]
        public void AltarHallTemplate_ActuallyContainsAltarTile()
        {
            var template = SegmentTemplateCatalog.All.Single(t => t.Name == "зал-алтаря");
            Assert.IsTrue(ContainsTileType(template, SegmentTileType.Altar));
        }

        [Test]
        public void BossArenaTemplate_ActuallyContainsBossTile()
        {
            var template = SegmentTemplateCatalog.All.Single(t => t.Name == "арена-босса");
            Assert.IsTrue(ContainsTileType(template, SegmentTileType.Boss));
        }

        [Test]
        public void KeyScatterTemplate_ActuallyContainsKeySourceTiles()
        {
            var template = SegmentTemplateCatalog.All.Single(t => t.Name == "россыпь-ключей");
            Assert.IsTrue(ContainsTileType(template, SegmentTileType.KeySource));
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
