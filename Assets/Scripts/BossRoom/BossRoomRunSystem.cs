using System;
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

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly RunCurrencyAccumulator _mana;
        private readonly Action<string> _reportBossDefeat;
        private readonly BossRoomGenerator _generator;
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

        /// <summary>Тикает волну реальным временем — вызывать из Update() владеющего MonoBehaviour.</summary>
        public void Tick(float deltaSeconds)
        {
            ActiveRoom?.TickWave(deltaSeconds);
        }

        private void OnAdvanced(GridCoordinate coordinate)
        {
            if (!_grid.TryGetTile(coordinate, out var tile)) return;

            if (ActiveRoom == null)
            {
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
            var roomLength = BossRoomGenerator.DefaultRoomLengthRows;
            _generator.Generate(entryRow, roomLength);
            ActiveRoom = new BossRoom(entryRow, entryRow + roomLength, WaveRowsPerSecond);
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
