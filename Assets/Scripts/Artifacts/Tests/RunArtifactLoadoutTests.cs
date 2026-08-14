using System;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class RunArtifactLoadoutTests
    {
        [Test]
        public void NewLoadout_AcquiredIsEmpty()
        {
            var loadout = new RunArtifactLoadout(new ArtifactCollection());

            Assert.IsEmpty(loadout.Acquired);
        }

        [Test]
        public void Acquire_Amulet_AddsToAcquiredInOrder()
        {
            var loadout = new RunArtifactLoadout(new ArtifactCollection());
            var amulet = new Amulet("am1", "А", "эффект", new[] { ArtifactTag.Defense });
            var talisman = new Talisman("t1", "Т", "эффект", new[] { ArtifactTag.Mana });

            loadout.Acquire(amulet);
            loadout.Acquire(talisman);

            CollectionAssert.AreEqual(new Artifact[] { amulet, talisman }, loadout.Acquired);
        }

        [Test]
        public void Acquire_RecordsArtifactIntoCollection()
        {
            var collection = new ArtifactCollection();
            var loadout = new RunArtifactLoadout(collection);
            var amulet = new Amulet("am1", "А", "эффект", new[] { ArtifactTag.Defense });

            loadout.Acquire(amulet);

            Assert.IsTrue(collection.Contains("am1"));
        }

        [Test]
        public void Acquire_NonAmuletNonTalisman_Throws()
        {
            var loadout = new RunArtifactLoadout(new ArtifactCollection());
            var idol = new Idol("i1", "Идол");

            Assert.Throws<ArgumentException>(() => loadout.Acquire(idol));
        }

        [Test]
        public void ActiveResonances_DelegatesToResonanceCalculator()
        {
            var loadout = new RunArtifactLoadout(new ArtifactCollection());
            loadout.Acquire(new Talisman("t1", "Т1", "э", new[] { ArtifactTag.Keys }));
            loadout.Acquire(new Talisman("t2", "Т2", "э", new[] { ArtifactTag.Keys }));

            var resonances = loadout.ActiveResonances();

            CollectionAssert.Contains(resonances, ResonanceType.KeyBond);
        }

        [Test]
        public void Remove_AcquiredArtifact_RemovesFromAcquiredAndReturnsTrue()
        {
            var loadout = new RunArtifactLoadout(new ArtifactCollection());
            var talisman = new Talisman("t1", "Т", "э", new[] { ArtifactTag.Mana });
            loadout.Acquire(talisman);

            var removed = loadout.Remove(talisman);

            Assert.IsTrue(removed);
            Assert.IsEmpty(loadout.Acquired);
        }

        [Test]
        public void Remove_NotAcquiredArtifact_ReturnsFalse()
        {
            var loadout = new RunArtifactLoadout(new ArtifactCollection());
            var talisman = new Talisman("t1", "Т", "э", new[] { ArtifactTag.Mana });

            Assert.IsFalse(loadout.Remove(talisman));
        }

        [Test]
        public void Remove_DoesNotUnrecordFromCollection()
        {
            var collection = new ArtifactCollection();
            var loadout = new RunArtifactLoadout(collection);
            var talisman = new Talisman("t1", "Т", "э", new[] { ArtifactTag.Mana });
            loadout.Acquire(talisman);

            loadout.Remove(talisman);

            Assert.IsTrue(collection.Contains("t1"), "запись в Коллекции остаётся навсегда, даже если инстанс убран");
        }
    }
}
