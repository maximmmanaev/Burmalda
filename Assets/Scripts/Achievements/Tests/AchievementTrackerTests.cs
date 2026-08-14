using Burmalda.Artifacts;
using NUnit.Framework;

namespace Burmalda.Achievements.Tests
{
    public class AchievementTrackerTests
    {
        [Test]
        public void Evaluate_ConditionNotMet_DoesNotGrantOrUnlock()
        {
            var pool = new ArtifactPool();
            var tracker = new AchievementTracker(pool);
            tracker.Register(new AchievementDefinition("a1", "Тест", "art1", () => false));

            tracker.Evaluate();

            Assert.IsFalse(tracker.IsGranted("a1"));
            Assert.IsFalse(pool.IsUnlocked("art1"));
        }

        [Test]
        public void Evaluate_ConditionMet_GrantsAndUnlocksArtifact()
        {
            var pool = new ArtifactPool();
            var tracker = new AchievementTracker(pool);
            tracker.Register(new AchievementDefinition("a1", "Тест", "art1", () => true));

            tracker.Evaluate();

            Assert.IsTrue(tracker.IsGranted("a1"));
            Assert.IsTrue(pool.IsUnlocked("art1"));
        }

        [Test]
        public void Evaluate_ConditionMet_FiresGrantedWithDefinition()
        {
            var pool = new ArtifactPool();
            var tracker = new AchievementTracker(pool);
            var definition = new AchievementDefinition("a1", "Тест", "art1", () => true);
            tracker.Register(definition);

            AchievementDefinition granted = null;
            tracker.Granted += d => granted = d;
            tracker.Evaluate();

            Assert.AreSame(definition, granted);
        }

        [Test]
        public void Evaluate_AlreadyGranted_DoesNotReGrantOrReFireEvenIfConditionStillTrue()
        {
            // Условие может оставаться истинным навсегда (напр. "Босс побеждён") —
            // повторный опрос не должен повторно выдавать/оповещать.
            var pool = new ArtifactPool();
            var tracker = new AchievementTracker(pool);
            tracker.Register(new AchievementDefinition("a1", "Тест", "art1", () => true));

            var firedCount = 0;
            tracker.Granted += _ => firedCount++;
            tracker.Evaluate();
            tracker.Evaluate();
            tracker.Evaluate();

            Assert.AreEqual(1, firedCount);
        }

        [Test]
        public void Evaluate_MultipleDefinitions_EachEvaluatedIndependently()
        {
            var pool = new ArtifactPool();
            var tracker = new AchievementTracker(pool);
            tracker.Register(new AchievementDefinition("a1", "Тест 1", "art1", () => true));
            tracker.Register(new AchievementDefinition("a2", "Тест 2", "art2", () => false));

            tracker.Evaluate();

            Assert.IsTrue(tracker.IsGranted("a1"));
            Assert.IsFalse(tracker.IsGranted("a2"));
        }

        [Test]
        public void MarkGranted_DoesNotUnlockArtifactOrFireGranted()
        {
            var pool = new ArtifactPool();
            var tracker = new AchievementTracker(pool);
            tracker.Register(new AchievementDefinition("a1", "Тест", "art1", () => true));

            var fired = false;
            tracker.Granted += _ => fired = true;
            tracker.MarkGranted("a1");

            Assert.IsTrue(tracker.IsGranted("a1"));
            Assert.IsFalse(pool.IsUnlocked("art1"));
            Assert.IsFalse(fired);
        }

        [Test]
        public void MarkGranted_PreventsSubsequentEvaluateFromGrantingAgain()
        {
            var pool = new ArtifactPool();
            var tracker = new AchievementTracker(pool);
            tracker.Register(new AchievementDefinition("a1", "Тест", "art1", () => true));
            tracker.MarkGranted("a1"); // напр. восстановлено из сохранения

            var fired = false;
            tracker.Granted += _ => fired = true;
            tracker.Evaluate();

            Assert.IsFalse(fired);
            Assert.IsFalse(pool.IsUnlocked("art1")); // пул восстанавливается отдельно (ProgressSnapshot)
        }

        [Test]
        public void Constructor_NullPool_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new AchievementTracker(null));
        }
    }
}
