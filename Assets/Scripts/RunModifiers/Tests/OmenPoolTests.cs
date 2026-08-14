using NUnit.Framework;

namespace Burmalda.RunModifiers.Tests
{
    public class OmenPoolTests
    {
        [Test]
        public void NewPool_NothingUnlocked()
        {
            var pool = new OmenPool();

            Assert.IsFalse(pool.IsUnlocked(OmenId.FragileVault));
            Assert.AreEqual(0, pool.UnlockedIds.Count);
        }

        [Test]
        public void Unlock_MarksOmenAsUnlocked()
        {
            var pool = new OmenPool();

            pool.Unlock(OmenId.FragileVault);

            Assert.IsTrue(pool.IsUnlocked(OmenId.FragileVault));
            Assert.IsFalse(pool.IsUnlocked(OmenId.HuntingPath));
        }

        [Test]
        public void Unlock_FiresUnlockedEventOnlyOnFirstCall()
        {
            var pool = new OmenPool();
            var firedCount = 0;
            OmenId? lastFired = null;
            pool.Unlocked += id =>
            {
                firedCount++;
                lastFired = id;
            };

            pool.Unlock(OmenId.HungryBoss);
            pool.Unlock(OmenId.HungryBoss); // повтор — не-op

            Assert.AreEqual(1, firedCount);
            Assert.AreEqual(OmenId.HungryBoss, lastFired);
        }
    }
}
