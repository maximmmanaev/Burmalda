using System;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Материализует плиты сетки на несколько рядов впереди игрока заранее
    /// (issue #9: статичные препятствия должны быть «видны заранее», а не
    /// решаться в момент шага). Без этого <c>Burmalda.Core.TunnelObstacleGenerator</c>
    /// узнавал бы тип плиты только в момент фактического успешного хода на
    /// неё — <see cref="GridTraceTrail.TryAdvanceTo"/> материализует цель
    /// уже ПОСЛЕ того, как <see cref="GridTraceTrail.CanAdvanceTo"/>
    /// разрешает ход по непроинициализированной плите (см. комментарий там:
    /// сделано намеренно, чтобы проверка хода не материализовывала плиту как
    /// побочный эффект).
    ///
    /// Ряды раскрываются по всей ширине тоннеля (не только по столбцу
    /// игрока) — препятствие может оказаться в соседнем столбце.
    ///
    /// <b>УСТАРЕЛО (PRD v7 §21, issue #78)</b> — заменён
    /// <c>Burmalda.Generation.SegmentRowProvider</c> (раскрывает целыми
    /// сегментами, а не построчно). См. <c>TunnelObstacleController</c> для
    /// причины, по которой класс не удалён.
    /// </summary>
    public sealed class TunnelGridReveal : IDisposable
    {
        // Единственное строго необходимое для корректности значение — >=1
        // (сосед текущей позиции должен быть материализован раньше, чем
        // игрок сможет до него дотронуться). Запас с большим числом — вопрос
        // того, насколько заранее препятствие видно на экране, не
        // корректности; ориентир — TunnelCameraFollow.TrailingRowsBehindPlayer (5).
        public const int RowsAheadOfPlayer = 8;

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private int _revealedThroughRow = -1;
        private bool _disposed;

        public TunnelGridReveal(TunnelGrid grid, GridTraceTrail trail)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));

            RevealThrough(_trail.CurrentPosition.Row + RowsAheadOfPlayer);
            _trail.PositionChanged += OnPositionChanged;
        }

        /// <summary>Отписывается от трейла. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.PositionChanged -= OnPositionChanged;
            _disposed = true;
        }

        private void OnPositionChanged(GridCoordinate coordinate) =>
            RevealThrough(coordinate.Row + RowsAheadOfPlayer);

        private void RevealThrough(int row)
        {
            for (var r = _revealedThroughRow + 1; r <= row; r++)
                for (var c = 0; c < _grid.Width; c++)
                    _grid.GetOrCreateTile(new GridCoordinate(r, c));

            if (row > _revealedThroughRow) _revealedThroughRow = row;
        }
    }
}
