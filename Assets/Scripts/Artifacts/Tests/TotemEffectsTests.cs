using Burmalda.Core;
using Burmalda.FateRoll;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Artifacts.Tests
{
    /// <summary>Эффекты пяти Тотемов, 3 уровня, по одному тесту на эффект и уровень (PRD v10 §19).</summary>
    public class TotemEffectsTests
    {
        private static TotemEffects With(string id, int level, int yarus = 1)
        {
            var p = new CollectionProgress();
            var item = CollectibleCatalog.Get(id);
            p.SetParts(id, item.PartsRequired);
            p.TrySelect(id);
            p.SetLevel(id, level);
            return new TotemEffects(p, () => yarus);
        }

        [Test]
        public void NoTotem_AllNeutral()
        {
            var e = new TotemEffects(new CollectionProgress(), () => 1);

            Assert.AreEqual(1f, e.DecayRateMultiplier);
            Assert.AreEqual(1f, e.SourceManaMultiplier);
            Assert.AreEqual(0, e.FateBonus);
            Assert.AreEqual(0, e.MaangMaxGroup);
            Assert.IsFalse(e.KnightMoveAvailable(0));
        }

        // ---- Маанг ----

        private static (TunnelGrid grid, GridTraceTrail trail) Field()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            for (var r = 0; r <= 12; r++) for (var c = 0; c < 5; c++) grid.GetOrCreateTile(new GridCoordinate(r, c));
            grid.GetOrCreateTile(new GridCoordinate(1, 0)).MarkFallingRockTrigger(new GridCoordinate(2, 0));
            grid.GetOrCreateTile(new GridCoordinate(2, 1)).MarkMovingWallTrigger(3, MovingWallMode.Both);
            grid.GetOrCreateTile(new GridCoordinate(3, 1)).MarkMovingWallTrigger(4, MovingWallMode.FromLeft);
            grid.GetOrCreateTile(new GridCoordinate(4, 1)).MarkArrowWaveTrigger(5, RowWaveDirection.LeftToRight);
            grid.GetOrCreateTile(new GridCoordinate(5, 3)).MarkBladeTactTrigger(6);
            grid.GetOrCreateTile(new GridCoordinate(6, 3)).MarkBombTrigger();
            return (grid, trail);
        }

        [Test]
        public void Maang_L1_RevealsTriggersAndTypeOfGroup1_Only()
        {
            var (grid, trail) = Field();
            using var reveal = new TotemRevealSystem(grid, trail, With(TotemEffects.Maang, 1));

            foreach (var c in new[] { new GridCoordinate(1, 0), new GridCoordinate(2, 1), new GridCoordinate(3, 1), new GridCoordinate(4, 1) })
                Assert.IsTrue(grid.GetOrCreateTile(c).IsTrapTypeRevealed, "группа 1 — Камень, Давилка, Стены, Стрелы: " + c);
            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(5, 3)).IsTrapTypeRevealed, "Лезвия — группа 2");
            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(6, 3)).IsTrapTypeRevealed, "Бомба — группа 2");
        }

        [Test]
        public void Maang_L2_AddsBladesAndBomb()
        {
            var (grid, trail) = Field();
            using var reveal = new TotemRevealSystem(grid, trail, With(TotemEffects.Maang, 2));

            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(5, 3)).IsTrapTypeRevealed);
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(6, 3)).IsTrapTypeRevealed);
        }

        [Test]
        public void Maang_L3_Groups_FireJetBearTrapBoulder_Hooks()
        {
            Assert.AreEqual(1, TrapGroups.GroupOf(TrapKind.Arrows));
            Assert.AreEqual(2, TrapGroups.GroupOf(TrapKind.FireJet), "Огненная струя — группа 2 (ур. 2)");
            Assert.AreEqual(3, TrapGroups.GroupOf(TrapKind.BearTrap), "Капкан — группа 3 (ур. 3)");
            Assert.AreEqual(3, TrapGroups.GroupOf(TrapKind.Boulder), "Валун — группа 3 (ур. 3)");
            Assert.AreEqual(3, With(TotemEffects.Maang, 3).MaangMaxGroup);
            Assert.AreEqual(2, With(TotemEffects.Maang, 2).MaangMaxGroup);
        }

        [Test]
        public void Maang_RevealsAheadAsPlayerAdvances()
        {
            var (grid, trail) = Field();
            grid.GetOrCreateTile(new GridCoordinate(12, 2)).MarkBombTrigger();
            using var reveal = new TotemRevealSystem(grid, trail, With(TotemEffects.Maang, 2));
            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(12, 2)).IsTrapTypeRevealed, "дальше горизонта");

            for (var r = 1; r <= 5; r++) trail.TryAdvanceTo(new GridCoordinate(r, 2));

            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(12, 2)).IsTrapTypeRevealed);
        }

        // ---- Амик ----

        [Test]
        public void Amik_L1_TilesDecaySlower() => Assert.Less(With(TotemEffects.Amik, 1).DecayRateMultiplier, 1f);

        [Test]
        public void Amik_L2_CrusherAndMovingWallsCloseSlower()
        {
            Assert.AreEqual(1f, With(TotemEffects.Amik, 1).WallCloseSlowdown);
            Assert.Greater(With(TotemEffects.Amik, 2).WallCloseSlowdown, 1f);
        }

        [Test]
        public void Amik_L3_SanctuaryRowsCollapseSlower()
        {
            Assert.AreEqual(1f, With(TotemEffects.Amik, 2).CollapseSpeedMultiplier);
            Assert.Less(With(TotemEffects.Amik, 3).CollapseSpeedMultiplier, 1f);
        }

        // ---- Ваагош ----

        [Test]
        public void Waagosh_L1_ManaSourcesGiveMore()
        {
            Assert.Greater(With(TotemEffects.Waagosh, 1).SourceManaMultiplier, 1f);
            Assert.AreEqual(1f, With(TotemEffects.Waagosh, 1).VeinManaMultiplier);
        }

        [Test]
        public void Waagosh_L2_SanctuaryVeinsGiveMore() => Assert.Greater(With(TotemEffects.Waagosh, 2).VeinManaMultiplier, 1f);

        [Test]
        public void Waagosh_L3_RevealsGateVault_Hook()
        {
            Assert.IsFalse(With(TotemEffects.Waagosh, 2).RevealsGateVault);
            Assert.IsTrue(With(TotemEffects.Waagosh, 3).RevealsGateVault);
        }

        // ---- Ваабижеши ----

        private static (TunnelGrid grid, GridTraceTrail trail) Open()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(2, 2));
            for (var r = 0; r <= 8; r++) for (var c = 0; c < 5; c++) grid.GetOrCreateTile(new GridCoordinate(r, c));
            return (grid, trail);
        }

        [Test]
        public void Waabizheshi_L1_OneKnightMovePerYarus()
        {
            var yarus = 1;
            var p = new CollectionProgress();
            p.SetParts(TotemEffects.Waabizheshi, 5);
            p.TrySelect(TotemEffects.Waabizheshi);
            var e = new TotemEffects(p, () => yarus);
            var (grid, trail) = Open();

            Assert.IsFalse(e.TryKnightMove(grid, trail, new GridCoordinate(3, 3), 0), "не ход конём");
            Assert.IsTrue(e.TryKnightMove(grid, trail, new GridCoordinate(4, 3), 0));
            Assert.AreEqual(new GridCoordinate(4, 3), trail.CurrentPosition);
            Assert.IsFalse(e.TryKnightMove(grid, trail, new GridCoordinate(6, 4), 0), "раз за Ярус");

            yarus = 2;
            Assert.IsTrue(e.TryKnightMove(grid, trail, new GridCoordinate(6, 4), 0), "на новом Ярусе снова");
        }

        [Test]
        public void Waabizheshi_L2_LandingDoesNotTriggerTrap()
        {
            var e = With(TotemEffects.Waabizheshi, 2);
            var (grid, trail) = Open();
            Assert.IsTrue(e.TryKnightMove(grid, trail, new GridCoordinate(4, 3), 0));

            Assert.IsFalse(e.StepsActivateTraps);
            e.OnStepTaken();
            Assert.IsTrue(e.StepsActivateTraps);
            Assert.IsTrue(With(TotemEffects.Waabizheshi, 1).StepsActivateTraps, "ур. 1 ловушки не подавляет");
        }

        [Test]
        public void Waabizheshi_L3_AvailableEveryNStreakTiles()
        {
            var e = With(TotemEffects.Waabizheshi, 3);
            var (grid, trail) = Open();
            Assert.IsTrue(e.TryKnightMove(grid, trail, new GridCoordinate(4, 3), streak: 4));

            Assert.IsFalse(e.KnightMoveAvailable(4 + TotemEffects.KnightMoveEveryStreakTiles - 1));
            Assert.IsTrue(e.KnightMoveAvailable(4 + TotemEffects.KnightMoveEveryStreakTiles));
        }

        [Test]
        public void KnightMove_CannotLandOnWallOrLava()
        {
            var e = With(TotemEffects.Waabizheshi, 1);
            var (grid, trail) = Open();
            grid.GetOrCreateTile(new GridCoordinate(4, 3)).MarkBlocked();
            grid.GetOrCreateTile(new GridCoordinate(4, 1)).MarkLethalTrap(LethalTrapType.Lava);

            Assert.IsFalse(e.TryKnightMove(grid, trail, new GridCoordinate(4, 3), 0));
            Assert.IsFalse(e.TryKnightMove(grid, trail, new GridCoordinate(4, 1), 0));
            Assert.IsTrue(e.KnightMoveAvailable(0), "неудачная попытка заряд не тратит");
        }

        // ---- Мигизи ----

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Migizi_AddsLevelToFateRollSum(int level)
        {
            var bonus = With(TotemEffects.Migizi, level).FateBonus;

            Assert.AreEqual(level, bonus);
            // Бросок I: «на месте» — сумма ≥ 12 или 6+6; 5+5 = 10 без бонуса — к Алтарю, при +2 и выше — на месте.
            var without = FateRollRules.Evaluate(1, 5, 5, 0);
            var with = FateRollRules.Evaluate(1, 5, 5, bonus);
            Assert.AreEqual(FateOutcome.ToAltar, without);
            Assert.AreEqual(level >= 2 ? FateOutcome.InPlace : FateOutcome.ToAltar, with);
        }

        [Test]
        public void Migizi_ImprovesDisplayedChances_NaturalSixesAndSnakeEyesRulesKept()
        {
            var baseline = FateRollRules.Chances(1, 0);
            var plus3 = FateRollRules.Chances(1, With(TotemEffects.Migizi, 3).FateBonus);

            Assert.Less(plus3.DeathPercent, baseline.DeathPercent);
            Assert.Greater(plus3.InPlacePercent, baseline.InPlacePercent);
            Assert.AreEqual(FateOutcome.Death, FateRollRules.Evaluate(1, 1, 1, 3), "1+1 — всегда смерть, даже с +3");
            Assert.AreEqual(FateOutcome.InPlace, FateRollRules.Evaluate(3, 6, 6, 0), "6+6 — всегда на месте");
        }

        [Test]
        public void NoTotem_ChangesSarcophagusOdds_ByConstruction()
        {
            // Тотемы не имеют доступа к шансам Саркофагов/частей/Реликвий: эти правила не в IRunEffects (§19).
            foreach (var member in typeof(IRunEffects).GetProperties())
                StringAssert.DoesNotContain("Sarcophag", member.Name);
        }
    }
}
