using System.Collections.Generic;
using NUnit.Framework;

namespace Burmalda.Core.Tests
{
    public class TunnelObstacleGeneratorTests
    {
        // Задача «сделать тоннель играбельным», часть 2: *Share — изменяемые
        // static-поля (дебаг-панель их трогает в рантайме), снимок/восстановление
        // вокруг КАЖДОГО теста — иначе тест, меняющий долю, отравил бы дефолты
        // для остальных тестов файла (порядок выполнения NUnit не гарантирован).
        private float _savedBlockedShare, _savedFallingRockShare, _savedLavaShare, _savedBombShare, _savedArrowWaveShare, _savedBladeTactShare;

        [SetUp]
        public void SaveShares()
        {
            _savedBlockedShare = TunnelObstacleGenerator.BlockedShare;
            _savedFallingRockShare = TunnelObstacleGenerator.FallingRockShare;
            _savedLavaShare = TunnelObstacleGenerator.LavaShare;
            _savedBombShare = TunnelObstacleGenerator.BombShare;
            _savedArrowWaveShare = TunnelObstacleGenerator.ArrowWaveShare;
            _savedBladeTactShare = TunnelObstacleGenerator.BladeTactShare;
        }

        [TearDown]
        public void RestoreShares()
        {
            TunnelObstacleGenerator.BlockedShare = _savedBlockedShare;
            TunnelObstacleGenerator.FallingRockShare = _savedFallingRockShare;
            TunnelObstacleGenerator.LavaShare = _savedLavaShare;
            TunnelObstacleGenerator.BombShare = _savedBombShare;
            TunnelObstacleGenerator.ArrowWaveShare = _savedArrowWaveShare;
            TunnelObstacleGenerator.BladeTactShare = _savedBladeTactShare;
        }

        private static System.Func<float> Sequence(params float[] rolls)
        {
            var queue = new Queue<float>(rolls);
            return () => queue.Dequeue();
        }

        [Test]
        public void TileMaterialized_RollBelowBlockedThreshold_MarksTileBlocked()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(0f));

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsTrue(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
        }

