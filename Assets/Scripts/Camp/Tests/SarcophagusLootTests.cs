using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Burmalda.Artifacts;
using Burmalda.Currencies;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.Camp.Tests
{
    public class SarcophagusLootTests
    {
        private double[] _partBase, _partStep;
        private double _relicBase, _relicStep, _choice;

        [SetUp]
        public void SaveConfig()
        {
            _partBase = (double[])SarcophagusConfig.PartBasePercent.Clone();
            _partStep = (double[])SarcophagusConfig.PartPityStepPercent.Clone();
            _relicBase = SarcophagusConfig.RelicBasePercent;
            _relicStep = SarcophagusConfig.RelicPityStepPercent;
            _choice = SarcophagusConfig.PartChoicePercent;
        }

        [TearDown]
        public void RestoreConfig()
        {
            SarcophagusConfig.PartBasePercent = _partBase;
            SarcophagusConfig.PartPityStepPercent = _partStep;
            SarcophagusConfig.RelicBasePercent = _relicBase;
            SarcophagusConfig.RelicPityStepPercent = _relicStep;
            SarcophagusConfig.PartChoicePercent = _choice;
        }

        private static ArtifactPool StarterPool()
        {
            var pool = new ArtifactPool();
            foreach (var id in RelicCatalog.StarterIds) pool.Unlock(id);
            return pool;
        }

        private static CollectionProgress AfterFirstPart() => new CollectionProgress { FirstPartGranted = true };

        private static void NoParts() { SarcophagusConfig.PartBasePercent = new[] { 0.0, 0.0, 0.0 }; SarcophagusConfig.PartPityStepPercent = new[] { 0.0, 0.0, 0.0 }; }

        private static void NoRelics() { SarcophagusConfig.RelicBasePercent = 0; SarcophagusConfig.RelicPityStepPercent = 0; }

        // ---- Редкость ----

        [Test]
        public void RarityRoll_FollowsTableByGodRank()
        {
            var expected = new[] { new[] { 80, 17, 3 }, new[] { 55, 35, 10 }, new[] { 25, 35, 40 } };
            var rng = new System.Random(123);
            for (var rank = 0; rank < 3; rank++)
            {
                var counts = new int[3];
                const int n = 20000;
                for (var i = 0; i < n; i++) counts[(int)SarcophagusRarityRoller.Roll((GodRank)rank, rng)]++;
                for (var r = 0; r < 3; r++)
                    Assert.AreEqual(expected[rank][r], counts[r] * 100.0 / n, 1.5, $"ранг {rank}, редкость {r}");
            }
        }

        [Test]
        public void Take_UsesGodAndRankOfYarus()
        {
            var s = SarcophagusRarityRoller.Take(6, new System.Random(1));
            Assert.AreEqual(God.Enlil, s.God);
            Assert.AreEqual(SarcophagusRank.Senior, s.Rank);
            Assert.AreEqual(SarcophagusRank.Junior, SarcophagusRarityRoller.Take(1, new System.Random(1)).Rank);
        }

        // ---- Монеты и первая часть ----

        [Test]
        public void FirstSarcophagus_AlwaysGivesRarePart_WhateverItsRarity_AndCoinsByRarity()
        {
            var progress = new CollectionProgress();
            var loot = SarcophagusLootRoller.Roll(new Sarcophagus(SarcophagusRank.Senior, RelicRarity.Legendary), progress, StarterPool(), new System.Random(5));

            Assert.AreEqual(SarcophagusLootKind.Part, loot.Kind);
            Assert.IsTrue(loot.FirstPartGift);
            Assert.AreEqual(RelicRarity.Rare, loot.Part.Rarity);
            Assert.AreEqual(1, progress.PartsOf(loot.Part.Id));
            Assert.IsTrue(progress.FirstPartGranted);
            Assert.AreEqual(SarcophagusConfig.CoinsFor(RelicRarity.Legendary), loot.Coins, "Монеты всегда, по редкости");
        }

        [Test]
        public void SecondSarcophagus_IsNotAGift()
        {
            var progress = new CollectionProgress();
            var pool = StarterPool();
            SarcophagusLootRoller.Roll(new Sarcophagus(SarcophagusRank.Junior), progress, pool, new System.Random(1));
            NoParts();
            NoRelics();

            var second = SarcophagusLootRoller.Roll(new Sarcophagus(SarcophagusRank.Junior), progress, pool, new System.Random(1));

            Assert.AreEqual(SarcophagusLootKind.CoinsOnly, second.Kind);
            Assert.IsFalse(second.FirstPartGift);
        }

        // ---- Часть: чья, защита от невезения ----

        [Test]
        public void Part_GoesIntoAlreadyStartedItem_UntilAssembled_ThenNextItem()
        {
            SarcophagusConfig.PartBasePercent = new[] { 100.0, 100.0, 100.0 };
            SarcophagusConfig.PartChoicePercent = 100; // выбор не должен предлагаться, пока есть начатый предмет
            var progress = AfterFirstPart();
            var started = CollectibleCatalog.Get("idol-shield");
            progress.SetParts(started.Id, 1);

            for (var i = 0; i < 3; i++)
            {
                var loot = SarcophagusLootRoller.Roll(new Sarcophagus(SarcophagusRank.Junior), progress, StarterPool(), new System.Random(i));
                Assert.AreEqual(started, loot.Part);
                Assert.IsFalse(loot.NeedsPartChoice);
            }

            Assert.AreEqual(4, progress.PartsOf(started.Id));
            Assert.IsTrue(progress.IsAssembled(started));
            var next = SarcophagusLootRoller.Roll(new Sarcophagus(SarcophagusRank.Junior), progress, StarterPool(), new System.Random(9));
            Assert.AreNotEqual(started, next.Part, "собранный предмет больше не получает частей");
        }

        [Test]
        public void NoStartedItem_NoChoice_RandomUnassembledItemOfThatRarity()
        {
            SarcophagusConfig.PartBasePercent = new[] { 100.0, 100.0, 100.0 };
            SarcophagusConfig.PartChoicePercent = 0;
            var loot = SarcophagusLootRoller.Roll(new Sarcophagus(SarcophagusRank.Middle, RelicRarity.Epic), AfterFirstPart(), StarterPool(), new System.Random(2));

            Assert.AreEqual(RelicRarity.Epic, loot.Part.Rarity);
            Assert.IsFalse(loot.NeedsPartChoice);
        }

        [Test]
        public void NoStartedItem_ChoiceOffered_2To3Items_PartNotAppliedUntilChosen()
        {
            SarcophagusConfig.PartBasePercent = new[] { 100.0, 100.0, 100.0 };
            SarcophagusConfig.PartChoicePercent = 100;
            var progress = AfterFirstPart();
            var loot = SarcophagusLootRoller.Roll(new Sarcophagus(SarcophagusRank.Junior), progress, StarterPool(), new System.Random(4));

            Assert.IsTrue(loot.NeedsPartChoice);
            Assert.That(loot.PartChoices.Count, Is.InRange(2, 3));
            Assert.IsTrue(loot.PartChoices.All(c => c.Rarity == RelicRarity.Rare));
            Assert.IsNull(loot.Part);
            Assert.AreEqual(0, progress.Parts.Values.Sum(), "часть ещё не уложена");
        }

        [Test]
        public void PartPity_RaisesChanceByStep_AndResetsOnHit()
        {
            SarcophagusConfig.PartBasePercent = new[] { 0.0, 0.0, 0.0 };
            SarcophagusConfig.PartPityStepPercent = new[] { 100.0, 100.0, 100.0 };
            NoRelics();
            var progress = AfterFirstPart();
            var pool = StarterPool();
            var s = new Sarcophagus(SarcophagusRank.Junior);

            var first = SarcophagusLootRoller.Roll(s, progress, pool, new System.Random(1));
            Assert.AreEqual(SarcophagusLootKind.CoinsOnly, first.Kind);
            Assert.AreEqual(1, progress.PartMisses(RelicRarity.Rare));

            var second = SarcophagusLootRoller.Roll(s, progress, pool, new System.Random(1));
            Assert.AreEqual(SarcophagusLootKind.Part, second.Kind);
            Assert.AreEqual(0, progress.PartMisses(RelicRarity.Rare), "сброс при выпадении");
        }

        [Test]
        public void PartPity_IsPerRarity()
        {
            NoParts();
            NoRelics();
            var progress = AfterFirstPart();
            SarcophagusLootRoller.Roll(new Sarcophagus(SarcophagusRank.Junior, RelicRarity.Epic), progress, StarterPool(), new System.Random(1));

            Assert.AreEqual(1, progress.PartMisses(RelicRarity.Epic));
            Assert.AreEqual(0, progress.PartMisses(RelicRarity.Rare));
        }

        [Test]
        public void AllItemsOfRarityAssembled_NoPartCheck_PityUntouched()
        {
            var progress = AfterFirstPart();
            foreach (var item in CollectibleCatalog.All.Where(i => i.Rarity == RelicRarity.Rare)) progress.SetParts(item.Id, item.PartsRequired);
            SarcophagusConfig.PartBasePercent = new[] { 100.0, 100.0, 100.0 };
            NoRelics();

            var loot = SarcophagusLootRoller.Roll(new Sarcophagus(SarcophagusRank.Junior), progress, StarterPool(), new System.Random(1));

            Assert.AreNotEqual(SarcophagusLootKind.Part, loot.Kind);
            Assert.AreEqual(0, progress.PartMisses(RelicRarity.Rare));
        }

        // ---- Реликвия ----

        [Test]
        public void Relic_OnlyIfNoPart_NeverDuplicate_PrefersGodThenMythologyThenAny()
        {
            NoParts();
            SarcophagusConfig.RelicBasePercent = 100;
            var progress = AfterFirstPart();
            var pool = StarterPool();

            var humbaba = SarcophagusLootRoller.Roll(new Sarcophagus(SarcophagusRank.Junior, RelicRarity.Rare, God.Humbaba), progress, pool, new System.Random(1));
            Assert.AreEqual("relic-humbaba-amulet", humbaba.Relic.Id, "сначала бог этого Святилища");
            pool.Unlock(humbaba.Relic.Id);

            var leshy = SarcophagusLootRoller.Roll(new Sarcophagus(SarcophagusRank.Junior, RelicRarity.Rare, God.Leshy), progress, pool, new System.Random(1));
            Assert.AreEqual(Mythology.Slavic, leshy.Relic.Mythology, "у Лешего Редких не осталось — его мифология");
            Assert.AreEqual("relic-chernobog-amulet", leshy.Relic.Id);
            pool.Unlock(leshy.Relic.Id);

            var any = SarcophagusLootRoller.Roll(new Sarcophagus(SarcophagusRank.Junior, RelicRarity.Rare, God.Leshy), progress, pool, new System.Random(1));
            Assert.AreNotEqual(Mythology.Slavic, any.Relic.Mythology, "славянских Редких не осталось — любая");
            Assert.IsFalse(pool.IsUnlocked(any.Relic.Id));
        }

        [Test]
        public void Relic_Pity_RaisesChance_AndResets()
        {
            NoParts();
            SarcophagusConfig.RelicBasePercent = 0;
            SarcophagusConfig.RelicPityStepPercent = 100;
            var progress = AfterFirstPart();
            var pool = StarterPool();
            var s = new Sarcophagus(SarcophagusRank.Junior);

            Assert.AreEqual(SarcophagusLootKind.CoinsOnly, SarcophagusLootRoller.Roll(s, progress, pool, new System.Random(1)).Kind);
            Assert.AreEqual(1, progress.RelicMisses(RelicRarity.Rare));
            Assert.AreEqual(SarcophagusLootKind.Relic, SarcophagusLootRoller.Roll(s, progress, pool, new System.Random(1)).Kind);
            Assert.AreEqual(0, progress.RelicMisses(RelicRarity.Rare));
        }

        [Test]
        public void AllRelicsOfRarityOpened_NoRelicCheck()
        {
            NoParts();
            SarcophagusConfig.RelicBasePercent = 100;
            var progress = AfterFirstPart();
            var pool = new ArtifactPool();
            foreach (var r in RelicCatalog.All.Where(x => x.Rarity == RelicRarity.Rare)) pool.Unlock(r.Id);

            var loot = SarcophagusLootRoller.Roll(new Sarcophagus(SarcophagusRank.Junior), progress, pool, new System.Random(1));

            Assert.AreEqual(SarcophagusLootKind.CoinsOnly, loot.Kind);
            Assert.AreEqual(0, progress.RelicMisses(RelicRarity.Rare));
        }

        [Test]
        public void PartHit_SkipsRelicCheck_RelicPityUntouched()
        {
            SarcophagusConfig.PartBasePercent = new[] { 100.0, 100.0, 100.0 };
            SarcophagusConfig.RelicBasePercent = 100;
            var progress = AfterFirstPart();
            progress.SetRelicMisses(RelicRarity.Rare, 7);

            var loot = SarcophagusLootRoller.Roll(new Sarcophagus(SarcophagusRank.Junior), progress, StarterPool(), new System.Random(1));

            Assert.AreEqual(SarcophagusLootKind.Part, loot.Kind);
            Assert.AreEqual(7, progress.RelicMisses(RelicRarity.Rare));
        }

        // ---- Camp.OpenNext ----

        [Test]
        public void Camp_OpenNext_UnlocksRelicInPool_SoItJoinsAltarOffers()
        {
            NoParts();
            SarcophagusConfig.RelicBasePercent = 100;
            var pool = StarterPool();
            var camp = new Camp(new PersistentWallet(), pool, AfterFirstPart(), new System.Random(1));
            camp.AddUnopened(new[] { new Sarcophagus(SarcophagusRank.Junior, RelicRarity.Rare, God.Humbaba) });

            var contents = camp.OpenNext().Value;

            Assert.AreEqual(SarcophagusLootKind.Relic, contents.Loot.Kind);
            Assert.IsTrue(pool.IsUnlocked(contents.Loot.Relic.Id), "открыта навсегда");
        }

        [Test]
        public void Camp_PartChoice_BlocksNextOpening_UntilChosen_ThenPartApplied()
        {
            SarcophagusConfig.PartBasePercent = new[] { 100.0, 100.0, 100.0 };
            SarcophagusConfig.PartChoicePercent = 100;
            var camp = new Camp(new PersistentWallet(), StarterPool(), AfterFirstPart(), new System.Random(3));
            camp.AddUnopened(new[] { new Sarcophagus(SarcophagusRank.Junior), new Sarcophagus(SarcophagusRank.Junior) });

            var first = camp.OpenNext().Value;
            Assert.IsTrue(first.Loot.NeedsPartChoice);
            Assert.IsNotNull(camp.PendingPartChoice);
            Assert.IsNull(camp.OpenNext(), "пока не выбрал — следующий не открывается");
            Assert.IsFalse(camp.ChoosePart("totem-migizi"), "нельзя выбрать то, чего не предлагали");

            var pick = first.Loot.PartChoices[0];
            Assert.IsTrue(camp.ChoosePart(pick.Id));
            Assert.AreEqual(1, camp.Progress.PartsOf(pick.Id));
            Assert.IsNull(camp.PendingPartChoice);
            Assert.IsNotNull(camp.OpenNext());
        }

        // ---- Симуляция: ~200 Саркофагов на ранг ----

        [Test]
        public void Simulation_200SarcophagiPerRank_KeepsInvariants_AndPrintsTable()
        {
            var table = new StringBuilder("ранг | Редк/Эпич/Легенд | части | Реликвии | только Монеты | срабатываний защиты (части/Реликвии)\n");
            foreach (var rank in new[] { GodRank.Junior, GodRank.Middle, GodRank.Senior })
            {
                var rng = new System.Random(1000 + (int)rank);
                var progress = new CollectionProgress();
                var pool = StarterPool();
                var rarities = new int[3];
                int parts = 0, relics = 0, coinsOnly = 0, pityPartHits = 0, pityRelicHits = 0;
                for (var i = 0; i < 200; i++)
                {
                    var god = GodTable.All.First(g => g.Rank == rank).God;
                    var sarcophagus = new Sarcophagus((SarcophagusRank)(int)rank, SarcophagusRarityRoller.Roll(rank, rng), god);
                    var partMissesBefore = progress.PartMisses(sarcophagus.Rarity);
                    var relicMissesBefore = progress.RelicMisses(sarcophagus.Rarity);
                    var wasFirst = !progress.FirstPartGranted;
                    var partsBefore = progress.Parts.Values.Sum();

                    var loot = SarcophagusLootRoller.Roll(sarcophagus, progress, pool, rng);
                    rarities[(int)sarcophagus.Rarity]++;
                    switch (loot.Kind)
                    {
                        case SarcophagusLootKind.Part:
                            parts++;
                            if (!wasFirst && partMissesBefore > 0) pityPartHits++;
                            if (loot.NeedsPartChoice) progress.AddPart(loot.PartChoices[0].Id);
                            Assert.AreEqual(partsBefore + 1, progress.Parts.Values.Sum(), "не больше одной части за Саркофаг");
                            break;
                        case SarcophagusLootKind.Relic:
                            relics++;
                            if (relicMissesBefore > 0) pityRelicHits++;
                            Assert.IsFalse(pool.IsUnlocked(loot.Relic.Id), "дубликатов Реликвий нет");
                            pool.Unlock(loot.Relic.Id);
                            break;
                        default:
                            coinsOnly++;
                            break;
                    }

                    if (i == 0) Assert.AreEqual(SarcophagusLootKind.Part, loot.Kind, "первый Саркофаг всегда даёт часть");
                }

                foreach (var item in CollectibleCatalog.All) Assert.LessOrEqual(progress.PartsOf(item.Id), item.PartsRequired, item.Id);
                table.AppendLine($"{rank} | {rarities[0]}/{rarities[1]}/{rarities[2]} | {parts} | {relics} | {coinsOnly} | {pityPartHits}/{pityRelicHits}");
                Assert.AreEqual(200, parts + relics + coinsOnly);
            }

            Debug.Log("Симуляция Саркофагов (по 200 на ранг):\n" + table);
        }
    }
}
