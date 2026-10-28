using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Sanctuary.Tests
{
    public class SanctuaryRunSystemTests
    {
        private const int Width = 5;
        
        private sealed class Fixture
        {
            public TunnelGrid Grid;
            public GridTraceTrail Trail;
            public RunCurrencyAccumulator Mana;
            public SanctuaryRunSystem System;
            public int Hazards;
        }

        // Ряд entryRow — вход (все пять плит); выход — по длине шаблона Святилища Лешего из каталога.
        private static Fixture CreateFixture(int entryRow = 1)
        {
            var grid = new TunnelGrid(Width);
            for (var c = 0; c < Width; c++) grid.GetOrCreateTile(new GridCoordinate(entryRow, c)).MarkSanctuaryEntry();
            var trail = new GridTraceTrail(grid, new GridCoordinate(entryRow - 1, 2)); // стартуем перед рядом входа
            var mana = new RunCurrencyAccumulator();
            var f = new Fixture { Grid = grid, Trail = trail, Mana = mana };
            f.System = new SanctuaryRunSystem(grid, trail, mana, () => f.Hazards++);
            return f;
        }

        private static void Step(Fixture f, int row, int column) =>
            Assert.IsTrue(f.Trail.TryAdvanceTo(new GridCoordinate(row, column)), $"шаг на ({row},{column})");

        [Test]
        public void EnteringEntryRow_ActivatesSanctuary()
        {
            var f = CreateFixture(entryRow: 1);

            Step(f, 1, 2);

            Assert.IsNotNull(f.System.ActiveSanctuary);
            Assert.AreEqual(1, f.System.ActiveSanctuary.EntryRow);
        }

        [Test]
        public void Tick_CollapsesRowsIntoLava()
        {
            var f = CreateFixture(entryRow: 1);
            Step(f, 1, 2);
            var rowsPerSecond = Sanctuary.DefaultCollapseRowsPerSecond;

            f.System.Tick(2.2f / rowsPerSecond); // фронт на ≈ ряду 1+2

            Assert.AreEqual(LethalTrapType.Lava, f.Grid.GetOrCreateTile(new GridCoordinate(2, 0)).LethalTrap);
            Assert.AreEqual(LethalTrapType.Lava, f.Grid.GetOrCreateTile(new GridCoordinate(3, 4)).LethalTrap);
            Assert.IsNull(f.Grid.GetOrCreateTile(new GridCoordinate(1, 2)).LethalTrap, "ряд входа не обрушается");
        }

        [Test]
        public void Collapse_OverPlayerRow_ReportsHazard()
        {
            var f = CreateFixture(entryRow: 1);
            Step(f, 1, 2);
            Step(f, 2, 2);

            f.System.Tick(5f / Sanctuary.DefaultCollapseRowsPerSecond);

            Assert.AreEqual(1, f.Hazards, "обрушился ряд под игроком — одна опасность");
        }

        [Test]
        public void Collapse_BehindPlayer_DoesNotReportHazard()
        {
            var f = CreateFixture(entryRow: 1);
            Step(f, 1, 2);
            Step(f, 2, 2);
            Step(f, 3, 2);
            Step(f, 4, 2);

            f.System.Tick(1.1f / Sanctuary.DefaultCollapseRowsPerSecond); // обрушен только ряд 2

            Assert.AreEqual(0, f.Hazards);
        }

        [Test]
        public void SteppingOnCollapsedRow_IsStillAStep_ButLethal()
        {
            var f = CreateFixture(entryRow: 1);
            Step(f, 1, 2);
            f.System.Tick(1.1f / Sanctuary.DefaultCollapseRowsPerSecond);
            LethalTrapType? raised = null;
            f.Trail.LethalTrapTriggered += (_, t) => raised = t;

            Assert.IsTrue(f.Trail.TryAdvanceTo(new GridCoordinate(2, 2)));
            Assert.AreEqual(LethalTrapType.Lava, raised);
        }

        [Test]
        public void SteppingOnSarcophagus_TakesItAndSpeedsUpCollapse()
        {
            var f = CreateFixture(entryRow: 1);
            f.Grid.GetOrCreateTile(new GridCoordinate(2, 2)).MarkSarcophagus();
            Step(f, 1, 2);
            var taken = false;
            f.System.SarcophagusTaken += () => taken = true;

            Step(f, 2, 2);

            Assert.IsTrue(taken);
            Assert.IsTrue(f.System.ActiveSanctuary.IsSarcophagusTaken);
            Assert.IsFalse(f.Grid.GetOrCreateTile(new GridCoordinate(2, 2)).HasSarcophagus);
        }

        [Test]
        public void Collecting_VeinResonanceEcho_UpdatesMultiplierAndIncome()
        {
            var f = CreateFixture(entryRow: 1);
            f.Grid.GetOrCreateTile(new GridCoordinate(2, 2)).MarkSanctuaryTile(SanctuaryTileKind.Resonance);
            f.Grid.GetOrCreateTile(new GridCoordinate(3, 2)).MarkSanctuaryTile(SanctuaryTileKind.Echo);
            f.Grid.GetOrCreateTile(new GridCoordinate(4, 2)).MarkSanctuaryTile(SanctuaryTileKind.Vein);
            Step(f, 1, 2);

            Step(f, 2, 2);
            Step(f, 3, 2);
            Step(f, 4, 2);

            Assert.AreEqual(3f, f.System.ActiveSanctuary.Multiplier.CurrentMultiplier, 1e-5f); // (1+0.5)*2
            Assert.AreEqual((int)(SanctuaryRunSystem.VeinBaseAmount * 3f), f.System.ActiveSanctuary.AccumulatedIncome);
        }

        [Test]
        public void ReachingExitRow_CreditsManaOnceAndRaisesExited()
        {
            var f = CreateFixture(entryRow: 1);
            f.Grid.GetOrCreateTile(new GridCoordinate(2, 2)).MarkSanctuaryTile(SanctuaryTileKind.Vein);
            Step(f, 1, 2);
            Step(f, 2, 2);
            Sanctuary exited = null;
            var count = 0;
            f.System.Exited += s => { exited = s; count++; };
            var exitRow = f.System.ActiveSanctuary.ExitRow;

            for (var row = 3; row <= exitRow; row++) Step(f, row, 2);

            Assert.AreEqual(1, count);
            Assert.AreSame(f.System.ActiveSanctuary, exited);
            Assert.AreEqual(SanctuaryRunSystem.VeinBaseAmount, f.Mana.Total);
        }

        [Test]
        public void StandingOnRiftVein_AccumulatesIncomeOverTime()
        {
            var f = CreateFixture(entryRow: 1);
            f.Grid.GetOrCreateTile(new GridCoordinate(2, 2)).MarkSanctuaryRift(SanctuaryRiftSubtype.Vein);
            Step(f, 1, 2);
            Step(f, 2, 2);

            f.System.Tick(0.1f);

            Assert.Greater(f.System.ActiveSanctuary.AccumulatedIncome, 0);
        }

        [Test]
        public void Dispose_StopsReacting()
        {
            var f = CreateFixture(entryRow: 1);
            f.System.Dispose();

            Step(f, 1, 2);

            Assert.IsNull(f.System.ActiveSanctuary);
        }
    }
}
