using System.Collections.Generic;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class ResonanceCalculatorTests
    {
        private static IReadOnlyList<ArtifactTag> Tags(params ArtifactTag[] tags) => tags;

        [Test]
        public void Compute_NoArtifacts_ReturnsEmpty()
        {
            var result = ResonanceCalculator.Compute(new List<IReadOnlyList<ArtifactTag>>(), unityConditionMet: false);

            Assert.IsEmpty(result);
        }

        [Test]
        public void Compute_ThreeManaArtifacts_ActivatesVein()
        {
            var acquired = new List<IReadOnlyList<ArtifactTag>>
            {
                Tags(ArtifactTag.Mana), Tags(ArtifactTag.Mana), Tags(ArtifactTag.Mana)
            };

            var result = ResonanceCalculator.Compute(acquired, unityConditionMet: false);

            CollectionAssert.Contains(result, ResonanceType.Vein);
        }

        [Test]
        public void Compute_TwoManaArtifacts_DoesNotActivateVein()
        {
            var acquired = new List<IReadOnlyList<ArtifactTag>> { Tags(ArtifactTag.Mana), Tags(ArtifactTag.Mana) };

            var result = ResonanceCalculator.Compute(acquired, unityConditionMet: false);

            CollectionAssert.DoesNotContain(result, ResonanceType.Vein);
        }

        [Test]
        public void Compute_TwoKeysArtifacts_ActivatesKeyBond()
        {
            var acquired = new List<IReadOnlyList<ArtifactTag>> { Tags(ArtifactTag.Keys), Tags(ArtifactTag.Keys) };

            var result = ResonanceCalculator.Compute(acquired, unityConditionMet: false);

            CollectionAssert.Contains(result, ResonanceType.KeyBond);
        }

        [Test]
        public void Compute_TwoDefenseArtifacts_ActivatesWard()
        {
            var acquired = new List<IReadOnlyList<ArtifactTag>> { Tags(ArtifactTag.Defense), Tags(ArtifactTag.Defense) };

            var result = ResonanceCalculator.Compute(acquired, unityConditionMet: false);

            CollectionAssert.Contains(result, ResonanceType.Ward);
        }

        [Test]
        public void Compute_GreedAndMovementTags_ActivatesStingyMove()
        {
            var acquired = new List<IReadOnlyList<ArtifactTag>> { Tags(ArtifactTag.Greed), Tags(ArtifactTag.Movement) };

            var result = ResonanceCalculator.Compute(acquired, unityConditionMet: false);

            CollectionAssert.Contains(result, ResonanceType.StingyMove);
        }

        [Test]
        public void Compute_SingleArtifactWithBothGreedAndMovementTags_ActivatesStingyMove()
        {
            var acquired = new List<IReadOnlyList<ArtifactTag>> { Tags(ArtifactTag.Greed, ArtifactTag.Movement) };

            var result = ResonanceCalculator.Compute(acquired, unityConditionMet: false);

            CollectionAssert.Contains(result, ResonanceType.StingyMove);
        }

        [Test]
        public void Compute_UnityConditionMet_ActivatesUnityEvenWithNoArtifacts()
        {
            var result = ResonanceCalculator.Compute(new List<IReadOnlyList<ArtifactTag>>(), unityConditionMet: true);

            CollectionAssert.Contains(result, ResonanceType.Unity);
        }

        [Test]
        public void Compute_NeverReturnsMoreThanTwo()
        {
            // Keys(idx1)=KeyBond, Mana x3(idx2-4)=Vein, Defense(idx5-6)=Ward — все три условия выполняются.
            var acquired = new List<IReadOnlyList<ArtifactTag>>
            {
                Tags(ArtifactTag.Keys), Tags(ArtifactTag.Keys),
                Tags(ArtifactTag.Mana), Tags(ArtifactTag.Mana), Tags(ArtifactTag.Mana),
                Tags(ArtifactTag.Defense), Tags(ArtifactTag.Defense),
            };

            var result = ResonanceCalculator.Compute(acquired, unityConditionMet: false);

            Assert.LessOrEqual(result.Count, 2);
        }

        [Test]
        public void Compute_PrioritizesEarlierSatisfiedConditions()
        {
            // KeyBond выполняется на индексе 1, Vein — на индексе 4, Ward — на индексе 6.
            // Приоритет по порядку получения — KeyBond и Vein должны победить Ward.
            var acquired = new List<IReadOnlyList<ArtifactTag>>
            {
                Tags(ArtifactTag.Keys), Tags(ArtifactTag.Keys),
                Tags(ArtifactTag.Mana), Tags(ArtifactTag.Mana), Tags(ArtifactTag.Mana),
                Tags(ArtifactTag.Defense), Tags(ArtifactTag.Defense),
            };

            var result = ResonanceCalculator.Compute(acquired, unityConditionMet: false);

            CollectionAssert.AreEqual(new[] { ResonanceType.KeyBond, ResonanceType.Vein }, result);
        }
    }
}
