using System;
using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Sanctuary.Tests
{
    public class SanctuaryTests
    {
        private static Sanctuary Create(float rate = 1f) => new Sanctuary(entryRow: 0, exitRow: 10, collapseRowsPerSecond: rate);

        [Test]
        public void Constructor_ExitNotBeyondEntry_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Sanctuary(5, 5, 1f));
        }

        [Test]
        public void New_IsActiveAndMultiplierIsOne()
        {
            var s = Create();

            Assert.IsTrue(s.IsActive);
            Assert.AreEqual(1f, s.Multiplier.CurrentMultiplier);
            Assert.IsFalse(s.IsSarcophagusTaken);
        }

        [Test]
        public void CollectVein_UsesCurrentMultiplier()
        {
            var s = Create();
            s.CollectResonance(); // ×1.5

            s.CollectVein(100);

            Assert.AreEqual(150, s.AccumulatedIncome);
        }

        [Test]
        public void Multiplier_ResonanceResonanceEcho_IsFour()
        {
            var s = Create();

            s.CollectResonance();
            s.CollectResonance();
            s.CollectEcho();

            Assert.AreEqual(4f, s.Multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void Multiplier_EchoResonanceResonance_IsThree()
        {
            var s = Create();

            s.CollectEcho();
            s.CollectResonance();
            s.CollectResonance();

            Assert.AreEqual(3f, s.Multiplier.CurrentMultiplier, 1e-5f);
        }

        [Test]
        public void Echo_RaisesEchoCollected()
        {
            var s = Create();
            var raised = false;
            s.EchoCollected += () => raised = true;

            s.CollectEcho();

            Assert.IsTrue(raised);
        }

        [Test]
        public void TickRift_Vein_AccumulatesIncome_Resonance_RaisesMultiplier()
        {
            var vein = Create();
            vein.TickRift(SanctuaryRiftSubtype.Vein, 2f);
            Assert.AreEqual(Sanctuary.RiftVeinIncomePerSecond * 2, vein.AccumulatedIncome);

            var resonance = Create();
            resonance.TickRift(SanctuaryRiftSubtype.Resonance, 10f);
            Assert.Greater(resonance.Multiplier.CurrentMultiplier, 1f);
        }

        [Test]
        public void Tick_AdvancesCollapseFront()
        {
            var s = Create(rate: 2f);

            s.Tick(1f);

            Assert.AreEqual(2f, s.Collapse.RowPosition, 1e-5f);
        }

        [Test]
        public void TakeSarcophagus_SpeedsUpCollapse()
        {
            var normal = Create();
            normal.Tick(1f);
            var fast = Create();
            fast.TakeSarcophagus();
            fast.Tick(1f);

            Assert.IsTrue(fast.IsSarcophagusTaken);
            Assert.Greater(fast.Collapse.RowPosition, normal.Collapse.RowPosition);
            Assert.AreEqual(Sanctuary.SarcophagusCollapseMultiplier, fast.Collapse.RowPosition / normal.Collapse.RowPosition, 1e-4f);
        }

        [Test]
        public void TakeSarcophagus_Twice_RaisesEventOnce()
        {
            var s = Create();
            var count = 0;
            s.SarcophagusTaken += () => count++;

            s.TakeSarcophagus();
            s.TakeSarcophagus();

            Assert.AreEqual(1, count);
        }

        [Test]
        public void LastCollapsedRow_NeverReachesExitRow()
        {
            var s = Create(rate: 100f);

            s.Tick(10f);

            Assert.AreEqual(9, s.LastCollapsedRow);
        }

        [Test]
        public void ReportPlayerRow_AtExit_MarksExitedAndDeactivates()
        {
            var s = Create();

            s.ReportPlayerRow(10);

            Assert.IsTrue(s.HasExited);
            Assert.IsFalse(s.IsActive);
        }

        [Test]
        public void AfterExit_CollectionsAndTicksAreIgnored()
        {
            var s = Create();
            s.ReportPlayerRow(10);

            s.CollectVein(100);
            s.CollectResonance();
            s.Tick(5f);

            Assert.AreEqual(0, s.AccumulatedIncome);
            Assert.AreEqual(1f, s.Multiplier.CurrentMultiplier);
            Assert.AreEqual(0f, s.Collapse.RowPosition);
        }
    }
}
