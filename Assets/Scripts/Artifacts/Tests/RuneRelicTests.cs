using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class RuneRelicTests
    {
        [Test]
        public void Rune_ExposesLineAndCategory()
        {
            var rune = new Rune("r1", "Руна Силы", "сила");

            Assert.AreEqual(ArtifactCategory.Rune, rune.Category);
            Assert.AreEqual("сила", rune.Line);
        }

        [Test]
        public void Relic_HasRelicCategory()
        {
            var relic = new Relic("rel1", "Древняя Реликвия");

            Assert.AreEqual(ArtifactCategory.Relic, relic.Category);
        }
    }
}
