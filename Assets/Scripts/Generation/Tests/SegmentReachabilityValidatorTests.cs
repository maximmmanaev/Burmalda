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
        public void IsTraversable_TriggerTiles_DoNotBlockPath()
        {
            // Триггеры (issues #213-#217) на этапе генерации — обычный
            // безопасный пол (GridTraceTrail.TryAdvanceTo их не отличает от
            // Open): опасность наступает ПОЗЖЕ, через систему-обработчик, не
            // в момент шага на саму плиту-триггер. Проходимы для этой
            // проверки заслуженно, в отличие от статичной Лавы (см. ниже).
            var tiles = OpenRows(5);
            tiles[1, 1] = SegmentTileType.ArrowWaveTrigger;
            tiles[2, 1] = SegmentTileType.BladeTactTrigger;
            tiles[3, 1] = SegmentTileType.LavaWaveTrigger;

            var template = new SegmentTemplate("triggers", 1, SegmentRewardTag.Coins, tiles);
            Assert.IsTrue(SegmentReachabilityValidator.IsTraversable(template));
        }

        // issue #251 (владелец, плейтест Ярус 3): "лава заливает всю ширину
        // тоннеля, пути вперёд нет". Разбор показал, что предыдущая версия
        // этого файла (см. git-историю) ошибочно считала статичную
        // SegmentTileType.Lava "проходимой, риск не стена" — но
        // GridTraceTrail.TryAdvanceTo категорически отклоняет ЛЮБОЙ шаг на
        // LethalTrap.HasValue (в т.ч. статичную Лаву) независимо от исхода
        // d20 — игрок физически НЕ МОЖЕТ пройти, не "не должен". В отличие
        // от триггеров выше, Lava — это уже LethalTrap с самого момента
        // генерации (SegmentRowProvider.ApplyTileType → MarkLethalTrap), не
        // отложенная угроза. Из-за этого пробела шаблон каталога
        // «лавовый-коридор» (сплошной ряд "lllll") годами проходил проверку,
        // будучи на самом деле гарантированным тупиком.
        [Test]
        public void IsTraversable_StaticLavaWithNoGap_ReturnsFalse()
        {
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++) tiles[2, c] = SegmentTileType.Lava; // сплошная лава поперёк, без единого прохода

            var template = new SegmentTemplate("lava-wall", 1, SegmentRewardTag.Coins, tiles);
            Assert.IsFalse(SegmentReachabilityValidator.IsTraversable(template),
                "статичная Лава непроходима так же, как Blocked — игрок физически не может на неё шагнуть");
        }

        [Test]
        public void IsTraversable_StaticLavaWithGap_ReturnsTrue()
        {
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++) tiles[2, c] = SegmentTileType.Lava;
            tiles[2, 1] = SegmentTileType.Open; // единственный проход

            var template = new SegmentTemplate("lava-wall-with-gap", 1, SegmentRewardTag.Coins, tiles);
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
