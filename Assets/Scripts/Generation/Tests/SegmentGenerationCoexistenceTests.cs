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
            using var segments = new SegmentRowProvider(grid, trail, selector, _ => 1);

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
            using var segments = new SegmentRowProvider(grid, trail, selector, _ => 1);

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

        /// <summary>
        /// Задача «партии 1 и 2 + правила отбора»: два теста выше доказывают,
        /// что разграничение по <c>ClaimRow</c> корректно, КОГДА единственный
        /// путь материализации ряда — сам <see cref="SegmentRowProvider"/>.
        /// В реальной сцене это не так: <c>Movement.TunnelObstacleController</c>
        /// остаётся подключён (переходное состояние, docs/wiki/roadmap.md) и
        /// его <c>Movement.TunnelGridReveal</c> материализует плиты НАПРЯМУЮ
        /// (<c>TunnelGrid.GetOrCreateTile</c>), вообще не зная о
        /// <c>ClaimRow</c> — он писался до появления сегментной генерации и
        /// не был обновлён. Если <c>TunnelGridReveal</c> успевает
        /// материализовать ряд РАНЬШЕ, чем <see cref="SegmentRowProvider"/> его
        /// заявляет (правдоподобно на практике: <c>TunnelObstacleController</c>
        /// уже размещён на сцене и получает свой первый <c>Update()</c>
        /// раньше, чем <c>Bootstrap.RunBootstrap</c> успевает через
        /// <c>FindFirstObjectByType</c> найти <c>GridTraceInputController</c>
        /// и добавить <c>Generation.SegmentGenerationController</c> — то есть
        /// как минимум на первых <see cref="SegmentRowProvider.RowsAheadOfPlayer"/>
        /// рядах забега), обстакловый генератор откликается на уже
        /// незаявленную на тот момент плиту первым — <c>Tile.MarkLethalTrap</c>
        /// (guard на уже установленное значение) молча ОТБРАСЫВАЕТ тип,
        /// который затем пытается назначить шаблон, а не заглушенные полями
        /// (<c>MarkLever</c>/<c>MarkManaSource</c> и т.п.) — накладываются
        /// поверх, давая противоречивую плиту (например, одновременно Lava и
        /// Lever). Тест ниже — не гипотеза, воспроизводит механизм напрямую.
        /// <b>Не чинится в этой задаче</b> — только диагностика по прямому
        /// запросу постановки.
        /// </summary>
        [Test]
        public void RevealedBeforeClaimed_ObstacleGeneratorWins_TemplateTileTypeSilentlyLost()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));

            // Порядок конструирования — генератор ПОДПИСЫВАЕТСЯ на
            // TileMaterialized первым (конструктор TunnelObstacleGenerator
            // только подписывается, сам ничего не материализует), а уже
            // затем TunnelGridReveal материализует ряды 0..RowsAheadOfPlayer(8)
            // синхронно в СВОЁМ конструкторе — тот же порядок, что вероятен
            // на сцене: TunnelObstacleController уже там (оба его класса
            // создаются в одном Rebuild(), генератор первым по коду), а
            // SegmentRowProvider ниже появляется позже (RunBootstrap ещё не
            // подключил SegmentGenerationController в этот момент).
            using var obstacles = new TunnelObstacleGenerator(grid, AlwaysLava());
            using var reveal = new TunnelGridReveal(grid, trail);

            // Шаблон говорит: локальная (2,1) → абсолютная (3,1) — Pit.
            var tiles = new SegmentTileType[5, Width];
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < Width; c++)
                    tiles[r, c] = SegmentTileType.Open;
            tiles[2, 1] = SegmentTileType.Pit;

            var template = new SegmentTemplate("adversarial", 1, SegmentRewardTag.Coins, tiles);
            var selector = new SegmentSelector(new List<SegmentTemplate> { template }, new RunSeed(1));
            using var segments = new SegmentRowProvider(grid, trail, selector, _ => 1);

            var tile = grid.GetOrCreateTile(new GridCoordinate(3, 1));

            // Реальный тип плиты — то, что откатил обстакловый генератор
            // (Lava), НЕ то, что задумал автор шаблона (Pit): Tile.MarkLethalTrap
            // молча не сработал повторно (LethalTrap уже был установлен).
            Assert.AreEqual(LethalTrapType.Lava, tile.LethalTrap,
                "Тип шаблона (Pit) потерян — плиту раньше материализовал TunnelGridReveal, минуя ClaimRow.");
        }

        // Roll, гарантированно попадающий в диапазон Lava (см.
        // TileMaterialized_RollAtPitThreshold_MarksLethalTrapLava в
        // Core.Tests.TunnelObstacleGeneratorTests — тот же приём).
        private static System.Func<float> AlwaysLava() => () => TunnelObstacleGenerator.PitThreshold;
    }
}
