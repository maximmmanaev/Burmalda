using System;
using Burmalda.Core;
using Burmalda.Movement;

namespace Burmalda.Currencies
{
    /// <summary>
    /// Серия чистого прохода (PRD v10 §12.2): плиты подряд без наступания на триггер ловушки. С порога путь подсвечивается,
    /// подсветка растёт, а серия даёт бонус Маны. Наступание на триггер обрывает серию. Числа — ЧЕРНОВЫЕ [CONFIG].
    /// Заряд Идола серия пока не даёт (не в этом спринте). Бонус умножает Лешачья тропа (×2) и Мана-множитель Реликвий.
    /// </summary>
    public sealed class CleanPassStreakSystem : IDisposable
    {
        /// <summary>С какой по счёту плиты серии начинается подсветка пути и бонус Маны [CONFIG, черновик].</summary>
        public static int GlowThreshold = 5;

        /// <summary>Бонус Маны за плиту серии на пороге; растёт на <see cref="BonusGrowthPerTile"/> за каждую следующую плиту [CONFIG, черновик].</summary>
        public static int BaseBonusMana = 5;

        public static int BonusGrowthPerTile = 1;

        /// <summary>Потолок бонуса за плиту (до множителей Реликвий) [CONFIG, черновик].</summary>
        public static int MaxBonusMana = 30;

        /// <summary>Плит серии, при которых подсветка максимальна [CONFIG, черновик].</summary>
        public static int FullGlowTiles = 25;

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly RunCurrencyAccumulator _runMana;
        private bool _disposed;

        public CleanPassStreakSystem(TunnelGrid grid, GridTraceTrail trail, RunCurrencyAccumulator runMana)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _runMana = runMana ?? throw new ArgumentNullException(nameof(runMana));
            _trail.Advanced += OnAdvanced;
        }

        /// <summary>Текущая серия — плит подряд без триггера.</summary>
        public int Streak { get; private set; }

        /// <summary>Подсветка пути 0..1: ноль до порога, дальше растёт до максимума.</summary>
        public float Glow => Streak < GlowThreshold ? 0f : Math.Min(1f, (Streak - GlowThreshold + 1f) / Math.Max(1, FullGlowTiles - GlowThreshold + 1));

        /// <summary>Срабатывает при изменении серии (в том числе при обрыве, значение 0).</summary>
        public event Action<int> Changed;

        /// <summary>Бонус Маны за плиту при серии <paramref name="streak"/> — до множителей Реликвий.</summary>
        public static int BonusFor(int streak) =>
            streak < GlowThreshold ? 0 : Math.Min(MaxBonusMana, BaseBonusMana + BonusGrowthPerTile * (streak - GlowThreshold));

        public void Dispose()
        {
            if (_disposed) return;
            _trail.Advanced -= OnAdvanced;
            _disposed = true;
        }

        private void OnAdvanced(GridCoordinate coordinate)
        {
            if (_grid.TryGetTile(coordinate, out var tile) && tile.IsTrapTrigger)
            {
                if (Streak == 0) return;
                Streak = 0; // наступание на триггер обрывает серию
                Changed?.Invoke(0);
                return;
            }

            Streak++;
            var bonus = BonusFor(Streak);
            if (bonus > 0)
            {
                var effects = RunEffects.Current;
                _runMana.Add((int)Math.Round(bonus * effects.StreakBonusMultiplier * effects.ManaMultiplier));
            }

            Changed?.Invoke(Streak);
        }
    }
}
