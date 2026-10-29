using System.Collections.Generic;
using System.Linq;
using Burmalda.Artifacts;
using NUnit.Framework;

namespace Burmalda.Altar.Tests
{
    public class AltarOfferGeneratorTests
    {
        private static readonly HashSet<string> AllOpen = new HashSet<string>(RelicCatalog.All.Select(r => r.Id));

        private static AltarOfferRequest Request(int shrineYarus = 1, HashSet<string> opened = null, RunRelicBuild build = null, int seed = 1) =>
            new AltarOfferRequest
            {
                ShrineYarus = shrineYarus,
                IsOpened = (opened ?? AllOpen).Contains,
                Build = build ?? new RunRelicBuild(),
                Seed = seed,
            };

        [Test]
        public void Default_TwoDistinctOptions()
        {
            for (var seed = 0; seed < 50; seed++)
            {
                var offer = AltarOfferGenerator.Generate(Request(seed: seed));
                Assert.AreEqual(2, offer.Options.Count, $"seed {seed}");
                Assert.AreEqual(2, offer.Options.Select(o => o.Id).Distinct().Count());
            }
        }

        [Test]
        public void Deterministic_BySeed()
        {
            var a = AltarOfferGenerator.Generate(Request(seed: 77)).Options.Select(o => o.Id).ToList();
            var b = AltarOfferGenerator.Generate(Request(seed: 77)).Options.Select(o => o.Id).ToList();
            CollectionAssert.AreEqual(a, b);
            var other = Enumerable.Range(0, 30).Select(s => string.Join(",", AltarOfferGenerator.Generate(Request(seed: s)).Options.Select(o => o.Id))).Distinct().Count();
            Assert.Greater(other, 5, "разные сиды дают разные предложения");
        }

        [Test]
        public void NeverOffers_RelicAlreadyInBuild()
        {
            var build = new RunRelicBuild();
            build.TryAdd(RelicCatalog.Get("relic-leshy-amulet"));
            build.TryAdd(RelicCatalog.Get("relic-perun-amulet"));
            for (var seed = 0; seed < 100; seed++)
            {
                var offer = AltarOfferGenerator.Generate(Request(1, build: build, seed: seed));
                Assert.IsFalse(offer.Options.Any(o => build.Contains(o.Id)), $"seed {seed}");
            }
        }

        [Test]
        public void MythologyPool_OfNearestShrine_IsUsed()
        {
            // Ярус 5 — шумерский мир.
            for (var seed = 0; seed < 40; seed++)
            {
                var offer = AltarOfferGenerator.Generate(Request(5, seed: seed));
                Assert.IsTrue(offer.Options.All(o => o.Mythology == Mythology.Sumerian), $"seed {seed}");
            }
        }

        [Test]
        public void FewerThanTwoInMythology_FillsFromAnyOpened()
        {
            var opened = new HashSet<string>(RelicCatalog.StarterIds) { "relic-humbaba-amulet" };
            for (var seed = 0; seed < 20; seed++)
            {
                var offer = AltarOfferGenerator.Generate(Request(4, opened, seed: seed)); // шумерский: открыт только Хумбаба
                Assert.AreEqual(2, offer.Options.Count);
                Assert.IsTrue(offer.Options.Any(o => o.Id == "relic-humbaba-amulet"), "своя мифология — в первую очередь");
            }
        }

        [Test]
        public void FirstAltar_OnlyStarterRelicsOpen_TwoOfThree()
        {
            var opened = new HashSet<string>(RelicCatalog.StarterIds);
            for (var seed = 0; seed < 20; seed++)
            {
                var offer = AltarOfferGenerator.Generate(Request(1, opened, seed: seed));
                Assert.AreEqual(2, offer.Options.Count);
                Assert.IsTrue(offer.Options.All(o => opened.Contains(o.Id)));
            }
        }

        [Test]
        public void OnlyOneOpenRelic_OffersJustIt_AndNothingOpen_OffersNothing()
        {
            Assert.AreEqual(1, AltarOfferGenerator.Generate(Request(1, new HashSet<string> { "relic-leshy-amulet" })).Options.Count);
            Assert.AreEqual(0, AltarOfferGenerator.Generate(Request(1, new HashSet<string>())).Options.Count);
        }

