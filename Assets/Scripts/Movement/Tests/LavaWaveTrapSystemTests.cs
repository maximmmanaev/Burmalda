using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class LavaWaveTrapSystemTests
    {
        private const int Width = 5;

        private static (TunnelGrid grid, GridTraceTrail trail, RealTimeThreatScheduler scheduler) CreateTrail(GridCoordinate start)
        {
            var grid = new TunnelGrid(Width);
            var trail = new GridTraceTrail(grid, start);
            var scheduler = new RealTimeThreatScheduler();
            return (grid, trail, scheduler);
        }

        private static bool IsRowFullyLava(TunnelGrid grid, int row)
        {
            for (var column = 0; column < Width; column++)
            {
                if (!grid.TryGetTile(new GridCoordinate(row, column), out var tile)) return false;
                if (tile.LethalTrap != LethalTrapType.Lava) return false;
            }
            return true;
        }

        // TryGetTile, не GetOrCreateTile — иначе выбросило бы исключение
        // для рядов за пределами сетки (Row < 0, см. TunnelGrid.Contains),
        // которые как раз и нужно проверять в тестах на границу тоннеля.
        // Немате­риализованная плита по определению не тронута волной.
        private static bool IsRowUntouched(TunnelGrid grid, int row)
        {
            for (var column = 0; column < Width; column++)
            {
                if (grid.TryGetTile(new GridCoordinate(row, column), out var tile) && tile.LethalTrap.HasValue)
                    return false;
            }
            return true;
        }

        // Ходит по прямой колонке col, из start.Row в target.Row (только вперёд).
        private static void WalkForwardTo(GridTraceTrail trail, int fromRow, int toRow, int column)
        {
            for (var row = fromRow + 1; row <= toRow; row++)
                Assert.IsTrue(trail.TryAdvanceTo(new GridCoordinate(row, column)), $"шаг на ({row},{column}) должен был пройти");
        }

        [Test]
        public void PositionChanged_TrailReachesTrigger_DoesNotArmAnyRowImmediately()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);

            WalkForwardTo(trail, 0, 5, 2);

            Assert.IsFalse(IsRowFullyLava(grid, 5));
        }

        // Владелец, 2026-09-04: "включая случай, когда игрок в момент
        // активации триггера стоит именно в ряду триггера" — критический тест.
        [Test]
        public void Tick_PlayerStillOnTriggerRow_DoesNotConvertTriggerRow_WaitsUntilPlayerMovesForward()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 5, 2); // игрок стоит прямо на триггере

            lava.Tick(LavaWaveTrapSystem.RowStepSeconds);
            Assert.IsFalse(IsRowFullyLava(grid, 5), "ряд игрока не должен стать лавой немедленно");

            lava.Tick(LavaWaveTrapSystem.RowStepSeconds);
            lava.Tick(LavaWaveTrapSystem.RowStepSeconds);
            Assert.IsFalse(IsRowFullyLava(grid, 5), "волна обязана ждать, а не пропускать ряд навсегда");

            trail.TryAdvanceTo(new GridCoordinate(6, 2)); // игрок продвинулся вперёд — ряд 5 теперь позади него
            lava.Tick(LavaWaveTrapSystem.RowStepSeconds);

            Assert.IsTrue(IsRowFullyLava(grid, 5), "как только ряд оказался строго позади игрока, волна должна была его конвертировать");
        }

        [Test]
        public void Tick_ConvertsWholeRow_AllColumns()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 6, 2); // игрок уже прошёл дальше триггера

            lava.Tick(LavaWaveTrapSystem.RowStepSeconds);

            for (var column = 0; column < Width; column++)
                Assert.AreEqual(LethalTrapType.Lava, grid.GetOrCreateTile(new GridCoordinate(5, column)).LethalTrap, $"столбец {column} ряда 5 должен быть лавой");
        }

        // Issue #262 («LavaWaveTrapSystem не меняет тип плитки на Lava, только
        // неверную текстуру»): до фикса система ставила отдельный
        // C#-идентификатор LethalTrapType.LavaWave — реальный ТИП плитки
        // менялся (это подтверждено, см. статус задачи), но не на Lava, из-за
        // чего DebugVisuals.TileArtKindResolver попадал в общую ветку
        // TimedTrapActive (текстура-сигнатура Стрелы) вместо собственной
        // ветки Lava. Явная регрессия на конкретное значение enum — не
        // полагается на то, что тест выше ("должна быть лавой" в комментарии)
        // README-подобно подразумевает нужное значение.
        [Test]
        public void Tick_ConvertsRow_TileTypeIsExactlyLava_NotASeparateLavaWaveIdentifier()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 6, 2);

            lava.Tick(LavaWaveTrapSystem.RowStepSeconds);

            var converted = grid.GetOrCreateTile(new GridCoordinate(5, 2));
            Assert.IsTrue(converted.LethalTrap.HasValue, "тип плитки обязан фактически измениться, не только текстура");
            Assert.AreEqual(LethalTrapType.Lava, converted.LethalTrap,
                "плитка обязана получить ТОТ ЖЕ тип, что статичная Лава из генерации — так рендер-слой (DebugVisuals.TileArtKindResolver.Resolve, ветка LethalTrap==Lava) покажет текстуру лавы, а не общую TimedTrapActive (текстура Стрелы), и так плитка ведёт себя идентично статичной Лаве (issue #258 — проходима, но летальна через d20).");
        }

        [Test]
        public void Tick_SecondStep_ConvertsRowBehindTriggerRow()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 6, 2);
            lava.Tick(LavaWaveTrapSystem.RowStepSeconds); // ряд 5

            lava.Tick(LavaWaveTrapSystem.RowStepSeconds); // должен быть ряд 4

            Assert.IsTrue(IsRowFullyLava(grid, 4));
        }

        [Test]
        public void Tick_ConvertedRow_NeverRevertsToSafe()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 6, 2);
            lava.Tick(LavaWaveTrapSystem.RowStepSeconds); // ряд 5 — лава

            for (var i = 0; i < 10; i++) lava.Tick(LavaWaveTrapSystem.RowStepSeconds); // дальнейшие тики волны и её завершение

            Assert.IsTrue(IsRowFullyLava(grid, 5), "владелец: «отрезая путь назад» — необратимость и есть смысл ловушки");
        }

        [Test]
        public void Tick_FullSequence_ConvertsExactlyMaxRows_ThenStops()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(10, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 20, 2); // далеко впереди — инвариант никогда не блокирует

            for (var i = 0; i < LavaWaveTrapSystem.MaxRows; i++) lava.Tick(LavaWaveTrapSystem.RowStepSeconds);

            for (var row = 10; row > 10 - LavaWaveTrapSystem.MaxRows; row--)
                Assert.IsTrue(IsRowFullyLava(grid, row), $"ряд {row} должен быть частью волны (всего {LavaWaveTrapSystem.MaxRows} рядов)");

            var beyondCapRow = 10 - LavaWaveTrapSystem.MaxRows;
            Assert.IsTrue(IsRowUntouched(grid, beyondCapRow), "ряд за пределами лимита не должен быть тронут");

            lava.Tick(LavaWaveTrapSystem.RowStepSeconds); // седьмой тик — не должен ничего менять
            Assert.IsTrue(IsRowUntouched(grid, beyondCapRow));
        }

        [Test]
        public void Tick_WaveReachesStartOfTunnel_StopsEarly_BeforeReachingMaxRows()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(2, 2); // всего 3 валидных ряда позади (2, 1, 0)
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 10, 2); // далеко впереди

            lava.Tick(LavaWaveTrapSystem.RowStepSeconds); // ряд 2
            lava.Tick(LavaWaveTrapSystem.RowStepSeconds); // ряд 1
            lava.Tick(LavaWaveTrapSystem.RowStepSeconds); // ряд 0

            Assert.IsTrue(IsRowFullyLava(grid, 2));
            Assert.IsTrue(IsRowFullyLava(grid, 1));
            Assert.IsTrue(IsRowFullyLava(grid, 0));

            Assert.DoesNotThrow(() => lava.Tick(LavaWaveTrapSystem.RowStepSeconds), "волна должна тихо остановиться на границе тоннеля, не упасть на отрицательном ряду");
        }

        [Test]
        public void Tick_RowsOutsideWavePath_AreNeverAffected()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 6, 2);

            lava.Tick(LavaWaveTrapSystem.RowStepSeconds); // ряд 5

            Assert.IsTrue(IsRowUntouched(grid, 6), "ряд игрока не должен быть тронут");
            Assert.IsTrue(IsRowUntouched(grid, 3), "ряд, до которого волна ещё не дошла, не должен быть тронут");
        }

        [Test]
        public void PositionChanged_RevisitingAlreadyFiredTrigger_DoesNotStartSecondWave()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 5, 2); // первый визит — на самом триггере

            trail.TryAdvanceTo(new GridCoordinate(4, 2)); // назад
            trail.TryAdvanceTo(trigger); // повторный визит на триггер — не должен запустить вторую волну
            WalkForwardTo(trail, 5, 6, 2); // вперёд, за пределы ряда триггера

            lava.Tick(LavaWaveTrapSystem.RowStepSeconds);

            // Одна волна — ряд 5 стал лавой ровно один раз (это уже
            // покрыто Tick_ConvertsWholeRow), здесь важно, что ДАЛЬШЕ по
            // тикам не оказывается ДВУХ независимых волн, тикающих
            // одновременно — косвенно проверяется тем, что после ровно
            // MaxRows тиков волна корректно останавливается (см. следующую
            // проверку: ряд глубоко за пределами лимита остаётся нетронутым).
            for (var i = 0; i < LavaWaveTrapSystem.MaxRows; i++) lava.Tick(LavaWaveTrapSystem.RowStepSeconds);

            Assert.IsTrue(IsRowUntouched(grid, 5 - LavaWaveTrapSystem.MaxRows), "если бы вторая волна зарегистрировалась, лимит рядов был бы превышен");
        }

        [Test]
        public void Dispose_StopsReactingToFurtherPositionChangesAndTicks()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            lava.Dispose();

            WalkForwardTo(trail, 0, 6, 2);
            lava.Tick(LavaWaveTrapSystem.RowStepSeconds);

            Assert.IsTrue(IsRowUntouched(grid, 5));
        }

        // issue #249 (владелец, плейтест после Комнаты Босса): "лава заливает
        // ряд игрока". При разборе выяснилось, что сама волна свой инвариант
        // не нарушает (см. Tick_PlayerStillOnTriggerRow_... выше) — реальный
        // путь другой: Алтарь — постоянный чекпоинт для d20-Knockback
        // (RunState.ResolveHazard/GridTraceTrail.TeleportTo), но волна ничего
        // не знает про Tile.IsAltar и просто перезаписывает его в LavaWave,
        // как и любую другую плиту в своём диапазоне — Tile.TransitionToLethalTrap
        // намеренно пишет поверх ЛЮБОЙ прежней роли (см. её doc-комментарий).
        [Test]
        public void Tick_RowContainsAltar_NeverConvertsAltarTile_OnlyOtherColumns()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(6, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();

            // Ряд 3 — один из шести залитых волной (6,5,4,3,2,1) — Алтарь на нём.
            var altarCoordinate = new GridCoordinate(3, 0);
            grid.GetOrCreateTile(altarCoordinate).MarkAltar();

            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 7, 2); // за пределы триггера — волна вправе конвертировать весь свой диапазон

            for (var i = 0; i < LavaWaveTrapSystem.MaxRows; i++) lava.Tick(LavaWaveTrapSystem.RowStepSeconds);

            Assert.IsFalse(grid.GetOrCreateTile(altarCoordinate).LethalTrap.HasValue,
                "Алтарь — постоянный чекпоинт для Knockback (PRD 9), волна не имеет права превращать его в лаву");
            Assert.IsTrue(grid.GetOrCreateTile(altarCoordinate).IsAltar, "роль Алтаря должна сохраниться");

            // Защищается ТОЛЬКО сама плита Алтаря — остальные столбцы того же ряда всё равно становятся лавой как обычно.
            for (var column = 1; column < Width; column++)
                Assert.AreEqual(LethalTrapType.Lava, grid.GetOrCreateTile(new GridCoordinate(3, column)).LethalTrap,
                    $"столбец {column} ряда 3 не связан с Алтарём и должен был стать лавой как обычно");
        }

        // Сценарий из issue #249 — Алтарь, который PRD-капстон
        // (Generation.SegmentRowProvider.EnsureCoveredThrough) всегда ставит
        // непосредственно перед входом в Комнату Босса. Если рядом (в
        // пределах MaxRows) сработает триггер Лавы — например, содержимое,
        // "просочившееся" в первые ряды Комнаты до того, как
        // BossRoomGenerator успел заявить их себе (см. её doc-комментарий про
        // HasForeignRole) — волна не должна суметь дотянуться до этого
        // Алтаря, иначе следующий же Knockback телепортирует игрока прямо в
        // лаву (GridTraceTrail.TeleportTo цель не проверяет).
        [Test]
        public void Tick_AltarPrecedingBossRoomEntry_SurvivesNearbyLavaWave_RemainsSafeTeleportDestination()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));

            var altarCoordinate = new GridCoordinate(2, 2); // Алтарь капстона перед Комнатой
            grid.GetOrCreateTile(altarCoordinate).MarkAltar();

            grid.GetOrCreateTile(new GridCoordinate(8, 2)).MarkBoss(); // вход в Комнату Босса

            // Триггер Лавы в первых рядах Комнаты, в пределах MaxRows от Алтаря.
            var trigger = new GridCoordinate(6, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();

            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 9, 2);

            for (var i = 0; i < LavaWaveTrapSystem.MaxRows; i++) lava.Tick(LavaWaveTrapSystem.RowStepSeconds);

            Assert.IsFalse(grid.GetOrCreateTile(altarCoordinate).LethalTrap.HasValue,
                "Алтарь перед Комнатой Босса не должен сгорать от постороннего триггера Лавы внутри/рядом с Комнатой");

            // Симулируем сам Knockback (RunState.ResolveHazard) — телепорт на Алтарь обязан оставаться безопасным.
            trail.TeleportTo(altarCoordinate);
            Assert.IsFalse(grid.GetOrCreateTile(trail.CurrentPosition).LethalTrap.HasValue,
                "Knockback не должен ставить игрока на горящую плиту");
        }

        // issue #254 — то же ядро критерия приёмки, что у Стрелы/Лезвий (см.
        // ArrowWaveTrapSystemTests/BladeTactTrapSystemTests): игрок делает
        // ход ЗА триггер и дальше стоит на месте — волна обязана продолжать
        // жечь ряды позади него по одному только реальному времени. Это же
        // прямое исправление старого бага: раньше стоящий на месте игрок
        // был "в безопасности" от Лавы не по замыслу, а потому что волна
        // тикалась только на его собственные шаги.
        [Test]
        public void Tick_PlayerStandsStillForSeveralSeconds_WaveStillAdvancesOnRealTimeAlone()
        {
            var (grid, trail, scheduler) = CreateTrail(new GridCoordinate(0, 2));
            var trigger = new GridCoordinate(5, 2);
            grid.GetOrCreateTile(trigger).MarkLavaTrigger();
            using var lava = new LavaWaveTrapSystem(grid, trail, scheduler);
            WalkForwardTo(trail, 0, 6, 2); // последний ход за весь тест — дальше игрок стоит на месте, за пределами триггера

            for (var i = 0; i < 3; i++) lava.Tick(LavaWaveTrapSystem.RowStepSeconds);

            Assert.IsTrue(IsRowFullyLava(grid, 5), "ряд 5 (триггер) должен был стать лавой на первом тике");
            Assert.IsTrue(IsRowFullyLava(grid, 4), "ряд 4 должен был стать лавой на втором тике");
            Assert.IsTrue(IsRowFullyLava(grid, 3),
                "волна обязана была продвинуться на 3 ряда назад по одному только реальному времени, без единого дополнительного хода игрока");
        }
    }
}
