using System;
using NUnit.Framework;

namespace Burmalda.BossRoom.Tests
{
    public class BossWaveTests
    {
        [Test]
        public void Constructor_ZeroOrNegativeSpeed_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossWave(startingRow: 0, rowsPerSecond: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossWave(startingRow: 0, rowsPerSecond: -1f));
        }

        [Test]
        public void NewBossWave_RowPositionEqualsStartingRow()
        {
            var wave = new BossWave(startingRow: 5, rowsPerSecond: 1f);

            Assert.AreEqual(5f, wave.RowPosition);
        }

        [Test]
        public void Tick_AdvancesRowPositionBySpeedTimesDelta()
        {
            var wave = new BossWave(startingRow: 0, rowsPerSecond: 2f);

            wave.Tick(1.5f);

            Assert.AreEqual(3f, wave.RowPosition, 1e-5f);
        }

        [Test]
        public void Tick_MultipleCalls_Accumulates()
        {
            var wave = new BossWave(startingRow: 0, rowsPerSecond: 1f);

            wave.Tick(0.5f);
            wave.Tick(0.5f);

            Assert.AreEqual(1f, wave.RowPosition, 1e-5f);
        }

        [Test]
        public void Tick_ZeroOrNegativeDelta_IsNoOp()
        {
            var wave = new BossWave(startingRow: 0, rowsPerSecond: 1f);

            wave.Tick(0f);
            wave.Tick(-1f);

            Assert.AreEqual(0f, wave.RowPosition);
        }

        [Test]
        public void HasCaught_RowPositionBelowPlayerRow_ReturnsFalse()
        {
            var wave = new BossWave(startingRow: 0, rowsPerSecond: 1f);

            Assert.IsFalse(wave.HasCaught(playerRow: 5));
        }

        [Test]
        public void HasCaught_RowPositionEqualsPlayerRow_ReturnsTrue()
        {
            var wave = new BossWave(startingRow: 5, rowsPerSecond: 1f);

            Assert.IsTrue(wave.HasCaught(playerRow: 5));
        }

        [Test]
        public void HasCaught_RowPositionPastPlayerRow_ReturnsTrue()
        {
            var wave = new BossWave(startingRow: 0, rowsPerSecond: 10f);
            wave.Tick(1f); // row 10

            Assert.IsTrue(wave.HasCaught(playerRow: 5));
        }
    }
}
