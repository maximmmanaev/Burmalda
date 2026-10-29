using System.Collections.Generic;
using System.Linq;
using Burmalda.Artifacts;
using NUnit.Framework;

namespace Burmalda.Altar.Tests
{
    public class AltarPickSessionTests
    {
        private static AltarOffer OfferOf(params string[] ids) =>
            new AltarOffer(ids.Select(RelicCatalog.Get).ToList(), false, false);

        [Test]
        public void Pick_WithFreeSlot_TakesExactlyOne_AndReportsAltarPick()
        {
            var build = new RunRelicBuild();
            var session = new AltarPickSession(OfferOf("relic-leshy-amulet", "relic-mokosh-talisman"), build, 1);
            var events = new List<AltarPickEvent>();
            session.Completed += events.Add;

            Assert.IsTrue(session.Pick("relic-mokosh-talisman"));

            Assert.AreEqual(AltarPickState.Done, session.State);
            Assert.AreEqual(1, build.Items.Count);
            Assert.IsTrue(build.Contains("relic-mokosh-talisman"));
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual("altar_pick", AltarPickEvent.Name);
            Assert.AreEqual("relic-mokosh-talisman", events[0].PickedId);
            Assert.IsNull(events[0].ReplacedId);
            Assert.AreEqual(1, events[0].Yarus);
            CollectionAssert.AreEqual(new[] { "relic-leshy-amulet", "relic-mokosh-talisman" }, events[0].OfferedIds);
        }

        [Test]
        public void Pick_Twice_OrUnknown_TakesNothingMore()
        {
            var build = new RunRelicBuild();
            var session = new AltarPickSession(OfferOf("relic-leshy-amulet", "relic-mokosh-talisman"), build, 1);
            var completed = 0;
            session.Completed += _ => completed++;

            Assert.IsFalse(session.Pick("relic-nope"));
            Assert.IsTrue(session.Pick("relic-leshy-amulet"));
            Assert.IsFalse(session.Pick("relic-mokosh-talisman"));

            Assert.AreEqual(1, build.Items.Count);
            Assert.AreEqual(1, completed);
        }

        [Test]
        public void FullSlots_RequireReplacement_ShowingOwnedItemsOfThatType()
        {
            var build = new RunRelicBuild();
            build.TryAdd(RelicCatalog.Get("relic-leshy-amulet"));
            build.TryAdd(RelicCatalog.Get("relic-perun-amulet"));
            build.TryAdd(RelicCatalog.Get("relic-mokosh-talisman"));
            var session = new AltarPickSession(OfferOf("relic-humbaba-amulet", "relic-enlil-rune"), build, 4);

            Assert.IsTrue(session.Pick("relic-humbaba-amulet"));

            Assert.AreEqual(AltarPickState.ChoosingReplacement, session.State);
            CollectionAssert.AreEquivalent(new[] { "relic-leshy-amulet", "relic-perun-amulet" }, session.ReplaceCandidates.Select(r => r.Id).ToList());
            Assert.AreEqual(3, build.Items.Count, "до подтверждения замены билд не меняется");
        }

        [Test]
        public void Replace_DropsChosenItem_AndCompletesWithReplacedId()
        {
            var build = new RunRelicBuild();
            build.TryAdd(RelicCatalog.Get("relic-leshy-amulet"));
            build.TryAdd(RelicCatalog.Get("relic-perun-amulet"));
            var session = new AltarPickSession(OfferOf("relic-humbaba-amulet", "relic-enlil-rune"), build, 4);
            AltarPickEvent evt = null;
            session.Completed += e => evt = e;

            session.Pick("relic-humbaba-amulet");
            Assert.IsFalse(session.Replace("relic-mokosh-talisman"), "нельзя вытеснить предмет, которого нет");
            Assert.IsTrue(session.Replace("relic-leshy-amulet"));

            Assert.AreEqual(AltarPickState.Done, session.State);
            Assert.IsTrue(build.Contains("relic-humbaba-amulet"));
            Assert.IsFalse(build.Contains("relic-leshy-amulet"));
            Assert.AreEqual(2, build.OfType(ArtifactCategory.Amulet).Count);
            Assert.AreEqual("relic-leshy-amulet", evt.ReplacedId);
            Assert.AreEqual(4, evt.Yarus);
        }

        [Test]
        public void BackToCards_ReturnsToChoosing_ButNothingIsTakenUntilConfirmed()
        {
            var build = new RunRelicBuild();
            build.TryAdd(RelicCatalog.Get("relic-leshy-amulet"));
            build.TryAdd(RelicCatalog.Get("relic-perun-amulet"));
            var session = new AltarPickSession(OfferOf("relic-humbaba-amulet", "relic-enlil-rune"), build, 4);
            var completed = 0;
            session.Completed += _ => completed++;

            session.Pick("relic-humbaba-amulet");
            Assert.IsTrue(session.BackToCards());

            Assert.AreEqual(AltarPickState.Choosing, session.State);
            Assert.AreEqual(0, completed);
            Assert.IsTrue(session.Pick("relic-enlil-rune"), "можно выбрать другую карточку, свободный слот Руны");
            Assert.AreEqual(1, completed);
        }

        [Test]
        public void Session_HasNoSkipOrCloseApi()
        {
            var names = typeof(AltarPickSession).GetMethods().Select(m => m.Name).ToList();
            CollectionAssert.DoesNotContain(names, "Skip");
            CollectionAssert.DoesNotContain(names, "Close");
            CollectionAssert.DoesNotContain(names, "Reroll");
            CollectionAssert.DoesNotContain(names, "Sell");
        }

        [Test]
        public void EmptyOffer_IsRejected()
        {
            Assert.Throws<System.ArgumentException>(() => new AltarPickSession(new AltarOffer(new RelicDefinition[0], false, false), new RunRelicBuild(), 1));
        }

        [Test]
        public void AltarPickEvent_ToString_IsReadable()
        {
            var e = new AltarPickEvent(2, new[] { "a", "b" }, "a", null);
            Assert.AreEqual("altar_pick yarus=2 offered=[a,b] picked=a replaced=-", e.ToString());
        }
    }
}
