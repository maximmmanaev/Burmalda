using System;
using System.Collections.Generic;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Movement;

namespace Burmalda.BossRoom
{
    /// <summary>
    /// Интеграция Комнаты Босса с забегом (вертикальный срез, задача
    /// «Комната Босса», владелец, 2026-09-02): вход по <see cref="Tile.IsBoss"/>,
    /// генерация содержимого (<see cref="BossRoomGenerator"/>), сбор Жилы/
    /// Резонанса/Эха, волна, выход с начислением счёта в Кристаллы Маны
    /// забега. По паттерну <c>Altar.AltarTriggerSystem</c>/
    /// <c>Boss.BossEncounterSystem</c> — обычный <see cref="IDisposable"/>
    /// класс с логикой, подписан на <see cref="GridTraceTrail"/>,
    /// тикается тонким driver'ом (<see cref="BossRoomController"/>).
    ///
    /// Реагирует на <see cref="GridTraceTrail.Advanced"/> для входа/сбора —
    /// только НОВАЯ плита (#61): вход и каждая Жила/Резонанс/Эхо
    /// засчитываются ровно один раз, повторный проход по уже собранной
    /// плите Комнаты — не-op. На <see cref="GridTraceTrail.PositionChanged"/>
    /// — для проверки волны/выхода: нужна КАЖДАЯ позиция игрока, не только
    /// новая плита (тот же принцип, что и у камеры, см. её докстрингу).
    ///
    /// <paramref name="reportBossDefeat"/> в конструкторе — колбэк, не
    /// прямая ссылка на <c>RunLifecycle.RunState</c> (тот же паттерн, что
    /// <c>Boss.BossController.ReportBossDefeat</c> — см. её докстрингу про
    /// причину: вызывающий Controller читает актуальный RunState заново на
    /// каждый вызов, не захватывает инстанс, который может устареть к
    /// моменту, когда волна реально догонит).
    /// </summary>
    public sealed class BossRoomRunSystem : IDisposable
    {
        // Черновое значение — предмет баланса (Спринт 18). PRD v8 §8.4 не
        // называет число, только "наступает сзади" — тот же приём, что
        // BossWave.DefaultStartingMarginRows/BossRoomGenerator.*Count*.
        public static float WaveRowsPerSecond = 1.5f;

        // Тот же смысл и тот же запас, что Movement.TunnelGridReveal.RowsAheadOfPlayer/
        // Generation.SegmentRowProvider.RowsAheadOfPlayer (дублирование по
        // тому же принципу, что и у них — см. их doc-комментарии, отдельная
        // ссылка на сборку Burmalda.Generation ради одной константы не
        // стоит того). Здесь — окно опроса Tick(), не материализации.
        private const int BossTileScanRowsAhead = 8;

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly RunCurrencyAccumulator _mana;
        private readonly Action<string> _reportBossDefeat;
        private readonly BossRoomGenerator _generator;
        private readonly HashSet<int> _generatedBossEntryRows = new HashSet<int>();
        private bool _disposed;

        public BossRoomRunSystem(TunnelGrid grid, GridTraceTrail trail, RunCurrencyAccumulator mana, Func<float> random01, Action<string> reportBossDefeat)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _mana = mana ?? throw new ArgumentNullException(nameof(mana));
            _reportBossDefeat = reportBossDefeat ?? throw new ArgumentNullException(nameof(reportBossDefeat));
            if (random01 == null) throw new ArgumentNullException(nameof(random01));
            _generator = new BossRoomGenerator(grid, random01);

            _trail.Advanced += OnAdvanced;
            _trail.PositionChanged += OnPositionChanged;
        }

        /// <summary>Активная Комната текущего захода — null до входа в Комнату (см. doc-комментарий <see cref="BossRoom"/>).</summary>
        public BossRoom ActiveRoom { get; private set; }

