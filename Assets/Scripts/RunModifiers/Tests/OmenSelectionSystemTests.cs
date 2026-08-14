using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Burmalda.RunModifiers.Tests
{
    public class OmenSelectionSystemTests
    {
        [Test]
        public void OfferOmens_EmptyPool_ReturnsEmpty()
        {
            var pool = new OmenPool();
            var selection = new OmenSelectionSystem(pool, () => 0f);

            var offered = selection.OfferOmens();

            Assert.AreEqual(0, offered.Count);
        }

        [Test]
        public void OfferOmens_FewerUnlockedThanOfferedCount_ReturnsAllOfThem()
        {
            var pool = new OmenPool();
            pool.Unlock(OmenId.FragileVault);
            pool.Unlock(OmenId.HuntingPath);
            var selection = new OmenSelectionSystem(pool, () => 0f);

            var offered = selection.OfferOmens();

            Assert.AreEqual(2, offered.Count);
            CollectionAssert.AreEquivalent(new[] { OmenId.FragileVault, OmenId.HuntingPath }, offered);
        }

        [Test]
        public void OfferOmens_MoreUnlockedThanOfferedCount_ReturnsExactlyOfferedCountDistinctOmens()
        {
            var pool = new OmenPool();
            foreach (OmenId id in System.Enum.GetValues(typeof(OmenId))) pool.Unlock(id); // все 6

            var selection = new OmenSelectionSystem(pool, () => 0.5f);
            var offered = selection.OfferOmens();

            Assert.AreEqual(OmenSelectionSystem.OfferedCount, offered.Count);
            Assert.AreEqual(offered.Count, offered.Distinct().Count()); // без повторов
        }

        [Test]
        public void OfferOmens_RandomAlwaysZero_PicksFromStartWithoutIndexOutOfRange()
        {
            var pool = new OmenPool();
            foreach (OmenId id in System.Enum.GetValues(typeof(OmenId))) pool.Unlock(id);

            var selection = new OmenSelectionSystem(pool, () => 0f);

            Assert.DoesNotThrow(() => selection.OfferOmens());
        }

        [Test]
        public void OfferOmens_RandomAlwaysJustBelowOne_PicksFromEndWithoutIndexOutOfRange()
        {
            // Регрессия на классическую ошибку off-by-one: random01()=1
            // (граничный случай) не должен давать index == Count.
            var pool = new OmenPool();
            foreach (OmenId id in System.Enum.GetValues(typeof(OmenId))) pool.Unlock(id);

            var selection = new OmenSelectionSystem(pool, () => 0.999999f);

            IReadOnlyList<OmenId> offered = null;
            Assert.DoesNotThrow(() => offered = selection.OfferOmens());
            Assert.AreEqual(OmenSelectionSystem.OfferedCount, offered.Count);
        }

        [Test]
        public void Constructor_NullArguments_Throw()
        {
            Assert.Throws<System.ArgumentNullException>(() => new OmenSelectionSystem(null, () => 0f));
            Assert.Throws<System.ArgumentNullException>(() => new OmenSelectionSystem(new OmenPool(), null));
        }
    }
}
