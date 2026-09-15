using System;
using System.Collections.Generic;
using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;

namespace Burmalda.Generation.Tests
{
    public class SegmentRowProviderTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail) CreateTrail()
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            return (grid, trail);
        }

        private static SegmentTileType[,] OpenRows(int rowCount, int width = Width)
        {
            var tiles = new SegmentTileType[rowCount, width];
            for (var r = 0; r < rowCount; r++)
                for (var c = 0; c < width; c++)
                    tiles[r, c] = SegmentTileType.Open;
            return tiles;
        }

        private static SegmentSelector SingleTemplateSelector(SegmentTemplate template, int seed = 1) =>
            new SegmentSelector(new List<SegmentTemplate> { template }, new RunSeed(seed));

        // Все шаблоны в этом файле — tier 1 (единственный кандидат в
        // каталоге-заглушке SingleTemplateSelector). maxDifficultyForRow
        // передаётся сюда как "_ => 1" (совпадает с тиром шаблона) — с
        // задачи «партии 1 и 2 + правила отбора» SegmentSelector фильтрует
        // ОКНОМ вокруг цели (по умолчанию [цель, цель+1]), не "<= цель":
        // "_ => 5" (прежнее значение, тест сам по себе к числу 5 не
        // привязан) больше не включал бы tier-1 в окно [5,6].

        [Test]
        public void Constructor_CoversRowsAheadOfPlayerImmediately()
        {
            var (grid, trail) = CreateTrail();
            var template = new SegmentTemplate("open-5", 1, SegmentRewardTag.Coins, OpenRows(5));
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);

            // 5-рядные шаблоны, старт row=0 => появляется минимум до row 8 (RowsAheadOfPlayer) включительно.
            Assert.IsTrue(grid.TryGetTile(new GridCoordinate(8, 0), out _));
        }

        [Test]
        public void ApplyTemplate_BlockedTile_MarksAbsoluteCoordinate()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[2, 1] = SegmentTileType.Blocked; // локально (2,1) -> глобально (baseRow=1) + 2 = row 3
            var template = new SegmentTemplate("with-block", 1, SegmentRewardTag.Coins, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);

            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(3, 1)).IsBlocked);
            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(3, 0)).IsBlocked);
        }

        [Test]
        public void ApplyTemplate_ArrowWaveTrigger_TargetsOwnRowLeftToRight()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[1, 2] = SegmentTileType.ArrowWaveTrigger; // baseRow=1 => глобально row 2

            var template = new SegmentTemplate("with-trigger", 1, SegmentRewardTag.Coins, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);

            var triggerTile = grid.GetOrCreateTile(new GridCoordinate(2, 2));
            Assert.AreEqual(2, triggerTile.ArrowWaveTargetRow);
            Assert.AreEqual(RowWaveDirection.LeftToRight, triggerTile.ArrowWaveDirection);
        }

        [Test]
        public void ApplyTemplate_BladeTactTrigger_TargetsOwnRow()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[1, 2] = SegmentTileType.BladeTactTrigger;

            var template = new SegmentTemplate("with-timed", 1, SegmentRewardTag.Coins, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);

            var triggerTile = grid.GetOrCreateTile(new GridCoordinate(2, 2));
            Assert.AreEqual(2, triggerTile.BladeTactTargetRow);
        }

        [Test]
        public void ApplyTemplate_ManaSource_MarksTile()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[1, 2] = SegmentTileType.ManaSource;

            var template = new SegmentTemplate("with-mana", 1, SegmentRewardTag.Mana, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);

            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(2, 2)).IsManaSource);
            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(2, 2)).IsKeySource);
        }

        [Test]
        public void ApplyTemplate_KeySource_MarksTile()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[1, 2] = SegmentTileType.KeySource;

            var template = new SegmentTemplate("with-key", 1, SegmentRewardTag.Keys, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);

            var tile = grid.GetOrCreateTile(new GridCoordinate(2, 2));
            Assert.IsTrue(tile.IsKeySource);
            Assert.IsFalse(tile.IsManaSource);
            Assert.IsFalse(tile.KeySourceAmount.HasValue, "обычный KeySource не должен нести явную сумму — решает CurrencyController.KeysPerSource");
        }

        // issue «размер награды за Воротами» (владелец, 2026-09-04).
        [Test]
        public void ApplyTemplate_GateVaultKeySource_MarksTileWithComputedAmount()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[1, 2] = SegmentTileType.GateVaultKeySource;
            var originalKeysPerPurchase = GateVaultPricing.KeysPerVaultPurchase;

            try
            {
                GateVaultPricing.KeysPerVaultPurchase = 80;
                var template = new SegmentTemplate("with-vault", 1, SegmentRewardTag.Keys, tiles, gateVaultPurchases: 1.5);
                using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);

                var tile = grid.GetOrCreateTile(new GridCoordinate(2, 2));
                Assert.IsTrue(tile.IsKeySource);
                Assert.AreEqual(120, tile.KeySourceAmount);
            }
            finally
            {
                GateVaultPricing.KeysPerVaultPurchase = originalKeysPerPurchase;
            }
        }

        [Test]
        public void ApplyTemplate_Altar_MarksTile()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[1, 2] = SegmentTileType.Altar;

            var template = new SegmentTemplate("with-altar", 1, SegmentRewardTag.Coins, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);

            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(2, 2)).IsAltar);
        }

        [Test]
        public void ApplyTemplate_Boss_MarksTile()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[1, 2] = SegmentTileType.Boss;

            var template = new SegmentTemplate("with-boss", 1, SegmentRewardTag.Coins, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);

            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(2, 2)).IsBoss);
        }

        [Test]
        public void ApplyTemplate_LeverAndGate_WiresAbsoluteGateCoordinates()
        {
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            tiles[1, 0] = SegmentTileType.Lever;   // -> row 2, col 0
            tiles[1, 1] = SegmentTileType.LeverGate; // -> row 2, col 1
            tiles[2, 1] = SegmentTileType.LeverGate; // -> row 3, col 1

            var template = new SegmentTemplate("with-lever", 1, SegmentRewardTag.Artifact, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);

            var lever = grid.GetOrCreateTile(new GridCoordinate(2, 0));
            Assert.IsTrue(lever.IsLever);
            CollectionAssert.AreEquivalent(
                new[] { new GridCoordinate(2, 1), new GridCoordinate(3, 1) },
                lever.LeverGateTargets);

            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(2, 1)).IsGated);
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(3, 1)).IsGated);

            // Issue #193: обе плиты-Ворота должны знать координату СВОЕГО
            // рычага — TunnelDebugVisual использует её для подсказки
            // направления на закрытых Воротах.
            var leverCoordinate = new GridCoordinate(2, 0);
            Assert.AreEqual(leverCoordinate, grid.GetOrCreateTile(new GridCoordinate(2, 1)).LeverCoordinate);
            Assert.AreEqual(leverCoordinate, grid.GetOrCreateTile(new GridCoordinate(3, 1)).LeverCoordinate);
        }

        [Test]
        public void EnsureCoveredThrough_AdvancesByWholeTemplatesNotPartialRows()
        {
            var (grid, trail) = CreateTrail();
            var template = new SegmentTemplate("open-8", 1, SegmentRewardTag.Coins, OpenRows(8));
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);

            provider.EnsureCoveredThrough(9); // запрос на 9, но шаблон занимает по 8 рядов — покрытие прыгнет минимум до row 8

            Assert.IsTrue(grid.TryGetTile(new GridCoordinate(8, 0), out _));
        }

        [Test]
        public void Constructor_TemplateWidthMismatch_Throws()
        {
            var (grid, trail) = CreateTrail();
            var template = new SegmentTemplate("wrong-width", 1, SegmentRewardTag.Coins, OpenRows(5, width: 3));

            Assert.Throws<InvalidOperationException>(() =>
                new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000));
        }

        [Test]
        public void PositionChanged_PlayerAdvances_CoversFurtherRows()
        {
            var (grid, trail) = CreateTrail();
            var template = new SegmentTemplate("open-5", 1, SegmentRewardTag.Coins, OpenRows(5));
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);

            for (var row = 1; row <= 6; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            // playerRow=6 + RowsAheadOfPlayer(8) = 14 должно быть покрыто.
            Assert.IsTrue(grid.TryGetTile(new GridCoordinate(14, 0), out _));
        }

        [Test]
        public void Dispose_StopsReactingToFurtherPositionChanges()
        {
            var (grid, trail) = CreateTrail();
            var template = new SegmentTemplate("open-5", 1, SegmentRewardTag.Coins, OpenRows(5));
            var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);
            var coveredBefore = grid.TryGetTile(new GridCoordinate(8, 0), out _); // покрыто ещё в конструкторе (rows 1..10)
            provider.Dispose();

            // Трейл идёт только по столбцу 2 — если бы provider всё ещё
            // реагировал, применение сегментов материализовало бы ВСЮ
            // ширину (см. ApplyTemplate), включая столбец 0 на дальних рядах.
            for (var row = 1; row <= 20; row++)
                trail.TryAdvanceTo(new GridCoordinate(row, 2));

            Assert.IsTrue(coveredBefore);
            Assert.IsFalse(grid.TryGetTile(new GridCoordinate(15, 0), out _), "после Dispose сегменты по всей ширине больше не применяются");
        }

        // Задача «параметр плотности» (владелец, 2026-09-08) — тот же
        // save/restore принцип, что TunnelObstacleGeneratorTests для *Share:
        // ExtraTrapDensity.Chance — mutable static, тест, меняющий её, не
        // должен отравлять дефолт для остальных тестов файла (порядок
        // выполнения NUnit не гарантирован).
        private float _savedExtraTrapChance;

        [SetUp]
        public void SaveExtraTrapChance() => _savedExtraTrapChance = ExtraTrapDensity.Chance;

        [TearDown]
        public void RestoreExtraTrapChance() => ExtraTrapDensity.Chance = _savedExtraTrapChance;

        [Test]
        public void ApplyTemplate_ExtraTrapChanceZero_OpenTileStaysOpen()
        {
            // 0 — нейтральное значение (владелец: "стартовое значение —
            // нейтральное") — гарантирует, что все ОСТАЛЬНЫЕ тесты этого
            // файла (которые ExtraTrapDensity вообще не трогают) остаются
            // полностью детерминированными.
            ExtraTrapDensity.Chance = 0f;
            var (grid, trail) = CreateTrail();
            var template = new SegmentTemplate("open-5-density-off", 1, SegmentRewardTag.Coins, OpenRows(5));
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));
            Assert.IsFalse(tile.LethalTrap.HasValue);
            Assert.IsFalse(tile.ArrowWaveTargetRow.HasValue);
            Assert.IsFalse(tile.IsBombTrigger);
            Assert.IsFalse(tile.BladeTactTargetRow.HasValue);
            Assert.IsFalse(tile.IsFallingRockTrigger);
            Assert.IsFalse(tile.IsLavaTrigger);
        }

        [Test]
        public void ApplyTemplate_ExtraTrapChanceOne_OpenTileBecomesSomeTrap()
        {
            // 1 — гарантированно конвертирует КАЖДУЮ Open-плиту материализуемого
            // сегмента в один из пяти типов (какой именно — равновероятный
            // случайный выбор, см. SegmentRowProvider.ApplyExtraTrapDensity) —
            // проверяем только сам факт превращения, не конкретный тип.
            ExtraTrapDensity.Chance = 1f;
            var (grid, trail) = CreateTrail();
            var template = new SegmentTemplate("open-5-density-max", 1, SegmentRewardTag.Coins, OpenRows(5));
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);

            var tile = grid.GetOrCreateTile(new GridCoordinate(1, 0));
            var isSomeTrap = tile.LethalTrap.HasValue || tile.ArrowWaveTargetRow.HasValue || tile.IsBombTrigger ||
                              tile.BladeTactTargetRow.HasValue || tile.IsFallingRockTrigger || tile.IsLavaTrigger;
            Assert.IsTrue(isSomeTrap, "при шансе 1 Open-плита должна была стать каким-то триггером ловушки.");
        }

        // Задача «награда никогда не лежит на ловушке» (владелец, Спринт
        // «Стены вместо ловушек», требование 3): ExtraTrapDensity роняет
        // триггер СЛУЧАЙНО на любую Open-плиту — без страховки он мог бы
        // поставить под угрозу существующую награду шаблона, даже если сам
        // авторский шаблон целиком безопасен (RewardTrapConflictValidator
        // ловит только статичные конфликты каталога, не рантайм-рулетку).
        // Шаблон ниже — Мана в центре, ВСЁ остальное Open — максимизирует
        // площадь, где ExtraTrapDensity может выстрелить рядом с наградой;
        // при шансе 1 почти наверняка выстрелит несколько раз за один
        // прогон. Проверка — НЕЗАВИСИМАЯ от RewardTrapConflictValidator
        // (не вызывает её), иначе тест доказывал бы только то, что
        // производственный код согласен сам с собой, а не то, что
        // инвариант реально соблюдается на живой сетке.
        [Test]
        public void ApplyTemplate_ExtraTrapChanceOne_NeverEndangersExistingReward()
        {
            ExtraTrapDensity.Chance = 1f;
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            const int rewardRow = 2;
            const int rewardColumn = 2;
            tiles[rewardRow, rewardColumn] = SegmentTileType.ManaSource;
            var template = new SegmentTemplate("open-with-mana-density-max", 1, SegmentRewardTag.Mana, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);

            for (var r = 0; r < 5; r++)
            for (var c = 0; c < Width; c++)
            {
                if (r == rewardRow && c == rewardColumn) continue; // сама награда
                var candidate = grid.GetOrCreateTile(new GridCoordinate(1 + r, c));

                if (candidate.ArrowWaveTargetRow.HasValue || candidate.BladeTactTargetRow.HasValue)
                    Assert.AreNotEqual(rewardRow, r, $"({r},{c}): Стрела/Лезвия задевают весь СВОЙ ряд — награда в том же ряду оказалась бы под угрозой.");

                if (candidate.IsBombTrigger)
                {
                    var withinBombRadius = Math.Abs(r - rewardRow) <= BombTrapSystem.RadiusTiles && Math.Abs(c - rewardColumn) <= BombTrapSystem.RadiusTiles;
                    Assert.IsFalse(withinBombRadius, $"({r},{c}): Бомба в радиусе {BombTrapSystem.RadiusTiles} от награды.");
                }

                if (candidate.IsLavaTrigger)
                {
                    var rewardBehindTrigger = rewardRow <= r && rewardRow > r - LavaWaveTrapSystem.MaxRows;
                    Assert.IsFalse(rewardBehindTrigger, $"({r},{c}): волна Лавы дошла бы до ряда награды {rewardRow}.");
                }
            }

            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(1 + rewardRow, rewardColumn)).IsManaSource, "сама награда должна остаться на месте.");
        }

        [Test]
        public void RowZero_NeverTouchedByTemplateApplication()
        {
            // Первый применённый шаблон всегда начинается с baseRow=1 (row 0
            // уже безопасен по построению трейла, см. EnsureCoveredThrough) —
            // даже шаблон, чей ЛОКАЛЬНЫЙ ряд 0 заблокирован, не может задеть
            // глобальный ряд 0.
            var (grid, trail) = CreateTrail();
            var tiles = OpenRows(5);
            for (var c = 0; c < Width; c++) tiles[0, c] = SegmentTileType.Blocked;
            var template = new SegmentTemplate("blocked-local-row0", 1, SegmentRewardTag.Coins, tiles);
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(template), _ => 1, rowsPerTier: 1000000);

            Assert.IsFalse(grid.GetOrCreateTile(new GridCoordinate(0, 2)).IsBlocked, "стартовый ряд должен оставаться безопасным (#9)");
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(1, 2)).IsBlocked, "локальный ряд 0 шаблона применяется к глобальному ряду 1, не 0");
        }

        // Задача «Алтари на 1/3 и 2/3 Яруса» (владелец, Спринт «Стены вместо
        // ловушек», задача 5). Заполнитель — фиксированный 5-рядный Open-
        // шаблон через SingleTemplateSelector: детерминирует ТОЧНЫЕ номера
        // рядов, на которых встанут landmark'ы (Alтарь/Комната по-прежнему
        // применяются напрямую из SegmentTemplateCatalog, не через
        // селектор — заполнитель нужен только для предсказуемости
        // заполнения МЕЖДУ ними). Числа ниже перепроверены отдельным
        // Python-скриптом, воспроизводящим ту же логику построчно.
        private static SegmentTemplate FiveRowFiller() => new SegmentTemplate("filler-5", 1, SegmentRewardTag.Coins, OpenRows(5));

        [Test]
        public void EnsureCoveredThrough_RowsPerTier40_PlacesAltarOneNearOneThird()
        {
            var (grid, trail) = CreateTrail();
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(FiveRowFiller()), _ => 1, rowsPerTier: 40);

            provider.EnsureCoveredThrough(50);

            // AltarTemplate: 6 рядов, 'A' на локальном ряду 1 → абсолютный ряд 17
            // (баланс заполнителей 5-рядными сегментами от ряда 1 до первого
            // ряда >= 40/3=13 включительно даёт старт Алтаря на ряду 16).
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(17, 2)).IsAltar, "Алтарь #1 должен быть рядом с 1/3 Яруса (40/3≈13), не в конце");
        }

        [Test]
        public void EnsureCoveredThrough_RowsPerTier40_PlacesAltarTwoNearTwoThirds()
        {
            var (grid, trail) = CreateTrail();
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(FiveRowFiller()), _ => 1, rowsPerTier: 40);

            provider.EnsureCoveredThrough(50);

            // Второй Алтарь стартует на ряду 27 (см. doc-комментарий теста
            // выше про арифметику) — 'A' на абсолютном ряду 28.
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(28, 2)).IsAltar, "Алтарь #2 должен быть рядом с 2/3 Яруса (40*2/3≈26), не в конце");
        }

        [Test]
        public void EnsureCoveredThrough_RowsPerTier40_PlacesBossAtEndOfTier()
        {
            var (grid, trail) = CreateTrail();
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(FiveRowFiller()), _ => 1, rowsPerTier: 40);

            provider.EnsureCoveredThrough(50);

            // BossTemplate: 5 рядов, 'B' на локальном ряду 2 → Комната стартует
            // на ряду 43, вход в Комнату на абсолютном ряду 45.
            Assert.IsTrue(grid.GetOrCreateTile(new GridCoordinate(45, 2)).IsBoss, "Комната Босса должна остаться в конце Яруса");
        }

        [Test]
        public void EnsureCoveredThrough_ExactlyTwoAltarsAndOneBossPerTier()
        {
            // PRD v9: ровно два Алтаря перед каждой Комнатой Босса — не
            // больше и не меньше, независимо от того, где именно внутри
            // Яруса они оказались.
            var (grid, trail) = CreateTrail();
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(FiveRowFiller()), _ => 1, rowsPerTier: 40);

            provider.EnsureCoveredThrough(50);

            var altarCount = 0;
            var bossCount = 0;
            for (var r = 1; r <= 50; r++)
                for (var c = 0; c < Width; c++)
                {
                    var tile = grid.GetOrCreateTile(new GridCoordinate(r, c));
                    if (tile.IsAltar) altarCount++;
                    if (tile.IsBoss) bossCount++;
                }

            Assert.AreEqual(2, altarCount, "ровно два Алтаря за Ярус");
            Assert.AreEqual(1, bossCount, "ровно одна Комната Босса за Ярус");
        }

        // Каждый landmark отмечен ровно ОДНОЙ плитой в столбце 2 (символ
        // 'A'/'B' — единственное вхождение в своём шаблоне, см.
        // SegmentTemplateCatalog), не всем своим рядам целиком — метка стоит
        // со смещением от начала сегмента (Алтарь: локальный ряд 1 из 6,
        // Комната: локальный ряд 2 из 5). Если бы два landmark'а стояли
        // ВПЛОТНУЮ (нулевой зазор), расстояние между их метками равнялось бы
        // РОВНО этой сумме смещений/размеров — StuckMarkerGap ниже. Реальный
        // зазор обязан быть СТРОГО больше на минимум
        // MinRowsBetweenLandmarks (задача 5) — тест не завязан на точный
        // размер заполнителей селектора (5-8 рядов в реальной игре, ровно 5
        // здесь только для предсказуемости прогона), только на гарантию
        // "не меньше минимума".
        private const int AltarMarkerOffset = 1; // AltarTemplate: 'A' на локальном ряду 1
        private const int BossMarkerOffset = 2;  // BossTemplate: 'B' на локальном ряду 2

        private static int StuckMarkerGap(bool prevIsBoss, bool nextIsBoss)
        {
            var prevRowCount = prevIsBoss ? 5 : 6; // BossTemplate/AltarTemplate.RowCount
            var prevOffset = prevIsBoss ? BossMarkerOffset : AltarMarkerOffset;
            var nextOffset = nextIsBoss ? BossMarkerOffset : AltarMarkerOffset;
            return (prevRowCount - prevOffset) + nextOffset;
        }

        [Test]
        public void EnsureCoveredThrough_LandmarksNeverAdjacent_EvenAcrossManyTiers()
        {
            // Владелец, задача 5: "Алтари не должны слипаться". Прогон
            // достаточно далеко, чтобы захватить несколько Ярусов подряд —
            // включая переход Комната одного Яруса → Алтарь #1 следующего
            // (та же болезнь могла бы повториться на границе, см.
            // doc-комментарий BeginTier).
            var (grid, trail) = CreateTrail();
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(FiveRowFiller()), _ => 1, rowsPerTier: 40);

            const int scanThrough = 200;
            provider.EnsureCoveredThrough(scanThrough);

            AssertNoLandmarksStuck(grid, scanThrough, minExpectedLandmarks: 6);
        }

        [Test]
        public void EnsureCoveredThrough_ShortTier_LandmarksStillNotAdjacent()
        {
            // Короткий Ярус — 1/3 и 2/3 номинально попадают в один и тот же
            // уже применённый сегмент (Алтарь #1 своими 6 рядами уже
            // перекрывает номинальные 2/3 при rowsPerTier=6). Второй Алтарь
            // и Комната обязаны всё равно появиться, с гарантированным
            // зазором, не пропуститься и не слипнуться.
            var (grid, trail) = CreateTrail();
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(FiveRowFiller()), _ => 1, rowsPerTier: 6);

            const int scanThrough = 60;
            provider.EnsureCoveredThrough(scanThrough);

            AssertNoLandmarksStuck(grid, scanThrough, minExpectedLandmarks: 3);
        }

        private void AssertNoLandmarksStuck(TunnelGrid grid, int scanThrough, int minExpectedLandmarks)
        {
            var markers = new List<(int row, bool isBoss)>();
            for (var r = 1; r <= scanThrough; r++)
            {
                var tile = grid.GetOrCreateTile(new GridCoordinate(r, 2));
                if (tile.IsAltar) markers.Add((r, false));
                if (tile.IsBoss) markers.Add((r, true));
            }

            Assert.GreaterOrEqual(markers.Count, minExpectedLandmarks, $"прогон на {scanThrough} рядов должен захватить хотя бы {minExpectedLandmarks} landmark'ов");

            for (var i = 1; i < markers.Count; i++)
            {
                var (prevRow, prevIsBoss) = markers[i - 1];
                var (row, isBoss) = markers[i];
                var actualGap = row - prevRow;
                var minAllowedGap = StuckMarkerGap(prevIsBoss, isBoss) + MinRowsBetweenLandmarksForTests;
                Assert.Greater(actualGap, StuckMarkerGap(prevIsBoss, isBoss),
                    $"landmark на ряду {row} слип с предыдущим на ряду {prevRow} — нулевой реальный зазор между сегментами");
                Assert.GreaterOrEqual(actualGap, minAllowedGap,
                    $"зазор между landmark'ами на рядах {prevRow} и {row} меньше гарантированного минимума ({MinRowsBetweenLandmarksForTests} рядов заполнителя)");
            }
        }

        // Копия SegmentRowProvider.MinRowsBetweenLandmarks (private) — тест
        // намеренно не читает internal-детали реализации, а знает ТУ ЖЕ
        // договорённость с постановки задачи (SegmentTemplate.MinRowCount).
        private const int MinRowsBetweenLandmarksForTests = SegmentTemplate.MinRowCount;

        [Test]
        public void EnsureCoveredThrough_AltarAndBoss_AreNotChosenBySelector()
        {
            // Детерминированный поток, не лотерея SegmentSelector (PRD v9) —
            // единственный шаблон в пуле селектора НЕ содержит ни Алтаря, ни
            // Комнаты, поэтому появление обоих может быть только прямой
            // вставкой SegmentTemplateCatalog.AltarTemplate/BossTemplate,
            // минуя селектор целиком.
            var (grid, trail) = CreateTrail();
            using var provider = new SegmentRowProvider(grid, trail, SingleTemplateSelector(FiveRowFiller()), _ => 1, rowsPerTier: 40);

            provider.EnsureCoveredThrough(50);

            var sawAltar = false;
            var sawBoss = false;
            for (var r = 1; r <= 50; r++)
            {
                var tile = grid.GetOrCreateTile(new GridCoordinate(r, 2));
                if (tile.IsAltar) sawAltar = true;
                if (tile.IsBoss) sawBoss = true;
            }

            Assert.IsTrue(sawAltar, "Алтарь обязан появиться, хотя заполнитель-селектор его не содержит");
            Assert.IsTrue(sawBoss, "Комната обязана появиться, хотя заполнитель-селектор её не содержит");
        }
    }
}
