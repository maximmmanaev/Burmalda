using NUnit.Framework;

namespace Burmalda.Achievements.Tests
{
    public class AchievementDefinitionTests
    {
        [Test]
        public void Constructor_ValidArguments_ExposesGivenValues()
        {
            var definition = new AchievementDefinition("a1", "Тестовое достижение", "art1", () => true);

            Assert.AreEqual("a1", definition.Id);
            Assert.AreEqual("Тестовое достижение", definition.Name);
            Assert.AreEqual("art1", definition.UnlockArtifactId);
            Assert.IsTrue(definition.ConditionMet());
        }

        [TestCase(null, "name", "art1")]
        [TestCase("", "name", "art1")]
        [TestCase("id", null, "art1")]
        [TestCase("id", "", "art1")]
        [TestCase("id", "name", null)]
        [TestCase("id", "name", "")]
        public void Constructor_MissingStringArgument_Throws(string id, string name, string unlockArtifactId)
        {
            Assert.Throws<System.ArgumentException>(() => new AchievementDefinition(id, name, unlockArtifactId, () => true));
        }

        [Test]
        public void Constructor_NullCondition_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new AchievementDefinition("a1", "name", "art1", null));
        }
    }
}
