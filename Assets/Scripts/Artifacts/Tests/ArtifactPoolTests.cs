using System;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class ArtifactPoolTests
    {
        [Test]
        public void NewPool_NothingIsUnlocked()
        {
            var pool = new ArtifactPool();

            Assert.IsFalse(pool.IsUnlocked("a1"));
        }

        [Test]
        public void Unlock_MarksArtifactUnlocked()
        {
            var pool = new ArtifactPool();

            pool.Unlock("a1");

            Assert.IsTrue(pool.IsUnlocked("a1"));
        }

        [Test]
        public void Unlock_CalledTwice_StaysUnlockedAndFiresEventOnce()
        {
            var pool = new ArtifactPool();
            var fireCount = 0;
            pool.Unlocked += _ => fireCount++;

            pool.Unlock("a1");
            pool.Unlock("a1");

            Assert.AreEqual(1, fireCount);
        }

        [Test]
        public void UnlockedIds_ReflectsAllUnlockedArtifacts()
        {
            var pool = new ArtifactPool();

            pool.Unlock("a1");
            pool.Unlock("a2");

            CollectionAssert.AreEquivalent(new[] { "a1", "a2" }, pool.UnlockedIds);
        }

        [TestCase(null)]
        [TestCase("")]
        public void Unlock_InvalidId_Throws(string id)
        {
            var pool = new ArtifactPool();
            Assert.Throws<ArgumentException>(() => pool.Unlock(id));
        }
    }
}
