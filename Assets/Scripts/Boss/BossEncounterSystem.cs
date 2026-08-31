using System;
using Burmalda.Artifacts;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Movement;
using Burmalda.Progression;

namespace Burmalda.Boss
{
    /// <summary>
    /// Встреча с Боссом при достижении его точки (PRD раздел 8, issue #22):
    /// "Встреча происходит автоматически при достижении точки Босса".
    /// Реагирует на <see cref="GridTraceTrail.Advanced"/> — по-настоящему
    /// новая плита (#61), как Алтарь/Рычаг. Победа: выдаётся Реликвия,
    /// первая победа за прохождение разблокирует все Амулеты/Талисманы
    /// каталога в
    /// <see cref="ArtifactPool"/> (PRD раздел 6), Ярус Глубины продвигается
    /// (<see cref="RunDepthTier.RecordBossVictory"/>, PRD v7 §22, issue
    /// #83) — победа над Боссом ГРАНИЦА Яруса, требуемая энергия следующего
    /// Босса растёт от ТЕКУЩЕГО (до этой победы) Яруса. Поражение:
    /// детерминированная смерть — d20 НЕ применяется (PRD v7 §8.3, issue #82).
    ///
    /// Смерть сообщается через <paramref name="reportBossDefeat"/> — колбэк,
    /// а не прямая ссылка на <c>RunLifecycle.RunState</c>: RunState
    /// пересобирается на каждый забег своим отдельным Controller'ом, и
    /// захват конкретного инстанса в конструкторе рисковал бы держать
    /// ссылку на уже устаревший (Disposed) RunState, если оба Controller'а
    /// пересобираются по одному и тому же событию в непредсказуемом порядке.
    /// Колбэк вызывающая сторона может привязать к "текущему RunState" так,
    /// чтобы он читался заново при каждом вызове (см. <c>BossController</c>).
    ///
    /// Выбор "вернуться в лагерь или идти глубже" после победы — вне этой
    /// системы, зависит от Лагеря (Спринт 8); эта система только разрешает
    /// встречу и публикует результат.
    ///
    /// <b>Больше не начисляет Монеты</b> (v9, задача по экономике "Мана как
    /// доход забега") — Перелив энергии в Монеты (PRD v7 §8.2) удалён вместе
    /// с <c>Boss.OverflowToCoinsRate</c>, см. докстроку <see cref="Boss"/>.
    /// </summary>
    public sealed class BossEncounterSystem : IDisposable
    {
        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly RunCurrencyAccumulator _mana;
        private readonly ArtifactPool _pool;
        private readonly FirstBossVictoryTracker _firstVictoryTracker;
        private readonly RunDepthTier _depthTier;
        private readonly Action<string> _reportBossDefeat;
        private readonly Func<int, int> _requiredEnergyForTier;
        private bool _disposed;

        /// <param name="requiredEnergyForTier">
        /// Требуемая энергия для Босса на границе данного Яруса (0 — первый
        /// Босс). Кривая по Ярусам — черновая (см. <c>BossController</c>),
        /// предмет баланса (Спринт 10).
        /// </param>
        /// <param name="reportBossDefeat">Вызывается с причиной при поражении — см. докстроку класса про причину колбэка вместо прямой ссылки на RunState.</param>
        public BossEncounterSystem(TunnelGrid grid, GridTraceTrail trail, RunCurrencyAccumulator mana,
            ArtifactPool pool, FirstBossVictoryTracker firstVictoryTracker, RunDepthTier depthTier, Action<string> reportBossDefeat, Func<int, int> requiredEnergyForTier)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _mana = mana ?? throw new ArgumentNullException(nameof(mana));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _firstVictoryTracker = firstVictoryTracker ?? throw new ArgumentNullException(nameof(firstVictoryTracker));
            _depthTier = depthTier ?? throw new ArgumentNullException(nameof(depthTier));
            _reportBossDefeat = reportBossDefeat ?? throw new ArgumentNullException(nameof(reportBossDefeat));
            _requiredEnergyForTier = requiredEnergyForTier ?? throw new ArgumentNullException(nameof(requiredEnergyForTier));

            _trail.Advanced += OnAdvanced;
        }

        /// <summary>Срабатывает после разрешения встречи — Реликвия не null только при победе.</summary>
        public event Action<BossEncounterOutcome, Relic> EncounterResolved;

        /// <summary>Отписывается от трейла. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.Advanced -= OnAdvanced;
            _disposed = true;
        }

        private void OnAdvanced(GridCoordinate coordinate)
        {
            if (!_grid.TryGetTile(coordinate, out var tile)) return;
            if (!tile.IsBoss) return;

            var boss = new Boss(_requiredEnergyForTier(_depthTier.CurrentTier));
            var outcome = boss.Resolve(_mana.Total);
            Relic relic = null;

            if (outcome.IsVictory)
            {
                relic = new Relic($"relic-boss-tier-{_depthTier.CurrentTier}-row-{coordinate.Row}", "Реликвия Босса");
                _depthTier.RecordBossVictory();

                if (!_firstVictoryTracker.HasWonBefore)
                {
                    _firstVictoryTracker.RecordVictory();
                    UnlockAmuletsAndTalismans();
                }
            }
            else
            {
                _reportBossDefeat($"Не хватило энергии до Босса ({outcome.AccumulatedMana}/{outcome.RequiredEnergy})");
            }

            EncounterResolved?.Invoke(outcome, relic);
        }

        private void UnlockAmuletsAndTalismans()
        {
            foreach (var amulet in ArtifactCatalog.Amulets) _pool.Unlock(amulet.Id);
            foreach (var talisman in ArtifactCatalog.Talismans) _pool.Unlock(talisman.Id);
        }
    }
}
