using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Currencies.Tests
{
    /// <summary>Серия чистого прохода (PRD v10 §12.2) и Мана-эффекты Реликвий: Лешачья тропа, Веретено, Чёрное золото.</summary>
    public class RunEffectsCurrencyTests
    {
        private int _oldThreshold, _oldBase, _oldGrowth, _oldMax;

        [SetUp]
        public void Setup()
        {
            _oldThreshold = CleanPassStreakSystem.GlowThreshold;
            _oldBase = CleanPassStreakSystem.BaseBonusMana;
            _oldGrowth = CleanPassStreakSystem.BonusGrowthPerTile;
            _oldMax = CleanPassStreakSystem.MaxBonusMana;
            CleanPassStreakSystem.GlowThreshold = 3;
            CleanPassStreakSystem.BaseBonusMana = 10;
            CleanPassStreakSystem.BonusGrowthPerTile = 2;
            CleanPassStreakSystem.MaxBonusMana = 100;
        }

        [TearDown]
        public void Teardown()
        {
            RunEffects.Current = RunEffects.Neutral;
            CleanPassStreakSystem.GlowThreshold = _oldThreshold;
            CleanPassStreakSystem.BaseBonusMana = _oldBase;
            CleanPassStreakSystem.BonusGrowthPerTile = _oldGrowth;
            CleanPassStreakSystem.MaxBonusMana = _oldMax;
        }

        private static (TunnelGrid grid, GridTraceTrail trail, RunCurrencyAccumulator mana) Make()
        {
            var grid = new TunnelGrid(5);
            return (grid, new GridTraceTrail(grid, new GridCoordinate(0, 2)), new RunCurrencyAccumulator());
        }

        private static void Walk(GridTraceTrail trail, int steps)
        {
            for (var i = 1; i <= steps; i++) Assert.IsTrue(trail.TryAdvanceTo(new GridCoordinate(trail.CurrentPosition.Row + 1, 2)));
        }

        [Test]
        public void Streak_CountsCleanTiles_BonusFromThreshold_GlowGrows()
        {
            var (grid, trail, mana) = Make();
            using var streak = new CleanPassStreakSystem(grid, trail, mana);

            Walk(trail, 2);
            Assert.AreEqual(2, streak.Streak);
            Assert.AreEqual(0, mana.Total, "до порога бонуса нет");
            Assert.AreEqual(0f, streak.Glow);

            Walk(trail, 1); // 3-я плита серии: бонус 10
            Assert.AreEqual(10, mana.Total);
            Walk(trail, 1); // 4-я: 12
            Assert.AreEqual(22, mana.Total);
            Assert.Greater(streak.Glow, 0f);
        }

        [Test]
        public void Streak_SteppingOnTrigger_BreaksIt()
        {
            var (grid, trail, mana) = Make();
            grid.GetOrCreateTile(new GridCoordinate(3, 2)).MarkBombTrigger();
            using var streak = new CleanPassStreakSystem(grid, trail, mana);

            Walk(trail, 2);
            Assert.AreEqual(2, streak.Streak);
            Walk(trail, 1); // плита (3,2) — триггер

            Assert.AreEqual(0, streak.Streak);
            Assert.AreEqual(0f, streak.Glow);
        }

        [Test]
        public void Streak_Glow_IsCappedAtOne()
        {
            var (grid, trail, mana) = Make();
            using var streak = new CleanPassStreakSystem(grid, trail, mana);

            Walk(trail, CleanPassStreakSystem.FullGlowTiles + 3);

            Assert.AreEqual(1f, streak.Glow);
        }

        [Test]
        public void LeshyTalisman_DoublesStreakBonus()
        {
            RunEffects.Current = new ConfigurableRunEffects { StreakBonusMultiplier = 2f };
            var (grid, trail, mana) = Make();
            using var streak = new CleanPassStreakSystem(grid, trail, mana);

            Walk(trail, 3);

            Assert.AreEqual(20, mana.Total);
        }

        [Test]
        public void Spindle_BonusManaOnEveryFifthTile()
        {
            RunEffects.Current = new ConfigurableRunEffects { SpindleEvery = 5, SpindleBonus = 50 };
            var (grid, trail, mana) = Make();
            var multiplier = new TrailMultiplierSystem(trail);
            using var income = new TrailManaIncomeSystem(trail, multiplier, mana);

            Walk(trail, 4);
            var before = mana.Total;
            Walk(trail, 1); // 5-я плита

            Assert.GreaterOrEqual(mana.Total - before, 50 + 1);
            var perTile = before / 4;
            Assert.AreEqual(perTile + 50, mana.Total - before, "обычная плита + бонус Веретена");
        }

        [Test]
        public void BlackGold_ManaMultiplier_AppliesToIncomeAndSources()
        {
            var (grid, trail, mana) = Make();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkManaSource();
            using var income = new TrailManaIncomeSystem(trail, new TrailMultiplierSystem(trail), mana);
            using var sources = new TrailTileCurrencySystem(grid, trail, t => t.IsManaSource, 100, mana);
            RunEffects.Current = new ConfigurableRunEffects { ManaMultiplier = 1.5f };

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            // плита-источник 100 × 1.5 = 150 (+ доход плиты ×1.5)
            Assert.GreaterOrEqual(mana.Total, 150);
            var expectedIncome = (int)System.Math.Round(TrailManaIncomeSystem.BaseManaPerTile * 1f * 1.5f);
            Assert.AreEqual(150 + expectedIncome, mana.Total);
        }
    }
}