        [Test]
        public void TileMaterialized_RollAtBlockedThreshold_MarksFallingRockTrigger()
        {
            // Верхняя граница диапазона block — начало диапазона falling rock (полуинтервалы).
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.BlockedThreshold));

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsFalse(tile.IsBlocked);
            Assert.IsTrue(tile.IsFallingRockTrigger);
        }

        [Test]
        public void TileMaterialized_RollAtFallingRockThreshold_MarksLethalTrapLava()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.FallingRockThreshold));

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsFalse(tile.IsBlocked);
            Assert.AreEqual(LethalTrapType.Lava, tile.LethalTrap);
        }

        [Test]
        public void TileMaterialized_RollAtLavaThreshold_MarksBombTrigger()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.LavaThreshold));

            var trigger = new GridCoordinate(1, 0);
            var tile = grid.GetOrCreateTile(trigger);

            Assert.IsFalse(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
            Assert.IsTrue(tile.IsBombTrigger);
        }

        [Test]
        public void TileMaterialized_RollAtBombThreshold_MarksArrowWaveTrigger()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.BombThreshold));

            var trigger = new GridCoordinate(1, 0);
            var tile = grid.GetOrCreateTile(trigger);

            Assert.IsFalse(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
            Assert.AreEqual(2, tile.ArrowWaveTargetRow);
            Assert.AreEqual(RowWaveDirection.LeftToRight, tile.ArrowWaveDirection);
        }

        [Test]
        public void TileMaterialized_RollAtArrowWaveThreshold_MarksBladeTactTrigger()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.ArrowWaveThreshold));

            var trigger = new GridCoordinate(1, 0);
            var tile = grid.GetOrCreateTile(trigger);

            Assert.AreEqual(2, tile.BladeTactTargetRow);
        }

        [Test]
        public void TileMaterialized_RollAtOrAboveBladeTactThreshold_TileStaysPassable()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.BladeTactThreshold));

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsFalse(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
            Assert.IsFalse(tile.ArrowWaveTargetRow.HasValue);
            Assert.IsFalse(tile.BladeTactTargetRow.HasValue);
        }

        [Test]
        public void TileMaterialized_TargetOfArrowWaveTrigger_IsNotIndependentlyRolled()
        {
            // Плита-цель триггера — резервирована: даже если бы ей самой
            // выпал бросок "заблокировано", она должна остаться обычной
            // (см. комментарий про _reservedTrapTargets в генераторе).
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.BombThreshold, 0f));

            grid.GetOrCreateTile(new GridCoordinate(1, 0)); // становится триггером волны Стрел на ряд 2, резервирует цель
            var target = grid.GetOrCreateTile(new GridCoordinate(2, 0));

            Assert.IsFalse(target.IsBlocked);
            Assert.IsFalse(target.LethalTrap.HasValue);
        }

        [Test]
        public void TileMaterialized_TargetOfBladeTactTrigger_IsNotIndependentlyRolled()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.ArrowWaveThreshold, 0f));

            grid.GetOrCreateTile(new GridCoordinate(1, 0)); // становится триггером такта Лезвий на ряд 2, резервирует цель
            var target = grid.GetOrCreateTile(new GridCoordinate(2, 0));

            Assert.IsFalse(target.IsBlocked);
        }

        [Test]
        public void TileMaterialized_TargetOfArrowWaveTrigger_ResolvedOnceThenRollsNormallyAgain()
        {
            // Резервация одноразовая: другая, никак не связанная плита с той
            // же координатой (в теории — другой забег/сетка) не должна
            // случайно остаться навсегда защищённой от бросков.
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.BombThreshold, 0f));

            grid.GetOrCreateTile(new GridCoordinate(1, 0)); // триггер → резервирует (2, 0)
            grid.GetOrCreateTile(new GridCoordinate(2, 0)); // резервированная цель — бросок не тратится, снимается резервация
            var otherTile = grid.GetOrCreateTile(new GridCoordinate(3, 0)); // обычная плита — снова тратит бросок (второе значение очереди)

            Assert.IsTrue(otherTile.IsBlocked);
        }

        [Test]
        public void TileMaterialized_RowZero_NeverMarkedRegardlessOfRoll()
        {
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(0f));

            var tile = grid.GetOrCreateTile(new GridCoordinate(0, 2));

            Assert.IsFalse(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
        }

        [Test]
        public void TileMaterialized_RowZero_DoesNotConsumeRandomRoll()
        {
            // Если бы ряд 0 всё равно бросал кубик, второй Dequeue() на пустой очереди упал бы с исключением.
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(0f));

            grid.GetOrCreateTile(new GridCoordinate(0, 2));
            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsTrue(tile.IsBlocked);
        }

        // Разграничение по рядам с Generation.SegmentRowProvider (переходное
        // состояние сосуществования двух генераторов, docs/wiki/roadmap.md) —
        // проверка по конструкции: даже с бросками, которые ВСЕГДА требуют
        // "заблокировано", заявленный ряд остаётся нетронутым. Не полагается
        // на "договорённость" — это прямая проверка условия в OnTileMaterialized.
        [Test]
        public void TileMaterialized_RowClaimed_TileStaysUntouchedRegardlessOfRoll()
        {
            var grid = new TunnelGrid(5);
            grid.ClaimRow(1);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(0f, 0f, 0f, 0f, 0f)); // 0f — гарантированно ниже любого порога

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsFalse(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
            Assert.IsFalse(tile.ArrowWaveTargetRow.HasValue);
            Assert.IsFalse(tile.BladeTactTargetRow.HasValue);
        }

        [Test]
        public void TileMaterialized_RowClaimed_DoesNotConsumeRandomRoll()
        {
            // Если бы заявленный ряд всё равно бросал кубик, второй Dequeue() на пустой очереди упал бы с исключением.
            var grid = new TunnelGrid(5);
            grid.ClaimRow(1);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(0f));

            grid.GetOrCreateTile(new GridCoordinate(1, 0)); // заявленный ряд — не должен тронуть очередь
            var tile = grid.GetOrCreateTile(new GridCoordinate(2, 0)); // незаявленный — тратит единственное значение очереди

            Assert.IsTrue(tile.IsBlocked);
        }

        [Test]
        public void TileMaterialized_OnlySpecificRowClaimed_OtherRowsStillRollNormally()
        {
            var grid = new TunnelGrid(5);
            grid.ClaimRow(1);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(0f)); // только для незаявленного ряда 2

            var claimedTile = grid.GetOrCreateTile(new GridCoordinate(1, 0));
            var normalTile = grid.GetOrCreateTile(new GridCoordinate(2, 0));

            Assert.IsFalse(claimedTile.IsBlocked, "Заявленный ряд не трогается генератором");
            Assert.IsTrue(normalTile.IsBlocked, "Незаявленный ряд по-прежнему засеивается генератором");
        }

        // Задача «сделать тоннель играбельным», часть 2.
        [Test]
        public void CumulativeThresholds_AreComputedFromSharesInOrder()
        {
            TunnelObstacleGenerator.BlockedShare = 0.10f;
            TunnelObstacleGenerator.FallingRockShare = 0.05f;
            TunnelObstacleGenerator.LavaShare = 0.03f;
            TunnelObstacleGenerator.BombShare = 0.04f;
            TunnelObstacleGenerator.ArrowWaveShare = 0.02f;
            TunnelObstacleGenerator.BladeTactShare = 0.01f;

            Assert.AreEqual(0.10f, TunnelObstacleGenerator.BlockedThreshold, 1e-6f);
            Assert.AreEqual(0.15f, TunnelObstacleGenerator.FallingRockThreshold, 1e-6f);
            Assert.AreEqual(0.18f, TunnelObstacleGenerator.LavaThreshold, 1e-6f);
            Assert.AreEqual(0.22f, TunnelObstacleGenerator.BombThreshold, 1e-6f);
            Assert.AreEqual(0.24f, TunnelObstacleGenerator.ArrowWaveThreshold, 1e-6f);
            Assert.AreEqual(0.25f, TunnelObstacleGenerator.BladeTactThreshold, 1e-6f);
        }

        [Test]
        public void ChangingBlockedShare_AtRuntime_AffectsNextRoll_WithoutWaitingForNewRun()
        {
            // Тот же принцип живого применения, что у ползунков EconomyDebugPanel
            // для курсов конвертации (см. её doc-комментарий) — не "со следующего забега".
            TunnelObstacleGenerator.BlockedShare = 0.5f;
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(0.4f)); // ниже нового порога 0.5, выше старого дефолта

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsTrue(tile.IsBlocked);
        }

        [Test]
        public void ZeroShare_ForAGivenType_NeverRollsThatType()
        {
            // Ширина полосы 0 — тип пропускается полностью, следующий начинается сразу на том же пороге.
            TunnelObstacleGenerator.FallingRockShare = 0f;
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.BlockedThreshold));

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsFalse(tile.IsBlocked);
            Assert.AreEqual(LethalTrapType.Lava, tile.LethalTrap); // не FallingRock — полоса нулевой ширины
        }

        // Найдено на реальном билде (2026-09-01, Ярус 3 живого забега):
        // необработанный InvalidOperationException в рантайме — триггер
        // (Row=1) целился в Row=2, а Row=2 уже был заявлен сегментной
        // генерацией и получил от неё ManaSource/KeySource. Резервация
        // (_reservedTrapTargets) защищает только от собственного ролла
        // ЭТОГО генератора — не от содержимого, которое положит туда
        // сегмент. Два теста ниже — по одному на тип триггера с целью
        // впереди (Падающий камень/Бомба сами себе цель, см. CanReserveTarget
        // в генераторе).
        [Test]
        public void TileMaterialized_ArrowWaveTrigger_TargetRowAlreadyClaimed_DoesNotPlaceTriggerOrReserve()
        {
            var grid = new TunnelGrid(5);
            grid.ClaimRow(2); // ряд цели уже заявлен сегментной генерацией — как в баге на устройстве
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.BombThreshold, 0f));

            var trigger = grid.GetOrCreateTile(new GridCoordinate(1, 0));
            Assert.IsFalse(trigger.ArrowWaveTargetRow.HasValue, "Триггер не должен был поставиться — цель на уже заявленном ряду.");
            Assert.IsFalse(trigger.IsBlocked);
            Assert.IsFalse(trigger.LethalTrap.HasValue);

            // Заявленный ряд этот генератор не трогает вовсе (см. IsRowClaimed
            // выше в OnTileMaterialized) — но важно, что и РЕЗЕРВАЦИЯ не
            // висит: следующая НЕЗАЯВЛЕННАЯ плита тратит второе значение
            // очереди как обычная, не "снятие резервации" без последствий.
            var otherTile = grid.GetOrCreateTile(new GridCoordinate(3, 0));
            Assert.IsTrue(otherTile.IsBlocked);
        }

        [Test]
        public void TileMaterialized_BladeTactTrigger_TargetRowAlreadyClaimed_DoesNotPlaceTriggerOrReserve()
        {
            var grid = new TunnelGrid(5);
            grid.ClaimRow(2);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.ArrowWaveThreshold));

            var trigger = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsFalse(trigger.BladeTactTargetRow.HasValue, "Триггер не должен был поставиться — цель на уже заявленном ряду.");
        }

        [Test]
        public void TileMaterialized_ArrowWaveTrigger_TargetRowNotClaimed_StillPlacesTriggerNormally()
        {
            // Контрольный тест — фикс не должен был сломать обычный случай
            // (уже покрыт TileMaterialized_RollAtBombThreshold_MarksArrowWaveTrigger
            // выше, но явно проверяет именно CanReserveTarget=true ветку).
            var grid = new TunnelGrid(5);
            using var generator = new TunnelObstacleGenerator(grid, Sequence(TunnelObstacleGenerator.BombThreshold));

            var trigger = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.AreEqual(2, trigger.ArrowWaveTargetRow);
        }

        [Test]
        public void Dispose_StopsReactingToFurtherMaterializedTiles()
        {
            var grid = new TunnelGrid(5);
            var generator = new TunnelObstacleGenerator(grid, Sequence(0f));
            generator.Dispose();

            // После Dispose очередь бросков больше не трогается — исключения из пустой очереди не будет, плита останется непомеченной.
            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));

            Assert.IsFalse(tile.IsBlocked);
            Assert.IsFalse(tile.LethalTrap.HasValue);
        }
    }
}
