using NUnit.Framework;

namespace Burmalda.Generation.Tests
{
    public class SegmentReachabilityValidatorTests
    {
        private const int Width = 3;

        private static SegmentTileType[,] OpenRows(int rowCount)
        {
            var tiles = new SegmentTileType[rowCount, Width];
            for (var r = 0; r < rowCount; r++)
                for (var c = 0; c < Width; c++)
                    tiles[r, c] = SegmentTileType.Open;
            return tiles;
        }

        [Test]
        public void IsTraversable_FullyOpenTemplate_ReturnsTrue()
        {
            var template = new SegmentTemplate("open", 1, SegmentRewardTag.Coins, OpenRows(5));
            Assert.IsTrue(SegmentReachabilityValidator.IsTraversable(template));
        }

        [Test]
        public void IsTraversable_StraightWallSplittingEntryFromExit_ReturnsFalse()
        {
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++) tiles[2, c] = SegmentTileType.Blocked; // сплошная стена поперёк — вход и выход разъединены

            var template = new SegmentTemplate("wall", 1, SegmentRewardTag.Coins, tiles);
            Assert.IsFalse(SegmentReachabilityValidator.IsTraversable(template));
        }

        [Test]
        public void IsTraversable_WallWithGap_ReturnsTrue()
        {
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++) tiles[2, c] = SegmentTileType.Blocked;
            tiles[2, 1] = SegmentTileType.Open; // проход в стене

