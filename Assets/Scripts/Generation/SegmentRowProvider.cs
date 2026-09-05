using System;
using System.Collections.Generic;
using Burmalda.Core;
using Burmalda.Movement;

namespace Burmalda.Generation
{
    /// <summary>
    /// Применяет выбранные <see cref="SegmentSelector"/> шаблоны к
    /// <see cref="TunnelGrid"/> целыми сегментами вместо поплиточного броска
    /// (PRD v7 §21, issue #78) — заменяет связку
    /// <c>Core.TunnelObstacleGenerator</c> + <c>Movement.TunnelGridReveal</c>.
    /// Материализует плиты на <see cref="RowsAheadOfPlayer"/> рядов вперёд
    /// игрока (тот же запас и тот же смысл, что у <c>TunnelGridReveal</c> —
    /// препятствия должны быть видны заранее, PRD 4.2), но целыми
    /// сегментами, а не построчно: если запрошенный ряд оказывается внутри
    /// уже частично покрытого сегмента, покрытие продвигается до конца
    /// текущего сегмента, а не останавливается посередине.
    ///
    /// Стартовый ряд трейла (обычно 0) уже безопасен по построению
    /// (<see cref="GridTraceTrail"/> материализует его как обычную плиту) —
    /// первый применённый здесь шаблон всегда начинается со следующего ряда,
    /// см. <see cref="_appliedThroughRow"/>.
    ///
    /// <b>Сосуществование с Core.TunnelObstacleGenerator (переходное
    /// состояние, docs/wiki/roadmap.md)</b>: каждый применённый ряд шаблона
    /// заявляется через <see cref="TunnelGrid.ClaimRow"/> ДО материализации
    /// его плит (см. <see cref="ApplyTemplate"/>) — это единственный
    /// источник claim в проекте, и именно порядок "заявить, потом
    /// материализовать" делает двойную запись в одну плиту невозможной по
    /// конструкции, а не по факту, что оба генератора "договорились" не
    /// мешать друг другу.
    /// </summary>
    public sealed class SegmentRowProvider : IDisposable
    {
        // Тот же смысл и тот же запас, что TunnelGridReveal.RowsAheadOfPlayer.
        public const int RowsAheadOfPlayer = 8;

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly SegmentSelector _selector;
        private readonly Func<int, int> _maxDifficultyForRow;
        private readonly int _rowsPerTier;
        private int _appliedThroughRow;
        private int _nextCapstoneRow;
        private bool _disposed;

        /// <param name="maxDifficultyForRow">
        /// Верхняя граница сложности сегмента для ряда, с которого он
        /// начинается — прокси "кривой сложности текущего Яруса" (PRD v7
        /// §21) до тех пор, пока в проекте нет Ярусов Глубины (Спринт 8);
        /// внедряется явно, чтобы не создавать зависимость на ещё не
        /// реализованную систему.
        /// </param>
        /// <param name="rowsPerTier">
        /// Правка по итогам ручной проверки владельца (2026-09-04, «Алтари и
        /// вход в Комнату — убрать из случайного пула»): период, с которым
        /// детерминированно ставится связка Алтарь→Алтарь→вход в Комнату
        /// Босса (см. <see cref="EnsureCoveredThrough"/>) — тот же прокси
        /// "границы Яруса", что уже использует <paramref name="maxDifficultyForRow"/>
        /// (на практике оба берут одно значение,
        /// <c>SegmentGenerationController.RowsPerDifficultyStep</c>, но
        /// провайдер не завязан на конкретного вызывающего).
        /// </param>
        public SegmentRowProvider(TunnelGrid grid, GridTraceTrail trail, SegmentSelector selector, Func<int, int> maxDifficultyForRow, int rowsPerTier)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _maxDifficultyForRow = maxDifficultyForRow ?? throw new ArgumentNullException(nameof(maxDifficultyForRow));
            if (rowsPerTier <= 0)
                throw new ArgumentOutOfRangeException(nameof(rowsPerTier), rowsPerTier, "Период границы Яруса должен быть положительным.");
            _rowsPerTier = rowsPerTier;

            _appliedThroughRow = trail.CurrentPosition.Row;
            _nextCapstoneRow = _appliedThroughRow + _rowsPerTier;
            EnsureCoveredThrough(_appliedThroughRow + RowsAheadOfPlayer);
            _trail.PositionChanged += OnPositionChanged;
        }

