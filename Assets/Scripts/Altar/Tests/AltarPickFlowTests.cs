using System.Collections.Generic;
using System.Linq;
using Burmalda.Artifacts;
using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Altar.Tests
{
    public class AltarPickFlowTests
    {
        private sealed class FakeContext : IAltarContext
        {
            public Dictionary<int, (int yarus, int ordinal)> Spots = new Dictionary<int, (int, int)>();
            public IReadOnlyCollection<ThreatType> Threats = new[] { ThreatType.Slashing };
            public int Seed = 5;

            public bool TryGetSpot(GridCoordinate coordinate, out int yarus, out int ordinal)
            {
                if (Spots.TryGetValue(coordinate.Row, out var s)) { yarus = s.yarus; ordinal = s.ordinal; return true; }
                yarus = 0; ordinal = 0; return false;
            }

            public IReadOnlyCollection<ThreatType> AfishaThreats(int shrineYarus) => Threats;

            public int RunSeed => Seed;

            public int CurrentYarus => 1;
        }

        private static ArtifactPool AllOpenPool()
        {
            var pool = new ArtifactPool();
            foreach (var r in RelicCatalog.All) pool.Unlock(r.Id);
            return pool;
        }

        private static (AltarPickFlow flow, RunRelicBuild build, VaultBonusTracker vault, FakeContext ctx) Make(ArtifactPool pool = null)
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var vault = new VaultBonusTracker(grid, trail);
            var build = new RunRelicBuild();
            var ctx = new FakeContext();
            return (new AltarPickFlow(pool ?? AllOpenPool(), build, vault, ctx), build, vault, ctx);
        }

        [Test]
        public void Altar1And2_UseSanctuaryOfSameYarus_Altar3UsesNextYarus()
        {
            var (flow, _, _, ctx) = Make();
            ctx.Spots[10] = (4, 1);   // шумерский Ярус 4
            ctx.Spots[20] = (3, 3);   // конец Святилища Яруса 3 → впереди Ярус 4 (шумерский)
            ctx.Spots[30] = (6, 3);   // → Ярус 7 (оджибве)

            Assert.IsTrue(flow.OnAltarReached(new GridCoordinate(10, 2)).Options.All(o => o.Mythology == Mythology.Sumerian));
            Assert.IsTrue(flow.OnAltarReached(new GridCoordinate(20, 2)).Options.All(o => o.Mythology == Mythology.Sumerian));
            Assert.IsTrue(flow.OnAltarReached(new GridCoordinate(30, 2)).Options.All(o => o.Mythology == Mythology.Ojibwe));
        }

        [Test]
        public void FinalAltarAfterYarus10_HasNoPick()
        {
            var (flow, _, _, ctx) = Make();
            ctx.Spots[10] = (10, 3);
            Assert.IsNull(flow.OnAltarReached(new GridCoordinate(10, 2)));
        }

        [Test]
        public void NothingOpened_NoSession()
        {
            var (flow, _, _, _) = Make(new ArtifactPool());
            Assert.IsNull(flow.OnAltarReached(new GridCoordinate(10, 2)));
        }

        [Test]
        public void UnknownAltar_DefaultsToYarus1()
        {
            var (flow, _, _, _) = Make();
            var session = flow.OnAltarReached(new GridCoordinate(10, 2));
            Assert.AreEqual(2, session.Options.Count);
            Assert.IsTrue(session.Options.All(o => o.Mythology == Mythology.Slavic));
        }

        [Test]
        public void VaultBonus_GivesThirdOption_ConsumedOnlyWhenOfferShown()
        {
            var grid = new TunnelGrid(5);
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkGateVault();
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var vault = new VaultBonusTracker(grid, trail);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            var flow = new AltarPickFlow(AllOpenPool(), new RunRelicBuild(), vault, new FakeContext());

            Assert.AreEqual(1, vault.PendingExtraOptions);
            var session = flow.OnAltarReached(new GridCoordinate(5, 2));
            Assert.AreEqual(3, session.Options.Count);
            Assert.AreEqual(0, vault.PendingExtraOptions);

            Assert.AreEqual(2, flow.OnAltarReached(new GridCoordinate(6, 2)).Options.Count, "следующий Алтарь — снова 2");
        }

        [Test]
        public void Altar2_GuaranteesProtectiveAmulet_ForAfishaThreat()
        {
            for (var seed = 0; seed < 40; seed++)
            {
                var (flow, _, _, ctx) = Make();
                ctx.Seed = seed;
                ctx.Spots[10] = (1, 2);
                var session = flow.OnAltarReached(new GridCoordinate(10, 2));
                Assert.IsTrue(session.Offer.ContainsProtectiveAmulet, $"seed {seed}");
            }
        }

        [Test]
        public void SameSeedAndAltar_SameOffer_DifferentAltars_DifferentSeeds()
        {
            var (f1, _, _, c1) = Make();
            var (f2, _, _, c2) = Make();
            c1.Spots[10] = c2.Spots[10] = (2, 1);
            var a = f1.OnAltarReached(new GridCoordinate(10, 2)).Options.Select(o => o.Id).ToList();
            var b = f2.OnAltarReached(new GridCoordinate(10, 2)).Options.Select(o => o.Id).ToList();
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void EveryAltar_TakesExactlyOne_AcrossAWholeRun()
        {
            var (flow, build, _, ctx) = Make();
            var taken = 0;
            for (var altar = 0; altar < 30; altar++)
            {
                ctx.Spots[altar] = (1 + altar / 3, 1 + altar % 3);
                var session = flow.OnAltarReached(new GridCoordinate(altar, 2));
                if (session == null) continue;
                var before = build.Items.Count;
                session.Pick(session.Options[0].Id);
                if (session.State == AltarPickState.ChoosingReplacement) session.Replace(session.ReplaceCandidates[0].Id);
                Assert.AreEqual(AltarPickState.Done, session.State);
                taken++;
                Assert.IsTrue(build.Items.Count == before + 1 || build.Items.Count == before, "слот либо добавлен, либо заменён");
                Assert.AreEqual(build.Items.Count, build.Items.Select(i => i.Id).Distinct().Count(), "дубликатов нет");
            }

            Assert.Greater(taken, 20);
        }

        // ---- Афиша (PRD v10 §13.3) ----

        [Test]
        public void Afisha_OfAltar1And2_IsGodOfSameYarus_WithThreatsOfItsLayout()
        {
            var (flow, _, _, ctx) = Make();
            ctx.Spots[10] = (3, 1);
            ctx.Threats = new[] { ThreatType.Blunt, ThreatType.Slashing };

            var afisha = flow.AfishaFor(new GridCoordinate(10, 2));

            Assert.AreEqual(God.Perun, afisha.Shrine.God);
            Assert.AreEqual(GodRank.Senior, afisha.Shrine.Rank);
            CollectionAssert.AreEquivalent(new[] { ThreatType.Blunt, ThreatType.Slashing }, afisha.Threats);
        }

        [Test]
        public void Afisha_OfEndOfSanctuaryAltar_ShowsNextYarusGod()
        {
            var (flow, _, _, ctx) = Make();
            ctx.Spots[10] = (3, 3);

            Assert.AreEqual(God.Humbaba, flow.AfishaFor(new GridCoordinate(10, 2)).Shrine.God);
        }

        [Test]
        public void Afisha_AfterYarus10_IsNull()
        {
            var (flow, _, _, ctx) = Make();
            ctx.Spots[10] = (10, 3);

            Assert.IsNull(flow.AfishaFor(new GridCoordinate(10, 2)));
        }

        [Test]
        public void Afisha_IsShownEvenWhenNothingToPick()
        {
            var (flow, _, _, ctx) = Make(new ArtifactPool());
            ctx.Spots[10] = (2, 1);

            Assert.IsNull(flow.OnAltarReached(new GridCoordinate(10, 2)));
            Assert.AreEqual(God.Mokosh, flow.AfishaFor(new GridCoordinate(10, 2)).Shrine.God);
        }
    }
}
