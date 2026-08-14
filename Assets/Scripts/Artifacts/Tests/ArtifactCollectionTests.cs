using System;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    public class ArtifactCollectionTests
    {
        [Test]
        public void NewCollection_DoesNotContainAnything()
        {
            var collection = new ArtifactCollection();

            Assert.IsFalse(collection.Contains("a1"));
        }

        [Test]
        public void Record_MarksArtifactAsContained()
        {
            var collection = new ArtifactCollection();

            collection.Record("a1");

            Assert.IsTrue(collection.Contains("a1"));
        }

        [Test]
        public void Record_CalledTwice_StaysRecordedAndFiresEventOnce()
        {
            var collection = new ArtifactCollection();
            var fireCount = 0;
            collection.Recorded += _ => fireCount++;

            collection.Record("a1");
            collection.Record("a1"); // "даже если сам инстанс исчезнет" — второй раз не переписывает историю

            Assert.IsTrue(collection.Contains("a1"));
            Assert.AreEqual(1, fireCount);
        }

        [Test]
        public void Record_RaisesRecordedWithArtifactId()
        {
            var collection = new ArtifactCollection();
            string seen = null;
            collection.Recorded += id => seen = id;

            collection.Record("a1");

            Assert.AreEqual("a1", seen);
        }

        [TestCase(null)]
        [TestCase("")]
        public void Record_InvalidId_Throws(string id)
        {
            var collection = new ArtifactCollection();
            Assert.Throws<ArgumentException>(() => collection.Record(id));
        }

        [Test]
        public void RecordedIds_ReflectsAllRecordedArtifacts()
        {
            // Понадобилось Persistence.ProgressSnapshot (issue #107) — без
            // этого свойства Capture() не компилировался (найдено при
            // написании e2e-теста core loop, issue #29).
            var collection = new ArtifactCollection();

            collection.Record("a1");
            collection.Record("a2");
            collection.Record("a1"); // повтор — не дублирует

            CollectionAssert.AreEquivalent(new[] { "a1", "a2" }, collection.RecordedIds);
        }
    }
}