            var template = new SegmentTemplate("gap", 1, SegmentRewardTag.Coins, tiles);
            Assert.IsTrue(SegmentReachabilityValidator.IsTraversable(template));
        }

        [Test]
        public void IsTraversable_AllEntryCellsBlocked_ReturnsFalse()
        {
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++) tiles[0, c] = SegmentTileType.Blocked;

            var template = new SegmentTemplate("no-entry", 1, SegmentRewardTag.Coins, tiles);
            Assert.IsFalse(SegmentReachabilityValidator.IsTraversable(template));
        }

        [Test]
        public void IsTraversable_AllExitCellsBlocked_ReturnsFalse()
        {
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++) tiles[4, c] = SegmentTileType.Blocked;

            var template = new SegmentTemplate("no-exit", 1, SegmentRewardTag.Coins, tiles);
            Assert.IsFalse(SegmentReachabilityValidator.IsTraversable(template));
        }

        [Test]
        public void IsTraversable_LavaAndTriggers_DoNotBlockPath()
        {
            // Опасные-но-не-заблокированные плиты (лава/триггеры) —
            // проходимы для целей гарантии проходимости: игрок физически
            // МОЖЕТ пройти (рискуя), просто не должен. Заблокирован путь
            // считается только по Blocked/LeverGate.
            var tiles = OpenRows(5);
            tiles[1, 1] = SegmentTileType.Lava;
            tiles[2, 1] = SegmentTileType.ArrowWaveTrigger;
            tiles[3, 1] = SegmentTileType.BladeTactTrigger;

            var template = new SegmentTemplate("hazards", 1, SegmentRewardTag.Coins, tiles);
            Assert.IsTrue(SegmentReachabilityValidator.IsTraversable(template));
        }

        [Test]
        public void IsTraversable_OnlyPathRunsThroughLeverGate_ReturnsFalse()
        {
            // LeverGate закрыта по умолчанию (issue #51) — если единственный
            // путь идёт через неё, основной маршрут (не боковой) на деле
            // непроходим без активации рычага в другом месте шаблона.
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++) tiles[2, c] = SegmentTileType.Blocked;
            tiles[2, 1] = SegmentTileType.LeverGate;
            tiles[0, 0] = SegmentTileType.Lever; // рычаг где-то в шаблоне, не на пути к выходу

            var template = new SegmentTemplate("gate-only-path", 1, SegmentRewardTag.Artifact, tiles);
            Assert.IsFalse(SegmentReachabilityValidator.IsTraversable(template));
        }

        [Test]
        public void IsTraversable_DiagonalGapAroundWall_ReturnsTrue()
        {
            // Стена с диагональным обходом на краю (8-направленное соседство, как GridCoordinate.IsAdjacentTo).
            var tiles = OpenRows(5);
            tiles[2, 0] = SegmentTileType.Blocked;
            tiles[2, 1] = SegmentTileType.Blocked;
            // tiles[2,2] остаётся Open — диагональный проход из (1,1)/(1,2) в (3,2) и т.п.

            var template = new SegmentTemplate("diagonal", 1, SegmentRewardTag.Coins, tiles);
            Assert.IsTrue(SegmentReachabilityValidator.IsTraversable(template));
        }

        [Test]
        public void LeverVaultIsReachable_NoGateInTemplate_ReturnsTrue()
        {
            var template = new SegmentTemplate("no-gate", 1, SegmentRewardTag.Coins, OpenRows(5));
            Assert.IsTrue(SegmentReachabilityValidator.LeverVaultIsReachable(template));
        }

        [Test]
        public void LeverVaultIsReachable_GateConnectedToRestOfSegmentWhenOpen_ReturnsTrue()
        {
            // Ворота примыкают к открытой плите сбоку — при открытых воротах карман доступен.
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++) tiles[2, c] = SegmentTileType.Blocked;
            tiles[2, 1] = SegmentTileType.LeverGate;
            tiles[0, 0] = SegmentTileType.Lever;

            var template = new SegmentTemplate("gate-connected", 1, SegmentRewardTag.Artifact, tiles);
            Assert.IsTrue(SegmentReachabilityValidator.LeverVaultIsReachable(template));
        }

        [Test]
        public void LeverVaultIsReachable_GateSealedOnEverySideExceptVault_ReturnsFalse()
        {
            // issue #208: клетка ворот окружена Blocked со всех сторон, кроме
            // самой награды за ней — карман замкнут сам на себя, открытые
            // ворота ничего не меняют, к ним физически неоткуда подойти.
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++)
            {
                tiles[1, c] = SegmentTileType.Blocked;
                tiles[3, c] = SegmentTileType.Blocked;
            }
            tiles[2, 0] = SegmentTileType.Blocked;
            tiles[2, 1] = SegmentTileType.LeverGate;
            tiles[2, 2] = SegmentTileType.KeySource; // "тайник" — недостижим даже при открытых воротах
            tiles[0, 0] = SegmentTileType.Lever;

            var template = new SegmentTemplate("sealed-vault", 1, SegmentRewardTag.Keys, tiles);
            Assert.IsFalse(SegmentReachabilityValidator.LeverVaultIsReachable(template));
        }

        // Issue #193: явная проверка "возвратный маршрут существует", а не
        // расчёт на то, что кто-то выведет её из IsTraversable +
        // LeverVaultIsReachable (см. doc-комментарий метода).
        [Test]
        public void ReturnRouteToExitExists_NoGateInTemplate_ReturnsTrue()
        {
            var template = new SegmentTemplate("no-gate", 1, SegmentRewardTag.Coins, OpenRows(5));
            Assert.IsTrue(SegmentReachabilityValidator.ReturnRouteToExitExists(template));
        }

        [Test]
        public void ReturnRouteToExitExists_GateConnectedToRestOfSegmentWhenOpen_ReturnsTrue()
        {
            // Тот же шаблон, что LeverVaultIsReachable_GateConnectedToRestOfSegmentWhenOpen_ReturnsTrue —
            // подтверждает, что связь с рядом ВХОДА (тот метод) влечёт связь с рядом ВЫХОДА (этот).
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++) tiles[2, c] = SegmentTileType.Blocked;
            tiles[2, 1] = SegmentTileType.LeverGate;
            tiles[0, 0] = SegmentTileType.Lever;

            var template = new SegmentTemplate("gate-connected", 1, SegmentRewardTag.Artifact, tiles);
            Assert.IsTrue(SegmentReachabilityValidator.ReturnRouteToExitExists(template));
        }

        [Test]
        public void ReturnRouteToExitExists_GateSealedOnEverySideExceptVault_ReturnsFalse()
        {
            // Тот же замкнутый карман, что LeverVaultIsReachable_GateSealedOnEverySideExceptVault_ReturnsFalse.
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++)
            {
                tiles[1, c] = SegmentTileType.Blocked;
                tiles[3, c] = SegmentTileType.Blocked;
            }
            tiles[2, 0] = SegmentTileType.Blocked;
            tiles[2, 1] = SegmentTileType.LeverGate;
            tiles[2, 2] = SegmentTileType.KeySource;
            tiles[0, 0] = SegmentTileType.Lever;

            var template = new SegmentTemplate("sealed-vault", 1, SegmentRewardTag.Keys, tiles);
            Assert.IsFalse(SegmentReachabilityValidator.ReturnRouteToExitExists(template));
        }
    }
}
