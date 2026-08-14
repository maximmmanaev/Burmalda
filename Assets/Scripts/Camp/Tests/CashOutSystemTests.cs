using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Camp.Tests
{
    public class CashOutSystemTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail) CreateTrail()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            return (grid, trail);
        }

        [Test]
        public void Advanced_ReachesAltar_ChecksPointsAllThreeAccumulators()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkAltar();
            var coins = new RunCurrencyAccumulator();
            var mana = new RunCurrencyAccumulator();
            var keys = new RunCurrencyAccumulator();
            coins.Add(100);
            mana.Add(200);
            keys.Add(5);
            using var system = new CashOutSystem(grid, trail, coins, mana, keys);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            coins.Add(50); // после чекпоинта
            coins.RevertToCheckpoint();

            Assert.AreEqual(100, coins.Total, "чекпоинт должен был зафиксироваться на 100 в момент достижения Алтаря");
        }

        [Test]
        public void Advanced_ReachesAltarWithoutKeys_StillChecksPoint()
        {
            // В отличие от AltarTriggerSystem (открытие Ритуала), кэш-аут не
            // зависит от наличия Ключей — фиксация происходит всегда.
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkAltar();
            var coins = new RunCurrencyAccumulator();
            var mana = new RunCurrencyAccumulator();
            var keys = new RunCurrencyAccumulator(); // 0 Ключей
            coins.Add(100);
            using var system = new CashOutSystem(grid, trail, coins, mana, keys);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            coins.Add(50);
            coins.RevertToCheckpoint();

            Assert.AreEqual(100, coins.Total);
        }

        [Test]
        public void Advanced_NonAltarTile_DoesNotCheckpoint()
        {
            var (grid, trail) = CreateTrail();
            var coins = new RunCurrencyAccumulator();
            var mana = new RunCurrencyAccumulator();
            var keys = new RunCurrencyAccumulator();
            coins.Add(100);
            using var system = new CashOutSystem(grid, trail, coins, mana, keys);

            trail.TryAdvanceTo(new GridCoordinate(1, 2)); // обычная плита
            coins.Add(50);
            coins.RevertToCheckpoint();

            Assert.AreEqual(0, coins.Total, "чекпоинт не был выставлен явно — откат к 0 (дефолт)");
        }

        [Test]
        public void Dispose_StopsReactingToFurtherAdvances()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkAltar();
            var coins = new RunCurrencyAccumulator();
            var mana = new RunCurrencyAccumulator();
            var keys = new RunCurrencyAccumulator();
            coins.Add(100);
            var system = new CashOutSystem(grid, trail, coins, mana, keys);
            system.Dispose();

            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            coins.Add(50);
            coins.RevertToCheckpoint();

            Assert.AreEqual(0, coins.Total, "после Dispose чекпоинт на Алтаре не должен выставляться");
        }
    }
}
