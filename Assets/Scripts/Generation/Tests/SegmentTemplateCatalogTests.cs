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
    }
}
