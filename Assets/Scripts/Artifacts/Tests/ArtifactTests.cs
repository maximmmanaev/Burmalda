using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class ArtifactTests
    {
        // Artifact абстрактен — тестируем через минимальный конкретный подкласс.
        private sealed class FakeArtifact : Artifact
        {
            public FakeArtifact(string id, string name, ArtifactCategory category, IReadOnlyList<ArtifactTag> tags = null)
                : base(id, name, category, tags)
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

        [Test]
        public void Constructor_NoTags_TagsIsEmpty()
        {
            var artifact = new FakeArtifact("a1", "Тестовый", ArtifactCategory.Idol);

            Assert.IsEmpty(artifact.Tags);
        }

        [Test]
        public void Constructor_WithTags_ExposesThem()
        {
            var artifact = new FakeArtifact("a1", "Тестовый", ArtifactCategory.Talisman,
                new[] { ArtifactTag.Mana, ArtifactTag.Greed });

            CollectionAssert.AreEqual(new[] { ArtifactTag.Mana, ArtifactTag.Greed }, artifact.Tags);
        }

        [TestCase(null)]
        [TestCase("")]
        public void Constructor_InvalidId_Throws(string id)
        {
            Assert.Throws<ArgumentException>(() => new FakeArtifact(id, "Тестовый", ArtifactCategory.Amulet));
        }
    }
}