        /// <summary>Отписывается от трейла. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.Advanced -= OnAdvanced;
            _trail.PositionChanged -= OnPositionChanged;
            _disposed = true;
        }

        /// <summary>
        /// Тикает волну реальным временем — вызывать из Update() владеющего
        /// MonoBehaviour. Заодно опрашивает окно впереди игрока на предмет
        /// уже материализованной плиты-Босса — см. <see cref="ScanForUpcomingBossTile"/>.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            ActiveRoom?.TickWave(deltaSeconds);
            ScanForUpcomingBossTile();
        }

        /// <summary>
        /// Баг с устройства (владелец, 2026-09-04, «вход в Комнату блокирует
        /// движение»): <see cref="Movement.TunnelGridReveal"/> (плюс
        /// <c>Generation.SegmentRowProvider</c> для авторских шаблонов)
        /// материализует ряды на несколько рядов ВПЕРЕДИ игрока непрерывно,
        /// независимо от Комнаты Босса — к моменту, когда игрок реально
        /// ступает на плиту-Босса и раньше только ТОГДА запускалась
        /// генерация Комнаты, первые несколько рядов Комнаты УЖЕ были
        /// материализованы и обкатаны <c>Core.TunnelObstacleGenerator</c>
        /// (тот уважает <see cref="TunnelGrid.IsRowClaimed"/>, но ряды
        /// Комнаты на тот момент ещё не заявлены — заявка происходила
        /// только при шаге). Результат — случайная стена/яма/ловушка от
        /// чужого генератора могла перекрыть столбцы одного из первых рядов
        /// Комнаты, и <see cref="BossRoomGenerator.HasForeignRole"/>
        /// корректно пропускал такую плиту (не портил чужую роль), но и не
        /// снимал её непроходимость — тупик без выхода.
        ///
        /// Фикс — генерировать содержимое Комнаты не в момент шага на плиту-
        /// Босса, а как только эта плита где-то в окне впереди игрока
        /// материализуется с ролью Босса. <see cref="TunnelGrid.TileMaterialized"/>
        /// для этого НЕ подходит: он стреляет прямо внутри
        /// <c>TunnelGrid.GetOrCreateTile</c>, ДО того как вызывающая сторона
        /// (<c>SegmentRowProvider.ApplyTileType</c>) успевает вызвать
        /// <c>Tile.MarkBoss()</c> на уже созданной плите — <c>tile.IsBoss</c>
        /// в момент события ещё false. Опрос раз в кадр (не событие) снимает
        /// эту гонку: к следующему Update() вся синхронная материализация
        /// текущего хода уже отработала целиком.
        /// </summary>
        private void ScanForUpcomingBossTile()
        {
            var fromRow = _trail.CurrentPosition.Row;
            var toRow = fromRow + BossTileScanRowsAhead;
            for (var row = fromRow; row <= toRow; row++)
            {
                if (_generatedBossEntryRows.Contains(row)) continue;

                for (var column = 0; column < _grid.Width; column++)
                {
                    if (!_grid.TryGetTile(new GridCoordinate(row, column), out var tile) || !tile.IsBoss) continue;
                    EnsureRoomGenerated(row);
                    break;
                }
            }
        }

        /// <summary>Генерирует содержимое Комнаты ровно один раз на вход — не-op при повторном вызове с тем же <paramref name="entryRow"/>.</summary>
        private void EnsureRoomGenerated(int entryRow)
        {
            if (!_generatedBossEntryRows.Add(entryRow)) return;
            _generator.Generate(entryRow, BossRoomGenerator.DefaultRoomLengthRows);
        }

        private void OnAdvanced(GridCoordinate coordinate)
        {
            if (!_grid.TryGetTile(coordinate, out var tile)) return;

            if (ActiveRoom == null)
            {
                // Подстраховка: обычно к этому моменту ScanForUpcomingBossTile
                // уже сгенерировал Комнату несколько кадров назад — но если
                // почему-то не успел (первый же ход забега, до первого
                // Tick()), EnsureRoomGenerated — не-op в остальных случаях.
                if (tile.IsBoss) EnterRoom(coordinate.Row);
                return;
            }

            if (!ActiveRoom.IsActive) return;
            CollectTile(tile);
        }

        private void OnPositionChanged(GridCoordinate coordinate)
        {
            if (ActiveRoom == null || !ActiveRoom.IsActive) return;

            ActiveRoom.ReportPlayerRow(coordinate.Row);

            if (ActiveRoom.HasBeenCaughtByWave)
            {
                // PRD v8 §8.4: касание волны = смерть, не спасение впритык.
                _reportBossDefeat("Комната Босса: догнала наступающая волна");
                return;
            }

            if (ActiveRoom.HasExited)
                _mana.Add(ActiveRoom.AccumulatedIncome);
        }

        private void EnterRoom(int entryRow)
        {
            // Обычно уже сгенерировано в ScanForUpcomingBossTile — здесь
            // не-op в подавляющем большинстве случаев, кроме подстраховки
            // из doc-комментария OnAdvanced.
            EnsureRoomGenerated(entryRow);
            ActiveRoom = new BossRoom(entryRow, entryRow + BossRoomGenerator.DefaultRoomLengthRows, WaveRowsPerSecond);
        }

        private void CollectTile(Tile tile)
        {
            switch (tile.BossRoomTile)
            {
                case BossRoomTileKind.Vein:
                    ActiveRoom.CollectVein(BossRoomGenerator.VeinBaseAmount);
                    break;
                case BossRoomTileKind.Resonance:
                    ActiveRoom.CollectResonance();
                    break;
                case BossRoomTileKind.Echo:
                    ActiveRoom.CollectEcho();
                    break;
            }
        }
    }
}
