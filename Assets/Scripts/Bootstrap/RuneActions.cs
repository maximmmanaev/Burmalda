using System;
using Burmalda.Artifacts;
using Burmalda.Core;
using Burmalda.Movement;

namespace Burmalda.Bootstrap
{
    /// <summary>
    /// Руны славянских богов по тапу на иконку в HUD (PRD v10 §20.4): разовая способность. Морок и Ночь Чернобога —
    /// таймеры в <see cref="RelicEffectState"/>; Распутье раскрывает типы триггеров в ближайших рядах; Удар Перуна
    /// разбивает соседнюю стену. Если у Руны нет цели (рядом нет стены) — она не тратится.
    /// </summary>
    public sealed class RuneActions
    {
        private readonly RelicEffectState _effects;
        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly Action<int> _ensureCoveredThrough;

        public RuneActions(RelicEffectState effects, TunnelGrid grid, GridTraceTrail trail, Action<int> ensureCoveredThrough = null)
        {
            _effects = effects ?? throw new ArgumentNullException(nameof(effects));
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _ensureCoveredThrough = ensureCoveredThrough;
        }

        /// <summary>Активирует Руну. False, если её нет в билде, она уже использована или у неё нет цели.</summary>
        public bool Activate(string runeId)
        {
            if (!_effects.CanUseRune(runeId)) return false;

            switch (runeId)
            {
                case RelicEffectState.RasputeRune:
                    _effects.TryUseRune(runeId);
                    RevealTrapTypesAhead(RelicEffectState.RasputeRows);
                    return true;

                case RelicEffectState.PerunStrikeRune:
                    if (!HasAdjacentWall()) return false; // цели нет — Руна не тратится
                    _effects.TryUseRune(runeId);
                    BreakAdjacentWalls(RelicEffectState.PerunStrikeWalls);
                    return true;

                default: // Морок, Ночь Чернобога — таймеры
                    return _effects.TryUseRune(runeId);
            }
        }

        private void RevealTrapTypesAhead(int rows)
        {
            var from = _trail.CurrentPosition.Row;
            _ensureCoveredThrough?.Invoke(from + rows);
            for (var row = from + 1; row <= from + rows; row++)
                for (var column = 0; column < _grid.Width; column++)
                    if (_grid.TryGetTile(new GridCoordinate(row, column), out var tile) && tile.IsTrapTrigger)
                        tile.RevealTrapType();
        }

        // Соседние стены в порядке «вперёд, вперёд по диагонали, вбок»: вперёд ломаем в первую очередь.
        private System.Collections.Generic.IEnumerable<Tile> AdjacentWalls()
        {
            var p = _trail.CurrentPosition;
            foreach (var (dr, dc) in new[] { (1, 0), (1, -1), (1, 1), (0, -1), (0, 1) })
            {
                var target = new GridCoordinate(p.Row + dr, p.Column + dc);
                if (_grid.Contains(target) && _grid.TryGetTile(target, out var tile) && tile.IsBlocked) yield return tile;
            }
        }

        private bool HasAdjacentWall() { foreach (var _ in AdjacentWalls()) return true; return false; }

        private void BreakAdjacentWalls(int count)
        {
            foreach (var tile in AdjacentWalls())
            {
                if (count-- <= 0) break;
                tile.BreakWall();
            }
        }
    }
}
