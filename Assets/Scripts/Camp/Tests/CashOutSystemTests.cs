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
        public void Advanced_ReachesAltar_ChecksPointsBothAccumulators()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkAltar();
            var mana = new RunCurrencyAccumulator();
            var keys = new RunCurrencyAccumulator();
            mana.Add(200);
            keys.Add(5);
            using var system = new CashOutSystem(grid, trail, mana, keys);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            mana.Add(50); // после чекпоинта
            mana.RevertToCheckpoint();

            Assert.AreEqual(200, mana.Total, "чекпоинт должен был зафиксироваться на 200 в момент достижения Алтаря");
        }

        [Test]
        public void Advanced_ReachesAltarWithoutKeys_StillChecksPoint()
        {
            // В отличие от AltarTriggerSystem (открытие Ритуала), кэш-аут не
            // зависит от наличия Ключей — фиксация происходит всегда.
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkAltar();
            var mana = new RunCurrencyAccumulator();
            var keys = new RunCurrencyAccumulator(); // 0 Ключей
            mana.Add(100);
            using var system = new CashOutSystem(grid, trail, mana, keys);

            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            mana.Add(50);
            mana.RevertToCheckpoint();

            Assert.AreEqual(100, mana.Total);
        }

        [Test]
        public void Advanced_NonAltarTile_DoesNotCheckpoint()
        {
            var (grid, trail) = CreateTrail();
            var mana = new RunCurrencyAccumulator();
            var keys = new RunCurrencyAccumulator();
            mana.Add(100);
            using var system = new CashOutSystem(grid, trail, mana, keys);

            trail.TryAdvanceTo(new GridCoordinate(1, 2)); // обычная плита
            mana.Add(50);
            mana.RevertToCheckpoint();

            Assert.AreEqual(0, mana.Total, "чекпоинт не был выставлен явно — откат к 0 (дефолт)");
        }

        [Test]
        public void Dispose_StopsReactingToFurtherAdvances()
        {
            var (grid, trail) = CreateTrail();
            grid.GetOrCreateTile(new GridCoordinate(1, 2)).MarkAltar();
            var mana = new RunCurrencyAccumulator();
            var keys = new RunCurrencyAccumulator();
            mana.Add(100);
            var system = new CashOutSystem(grid, trail, mana, keys);
            system.Dispose();

            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            mana.Add(50);
            mana.RevertToCheckpoint();

            Assert.AreEqual(0, mana.Total, "после Dispose чекпоинт на Алтаре не должен выставляться");
        }
    }
}