        /// <summary>Отписывается от трейла. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.PositionChanged -= OnPositionChanged;
            _disposed = true;
        }

        private void OnPositionChanged(GridCoordinate coordinate) => EnsureCoveredThrough(coordinate.Row + RowsAheadOfPlayer);

        /// <summary>
        /// Обеспечивает, что все ряды до <paramref name="row"/> включительно
        /// покрыты применённым сегментом — расширяет вперёд целыми
        /// сегментами (см. класс-докстроку).
        ///
        /// <b>Правка по итогам ручной проверки владельца (2026-09-04):</b> по
        /// достижении границы Яруса (<see cref="_nextCapstoneRow"/>) вместо
        /// случайного выбора применяется ФИКСИРОВАННАЯ связка
        /// <see cref="SegmentTemplateCatalog.AltarTemplate"/> ×2 →
        /// <see cref="SegmentTemplateCatalog.BossTemplate"/> — PRD v9 требует
        /// ровно два Алтаря перед каждой Комнатой Босса детерминированным
        /// потоком, не лотереей <see cref="SegmentSelector"/> (тот же
        /// селектор по-прежнему отвечает за наполнение МЕЖДУ границами).
        /// </summary>
        public void EnsureCoveredThrough(int row)
        {
            while (_appliedThroughRow < row)
            {
                var baseRow = _appliedThroughRow + 1;

                if (baseRow >= _nextCapstoneRow)
                {
                    ApplyTemplate(SegmentTemplateCatalog.AltarTemplate, baseRow);
                    _appliedThroughRow += SegmentTemplateCatalog.AltarTemplate.RowCount;
                    baseRow = _appliedThroughRow + 1;

                    ApplyTemplate(SegmentTemplateCatalog.AltarTemplate, baseRow);
                    _appliedThroughRow += SegmentTemplateCatalog.AltarTemplate.RowCount;
                    baseRow = _appliedThroughRow + 1;

                    ApplyTemplate(SegmentTemplateCatalog.BossTemplate, baseRow);
                    _appliedThroughRow += SegmentTemplateCatalog.BossTemplate.RowCount;

                    _nextCapstoneRow += _rowsPerTier;
                    continue;
                }

                var template = _selector.SelectNext(_maxDifficultyForRow(baseRow));
                ApplyTemplate(template, baseRow);
                _appliedThroughRow += template.RowCount;
            }
        }

        private void ApplyTemplate(SegmentTemplate template, int baseRow)
        {
            if (template.Width != _grid.Width)
                throw new InvalidOperationException(
                    $"Ширина шаблона '{template.Name}' ({template.Width}) не совпадает с шириной тоннеля ({_grid.Width}).");

            // Заявляем ВСЕ ряды шаблона ДО первого GetOrCreateTile ниже —
            // TunnelGrid.TileMaterialized стреляет синхронно внутри
            // GetOrCreateTile, и если Core.TunnelObstacleGenerator тоже
            // подписан (переходное состояние сосуществования двух
            // генераторов, docs/wiki/roadmap.md), он должен УЖЕ увидеть ряд
            // заявленным в момент своего обработчика — иначе успел бы
            // откликнуться первым и записать в плиту случайный тип раньше,
            // чем этот метод применит настоящий тип шаблона.
            for (var localRow = 0; localRow < template.RowCount; localRow++)
                _grid.ClaimRow(baseRow + localRow);

            // Первый проход — собрать координату рычага и все координаты его
            // ворот (в абсолютных координатах сетки): Tile.MarkLever нужен
            // весь список целей сразу, а расположение Lever/LeverGate в
            // шаблоне заранее не упорядочено относительно друг друга.
            GridCoordinate? leverCoordinate = null;
            var gateTargets = new List<GridCoordinate>();
            for (var localRow = 0; localRow < template.RowCount; localRow++)
            for (var column = 0; column < template.Width; column++)
            {
                var type = template.TileAt(localRow, column);
                if (type == SegmentTileType.Lever) leverCoordinate = new GridCoordinate(baseRow + localRow, column);
                else if (type == SegmentTileType.LeverGate) gateTargets.Add(new GridCoordinate(baseRow + localRow, column));
            }

            for (var localRow = 0; localRow < template.RowCount; localRow++)
            for (var column = 0; column < template.Width; column++)
            {
                var coordinate = new GridCoordinate(baseRow + localRow, column);
                var tile = _grid.GetOrCreateTile(coordinate);
                ApplyTileType(tile, coordinate, template.TileAt(localRow, column), gateTargets, leverCoordinate, template.GateVaultPurchases);
            }
        }

        private static void ApplyTileType(Tile tile, GridCoordinate coordinate, SegmentTileType type, List<GridCoordinate> leverGateTargets, GridCoordinate? leverCoordinate, double? gateVaultPurchases)
        {
            switch (type)
            {
                case SegmentTileType.Blocked:
                    tile.MarkBlocked();
                    break;
                case SegmentTileType.Lava:
                    tile.MarkLethalTrap(LethalTrapType.Lava);
                    break;
                case SegmentTileType.Lever:
                    tile.MarkLever(leverGateTargets);
                    break;
                case SegmentTileType.LeverGate:
                    // Issue #193: координата рычага — для подсказки
                    // направления на закрытых воротах (TunnelDebugVisual).
                    // SegmentTemplate.ValidateLeverGates уже гарантирует,
                    // что LeverGate не бывает без Lever в том же шаблоне —
                    // leverCoordinate здесь не null.
                    tile.MarkGated(leverCoordinate);
                    break;
                case SegmentTileType.ManaSource:
                    tile.MarkManaSource();
                    break;
                case SegmentTileType.KeySource:
                    tile.MarkKeySource();
                    break;
                case SegmentTileType.GateVaultKeySource:
                    // SegmentTemplate.ValidateGateVault уже гарантирует, что
                    // gateVaultPurchases не null, когда в шаблоне есть эта
                    // роль — см. её doc-комментарий.
                    tile.MarkKeySource(GateVaultPricing.ComputeVaultKeys(gateVaultPurchases.Value));
                    break;
                case SegmentTileType.Altar:
                    tile.MarkAltar();
                    break;
                case SegmentTileType.Boss:
                    tile.MarkBoss();
                    break;
                case SegmentTileType.ArrowWaveTrigger:
                    // Дефолт "ряд самого триггера/слева-направо" — см.
                    // doc-комментарий SegmentTileType.ArrowWaveTrigger.
                    tile.MarkArrowWaveTrigger(coordinate.Row, RowWaveDirection.LeftToRight);
                    break;
                case SegmentTileType.BombTrigger:
                    tile.MarkBombTrigger();
                    break;
                case SegmentTileType.BladeTactTrigger:
                    tile.MarkBladeTactTrigger(coordinate.Row);
                    break;
                case SegmentTileType.FallingRockTrigger:
                    tile.MarkFallingRockTrigger();
                    break;
                case SegmentTileType.LavaWaveTrigger:
                    tile.MarkLavaTrigger();
                    break;
                case SegmentTileType.Open:
                default:
                    break;
            }
        }
    }
}
