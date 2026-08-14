using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class AmuletTalismanTests
    {
        [Test]
        public void Amulet_ExposesEffectDescriptionTagsAndCategory()
        {
            var amulet = new Amulet("am1", "Иммунитет к ловушкам", "Иммунитет на попадание двух стрел",
                new[] { ArtifactTag.Defense });

            Assert.AreEqual(ArtifactCategory.Amulet, amulet.Category);
            Assert.AreEqual("Иммунитет на попадание двух стрел", amulet.EffectDescription);
            CollectionAssert.AreEqual(new[] { ArtifactTag.Defense }, amulet.Tags);
        }

        [Test]
        public void Talisman_ExposesEffectDescriptionTagsAndCategory()
        {
            var talisman = new Talisman("t1", "Жила щедрости", "+10 Кристаллов Маны каждая 3-я плитка",
                new[] { ArtifactTag.Mana, ArtifactTag.Greed });

            Assert.AreEqual(ArtifactCategory.Talisman, talisman.Category);
            Assert.AreEqual("+10 Кристаллов Маны каждая 3-я плитка", talisman.EffectDescription);
            CollectionAssert.AreEqual(new[] { ArtifactTag.Mana, ArtifactTag.Greed }, talisman.Tags);
        }
    }
}
