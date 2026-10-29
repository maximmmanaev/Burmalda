using System;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class ArtifactTests
    {
        // Artifact абстрактен — тестируем через минимальный конкретный подкласс.
        private sealed class FakeArtifact : Artifact
        {
            public FakeArtifact(string id, string name, ArtifactCategory category)
                : base(id, name, category)
            {
            }
        }

        [Test]
        public void Constructor_SetsIdNameCategory()
        {
            var artifact = new FakeArtifact("a1", "Тестовый", ArtifactCategory.Amulet);

            Assert.AreEqual("a1", artifact.Id);
            Assert.AreEqual("Тестовый", artifact.Name);
            Assert.AreEqual(ArtifactCategory.Amulet, artifact.Category);
        }

        [TestCase(null)]
        [TestCase("")]
        public void Constructor_InvalidId_Throws(string id)
        {
            Assert.Throws<ArgumentException>(() => new FakeArtifact(id, "Тестовый", ArtifactCategory.Amulet));
        }
    }
}
