using System.Collections.Generic;
using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Generation.Tests
{
    /// <summary>
    /// Разграничение по рядам между <see cref="Core.TunnelObstacleGenerator"/>
    /// и <see cref="SegmentRowProvider"/> (переходное состояние
    /// сосуществования двух генераторов на одной сетке, docs/wiki/roadmap.md)
    /// — в отличие от изолированных тестов в
    /// <c>Core.Tests.TunnelObstacleGeneratorTests</c> и
    /// <c>Core.Tests.TunnelGridTests</c> (которые проверяют механизм claim по
    /// отдельности), здесь оба генератора реально подписаны на ОДНУ и ту же
    /// <see cref="TunnelGrid"/> одновременно, как это будет на сцене после
    /// подключения <see cref="SegmentGenerationController"/> в RunBootstrap.
    /// Обстакловый генератор намеренно настроен на бросок, который ВСЕГДА
    /// требует "заблокировано" — если бы разграничение было соглашением, а не
    /// конструкцией, это проявилось бы как испорченные плиты сегмента.
    /// </summary>
    public class SegmentGenerationCoexistenceTests
    {
        private const int Width = 5;

        private static SegmentTileType[,] OpenRowsWithOneBlock(int rowCount, int blockedRow, int blockedColumn)
        {
            var tiles = new SegmentTileType[rowCount, Width];
            for (var r = 0; r < rowCount; r++)
                for (var c = 0; c < Width; c++)
                    tiles[r, c] = SegmentTileType.Open;
            tiles[blockedRow, blockedColumn] = SegmentTileType.Blocked;
            return tiles;
        }

        [Test]
        public void CoexistingGenerators_SegmentCoveredRows_NeverConsumeObstacleGeneratorRoll()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));

            // Ровно одно значение в очереди: если бы обстакловый генератор
            // хоть раз откликнулся на заявленный ряд, второй Dequeue() на
            // пустой очереди упал бы с исключением ещё до того, как мы
            // дойдём до незаявленного ряда ниже.
            using var obstacles = new TunnelObstacleGenerator(grid, AlwaysBlock());

            // Шаблон на 5 рядов, но покрытие в конструкторе SegmentRowProvider
            // идёт до row 0+RowsAheadOfPlayer(8) включительно — 5-рядный
            // шаблон не дотягивает за один проход, поэтому применяется дважды
            // подряд (baseRow=1..5, затем baseRow=6..10): заявлены ряды 1..10,
            // первый по-настоящему незаявленный ряд — 11.
            var template = new SegmentTemplate("with-block", 1, SegmentRewardTag.Coins,
                OpenRowsWithOneBlock(5, blockedRow: 2, blockedColumn: 1));
            var selector = new SegmentSelector(new List<SegmentTemplate> { template }, new RunSeed(1));
            using var segments = new SegmentRowProvider(grid, trail, selector, _ => 5);

            // Незаявленный ряд (за пределами обоих применений шаблона) —
            // единственный, кто имеет право потратить бросок из очереди.
            var unclaimedTile = grid.GetOrCreateTile(new GridCoordinate(11, 2));

            Assert.IsTrue(unclaimedTile.IsBlocked, "Незаявленный ряд по-прежнему засеивается обстакловым генератором");
        }

        [Test]
        public void CoexistingGenerators_SegmentTemplateContent_SurvivesAdversarialObstacleRolls()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));

            // Каждый бросок гарантированно ниже любого порога — если бы
            // разграничение было по договорённости, а не по конструкции,
            // ВСЕ плиты шаблона стали бы заблокированными.
            using var obstacles = new TunnelObstacleGenerator(grid, AlwaysBlock());

            var template = new SegmentTemplate("with-block", 1, SegmentRewardTag.Coins,
                OpenRowsWithOneBlock(5, blockedRow: 2, blockedColumn: 1)); // -> абсолютный row 3, column 1
            var selector = new SegmentSelector(new List<SegmentTemplate> { template }, new RunSeed(1));
            using var segments = new SegmentRowProvider(grid, trail, selector, _ => 5);

            // Ровно те тайлы, которые шаблон сам пометил заблокированными —
            // 5-рядный шаблон применяется дважды подряд (см. предыдущий
            // тест), локальный (2,1) даёт абсолютные (3,1) и (8,1).
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(3, 1)).IsBlocked);
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(8, 1)).IsBlocked);

            // Все остальные тайлы обоих применений шаблона (baseRow=1..10,
            // все колонки, кроме двух намеренно заблокированных выше) должны
            // остаться Open — обстакловый генератор не должен был тронуть ни
            // одну из них, несмотря на гарантированно "блокирующий" бросок.
            for (var row = 1; row <= 10; row++)
            for (var column = 0; column < Width; column++)
            {
                if ((row == 3 || row == 8) && column == 1) continue; // намеренно заблокированные тайлы шаблона
                var tile = grid.GetOrCreateTile(new GridCoordinate(row, column));
                Assert.IsFalse(tile.IsBlocked, $"({row},{column}) должен остаться Open по шаблону — не тронут обстакловым генератором");
            }
        }

        // Гарантированно ниже любого порога TunnelObstacleGenerator при
        // каждом вызове — не Queue с ограниченной длиной, потому что этот
        // тест намеренно не знает заранее, сколько тайлов покроет
        // SegmentRowProvider (зависит от RowsAheadOfPlayer + размера
        // шаблона); первый тест выше отдельно проверяет, что для заявленных
        // рядов бросок вообще не берётся, через Queue на ровно одно значение.
        private static System.Func<float> AlwaysBlock() => () => 0f;
    }
}
