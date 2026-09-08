using System.Collections.Generic;
using System.Linq;
using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.BossRoom.Tests
{
    public class BossRoomGeneratorTests
    {
        // Задача «Комната Босса» — те же изменяемые static-поля, что и у
        // Core.TunnelObstacleGenerator: снимок/восстановление вокруг
        // КАЖДОГО теста (порядок выполнения NUnit не гарантирован).
        private int _savedResonanceMin, _savedResonanceMax, _savedEchoMin, _savedEchoMax, _savedRoomLength, _savedVeinAmount;
        private float _savedVeinShare;

        [SetUp]
        public void SaveDefaults()
        {
            _savedResonanceMin = BossRoomGenerator.ResonanceCountMin;
            _savedResonanceMax = BossRoomGenerator.ResonanceCountMax;
            _savedEchoMin = BossRoomGenerator.EchoCountMin;
            _savedEchoMax = BossRoomGenerator.EchoCountMax;
            _savedRoomLength = BossRoomGenerator.DefaultRoomLengthRows;
            _savedVeinAmount = BossRoomGenerator.VeinBaseAmount;
            _savedVeinShare = BossRoomGenerator.VeinShareOfRemaining;
        }

        [TearDown]
        public void RestoreDefaults()
        {
            BossRoomGenerator.ResonanceCountMin = _savedResonanceMin;
            BossRoomGenerator.ResonanceCountMax = _savedResonanceMax;
            BossRoomGenerator.EchoCountMin = _savedEchoMin;
            BossRoomGenerator.EchoCountMax = _savedEchoMax;
            BossRoomGenerator.DefaultRoomLengthRows = _savedRoomLength;
            BossRoomGenerator.VeinBaseAmount = _savedVeinAmount;
            BossRoomGenerator.VeinShareOfRemaining = _savedVeinShare;
        }

        private static System.Func<float> Constant(float value) => () => value;

        [Test]
        public void Generate_ClaimsAllRoomRows()
        {
            var grid = new TunnelGrid(5);
            var generator = new BossRoomGenerator(grid, Constant(0f));

            generator.Generate(entryRow: 10, roomLengthRows: 6);

            for (var row = 11; row <= 16; row++)
                Assert.IsTrue(grid.IsRowClaimed(row), $"Ряд {row} должен быть заявлен.");
            Assert.IsFalse(grid.IsRowClaimed(10), "Сама плита-вход не входит в заявленные ряды Комнаты.");
            Assert.IsFalse(grid.IsRowClaimed(17), "Ряд за пределами Комнаты не должен быть заявлен.");
        }

        [Test]
        public void Generate_PlacesExactlyConfiguredResonanceAndEchoCounts()
        {
            BossRoomGenerator.ResonanceCountMin = 4;
            BossRoomGenerator.ResonanceCountMax = 4;
            BossRoomGenerator.EchoCountMin = 2;
            BossRoomGenerator.EchoCountMax = 2;

            var grid = new TunnelGrid(5);
            var generator = new BossRoomGenerator(grid, Constant(0f)); // 0 роллит минимум количества и всегда даёт Жилу на остальных клетках

            generator.Generate(entryRow: 0, roomLengthRows: 6);

            var tiles = CollectRoomTiles(grid, 1, 6);
            Assert.AreEqual(4, tiles.Count(t => t.BossRoomTile == BossRoomTileKind.Resonance));
            Assert.AreEqual(2, tiles.Count(t => t.BossRoomTile == BossRoomTileKind.Echo));
        }

        [Test]
        public void Generate_VeinShareZero_LeavesRemainingCellsWithoutContent()
        {
            BossRoomGenerator.ResonanceCountMin = 0;
            BossRoomGenerator.ResonanceCountMax = 0;
            BossRoomGenerator.EchoCountMin = 0;
            BossRoomGenerator.EchoCountMax = 0;
            BossRoomGenerator.VeinShareOfRemaining = 0f;

            var grid = new TunnelGrid(5);
            var generator = new BossRoomGenerator(grid, Constant(0.99f));

            generator.Generate(entryRow: 0, roomLengthRows: 3);

            var tiles = CollectRoomTiles(grid, 1, 3);
            Assert.IsTrue(tiles.All(t => !t.BossRoomTile.HasValue));
        }

        [Test]
        public void Generate_VeinShareOne_FillsAllRemainingCellsWithVein()
        {
            BossRoomGenerator.ResonanceCountMin = 0;
            BossRoomGenerator.ResonanceCountMax = 0;
            BossRoomGenerator.EchoCountMin = 0;
            BossRoomGenerator.EchoCountMax = 0;
            BossRoomGenerator.VeinShareOfRemaining = 1f;

            var grid = new TunnelGrid(5);
            var generator = new BossRoomGenerator(grid, Constant(0f));

            generator.Generate(entryRow: 0, roomLengthRows: 3);

            var tiles = CollectRoomTiles(grid, 1, 3);
            Assert.IsTrue(tiles.All(t => t.BossRoomTile == BossRoomTileKind.Vein));
        }

        [Test]
        public void Generate_SkipsCellsAlreadyMarkedByAnotherGenerator()
        {
            BossRoomGenerator.ResonanceCountMin = 0;
            BossRoomGenerator.ResonanceCountMax = 0;
            BossRoomGenerator.EchoCountMin = 0;
            BossRoomGenerator.EchoCountMax = 0;
            BossRoomGenerator.VeinShareOfRemaining = 1f;

            var grid = new TunnelGrid(5);
            // Симулирует окно материализации reveal-ahead: клетка уже
            // получила чужую роль ДО того, как генератор Комнаты до неё дошёл.
            var contaminated = new GridCoordinate(1, 0);
            grid.GetOrCreateTile(contaminated).MarkBlocked();

            var generator = new BossRoomGenerator(grid, Constant(0f));
            generator.Generate(entryRow: 0, roomLengthRows: 1);

            var tile = grid.GetOrCreateTile(contaminated);
            Assert.IsTrue(tile.IsBlocked);
            Assert.IsFalse(tile.BossRoomTile.HasValue);
        }

        // Владелец, 2026-09-05 («закрыть вертикальный срез — плита Эхо»):
        // «Эхо должно лежать так, чтобы дойти до него можно было только
        // после Резонансов» — прогоняем много разных seed'ов, чтобы поймать
        // регрессию, которая проявляется не при каждом случайном порядке.
        [Test]
        public void Generate_EchoTiles_AreAlwaysStrictlyDeeperThanEveryResonanceTile()
        {
            BossRoomGenerator.ResonanceCountMin = 6;
            BossRoomGenerator.ResonanceCountMax = 10;
            BossRoomGenerator.EchoCountMin = 1;
            BossRoomGenerator.EchoCountMax = 3;
            BossRoomGenerator.VeinShareOfRemaining = 0.6f;

            for (var seed = 0; seed < 30; seed++)
            {
                var grid = new TunnelGrid(5);
                var generator = new BossRoomGenerator(grid, PseudoRandom(seed));

                generator.Generate(entryRow: 0, roomLengthRows: BossRoomGenerator.DefaultRoomLengthRows);

                var tiles = CollectRoomTiles(grid, 1, BossRoomGenerator.DefaultRoomLengthRows);
                var resonanceRows = tiles.Where(t => t.BossRoomTile == BossRoomTileKind.Resonance).Select(t => t.Coordinate.Row).ToList();
                var echoRows = tiles.Where(t => t.BossRoomTile == BossRoomTileKind.Echo).Select(t => t.Coordinate.Row).ToList();

                Assert.Greater(resonanceRows.Count, 0, $"seed={seed}: должен быть хотя бы один Резонанс.");
                Assert.Greater(echoRows.Count, 0, $"seed={seed}: должно быть хотя бы одно Эхо.");
                Assert.Less(resonanceRows.Max(), echoRows.Min(), $"seed={seed}: самый глубокий Резонанс должен быть мельче самого мелкого Эха.");
            }
        }

        [Test]
        public void Generate_EchoCount_StaysWithinConfiguredRange()
        {
            BossRoomGenerator.ResonanceCountMin = 6;
            BossRoomGenerator.ResonanceCountMax = 10;
            BossRoomGenerator.EchoCountMin = 1;
            BossRoomGenerator.EchoCountMax = 3;
            BossRoomGenerator.VeinShareOfRemaining = 0.6f;

            for (var seed = 0; seed < 30; seed++)
            {
                var grid = new TunnelGrid(5);
                var generator = new BossRoomGenerator(grid, PseudoRandom(seed));

                generator.Generate(entryRow: 0, roomLengthRows: BossRoomGenerator.DefaultRoomLengthRows);

                var echoCount = CollectRoomTiles(grid, 1, BossRoomGenerator.DefaultRoomLengthRows).Count(t => t.BossRoomTile == BossRoomTileKind.Echo);
                Assert.GreaterOrEqual(echoCount, 1, $"seed={seed}");
                Assert.LessOrEqual(echoCount, 3, $"seed={seed}");
            }
        }

        private static System.Func<float> PseudoRandom(int seed)
        {
            var random = new System.Random(seed);
            return () => (float)random.NextDouble();
        }

        [Test]
        public void Generate_ZeroRoomLength_Throws()
        {
            var grid = new TunnelGrid(5);
            var generator = new BossRoomGenerator(grid, Constant(0f));

            Assert.Throws<System.ArgumentOutOfRangeException>(() => generator.Generate(entryRow: 0, roomLengthRows: 0));
        }

        private static List<Tile> CollectRoomTiles(TunnelGrid grid, int fromRow, int toRow)
        {
            var result = new List<Tile>();
            for (var row = fromRow; row <= toRow; row++)
                for (var column = 0; column < grid.Width; column++)
                    result.Add(grid.GetOrCreateTile(new GridCoordinate(row, column)));
            return result;
        }
    }
}