        // ---- Гарантия Амулета против угрозы Афиши ----

        private static AltarOfferRequest LastAltar(HashSet<string> opened = null, RunRelicBuild build = null, int seed = 1, bool already = false)
        {
            var r = Request(1, opened, build, seed);
            r.IsLastAltarBeforeShrine = true;
            r.ProtectiveAmuletAlreadyOffered = already;
            r.AfishaThreats = new[] { ThreatType.Slashing };
            return r;
        }

        [Test]
        public void LastAltar_GuaranteesAmuletAgainstAfishaThreat_WhenOpened()
        {
            for (var seed = 0; seed < 80; seed++)
            {
                var offer = AltarOfferGenerator.Generate(LastAltar(seed: seed));
                Assert.IsTrue(offer.ContainsProtectiveAmulet, $"seed {seed}");
                Assert.IsTrue(offer.Options.Any(o => o.Category == ArtifactCategory.Amulet && o.Protects.Contains(ThreatType.Slashing)), $"seed {seed}");
                Assert.AreEqual(2, offer.Options.Count);
            }
        }

        [Test]
        public void Guarantee_NotApplied_IfAlreadyOffered_OrBuildAlreadyProtected_OrNoneOpen()
        {
            Assert.IsFalse(AltarOfferGenerator.Generate(LastAltar(already: true)).ContainsProtectiveAmulet);

            var protectedBuild = new RunRelicBuild();
            protectedBuild.TryAdd(RelicCatalog.Get("relic-enlil-amulet")); // Slashing
            Assert.IsFalse(AltarOfferGenerator.Generate(LastAltar(build: protectedBuild)).ContainsProtectiveAmulet);

            var starters = new HashSet<string>(RelicCatalog.StarterIds); // Берестяной оберег защищает от Blunt, не Slashing
            Assert.IsFalse(AltarOfferGenerator.Generate(LastAltar(starters)).ContainsProtectiveAmulet);
        }

        [Test]
        public void Guarantee_NotApplied_OnNonLastAltar()
        {
            var r = Request();
            r.AfishaThreats = new[] { ThreatType.Slashing };
            // Без IsLastAltarBeforeShrine гарантия не действует (флаг может выпасть случайно, но не обязан).
            var any = Enumerable.Range(0, 60).Select(s => { r.Seed = s; return AltarOfferGenerator.Generate(r).ContainsProtectiveAmulet; });
            Assert.IsFalse(any.Any(x => x));
        }

        // ---- Третий вариант ----

        [Test]
        public void VaultBonus_GivesThreeOptions()
        {
            var r = Request();
            r.VaultBonus = true;
            Assert.AreEqual(3, AltarOfferGenerator.Generate(r).Options.Count);
        }

        [Test]
        public void ThirdOptionTalisman_UsesChance_AndNeverExceedsThree()
        {
            var build = new RunRelicBuild();
            build.TryAdd(RelicCatalog.Get(RelicCatalog.ThirdOptionTalismanId));
            var old = AltarOfferGenerator.ThirdOptionTalismanChance;
            try
            {
                AltarOfferGenerator.ThirdOptionTalismanChance = 1f;
                var always = AltarOfferGenerator.Generate(Request(build: build));
                Assert.AreEqual(3, always.Options.Count);
                Assert.IsTrue(always.ThirdFromTalisman);

                AltarOfferGenerator.ThirdOptionTalismanChance = 0f;
                Assert.AreEqual(2, AltarOfferGenerator.Generate(Request(build: build)).Options.Count);

                AltarOfferGenerator.ThirdOptionTalismanChance = 1f;
                var both = Request(build: build);
                both.VaultBonus = true;
                Assert.AreEqual(3, AltarOfferGenerator.Generate(both).Options.Count, "максимум 3");
            }
            finally
            {
                AltarOfferGenerator.ThirdOptionTalismanChance = old;
            }
        }

        [Test]
        public void WithoutTalisman_NoThirdOption()
        {
            for (var seed = 0; seed < 50; seed++)
                Assert.AreEqual(2, AltarOfferGenerator.Generate(Request(seed: seed)).Options.Count);
        }
    }
}
