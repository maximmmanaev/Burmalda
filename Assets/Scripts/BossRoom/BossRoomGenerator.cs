using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.BossRoom
{
    /// <summary>
    /// Материализует содержимое Комнаты Босса на реальной <see cref="TunnelGrid"/>
    /// при входе (вертикальный срез, задача «Комната Босса», владелец,
    /// 2026-09-02: в скоупе только Жила/Резонанс/Эхо — PRD v9 §8.2, Разлом
    /// не генерируется). По паттерну <see cref="TunnelObstacleGenerator"/> —
    /// <see cref="Func{TResult}"/> для случайности вместо
    /// <c>UnityEngine.Random</c>, чтобы оставаться тестируемым без сцены.
    ///
    /// <b>Заявляет ряды через <see cref="TunnelGrid.ClaimRow"/> ДО того, как
    /// раскладывает содержимое</b> — тот же приём, что
    /// <c>Generation.SegmentRowProvider</c>, не даёт
    /// <see cref="TunnelObstacleGenerator"/> засеять те же ряды собственным
    /// броском.
    ///
    /// <b>Известное ограничение вертикального среза (см. отчёт задачи):</b>
    /// вход в Комнату определяется по <see cref="Tile.IsBoss"/> уже ПОСЛЕ
    /// того, как игрок реально ступил на эту плиту — но
    /// <c>Movement.TunnelGridReveal</c> материализует ряды заранее (окно в
    /// несколько рядов вперёд), и эти уже материализованные ряды могли
    /// получить обычный тоннельный контент от другого генератора раньше,
    /// чем этот класс успел их заявить. Такие клетки — <see cref="HasForeignRole"/> —
    /// защитно пропускаются, а не перезаписываются: несколько первых рядов
    /// Комнаты могут оказаться обычным полом без содержимого Комнаты,
    /// вместо падения/порчи чужой роли. Комната намеренно длинная
    /// (см. <see cref="DefaultRoomLengthRows"/>), чтобы это окно оставалось
    /// маленькой долей Комнаты.
    /// </summary>
    public sealed class BossRoomGenerator
    {
        // Черновые значения — предмет баланса (Спринт 18). PRD v9 §8.2 даёт
        // диапазоны для Резонанса/Эха ("6-10"/"1-3 на комнату") и словесную
        // частоту для Жилы ("часто") без числа — то же положение, что у
        // Core.TunnelObstacleGenerator.*Share. Mutable static — тот же приём:
        // живой рычаг для будущей debug-панели, не const.
        public static int ResonanceCountMin = 6;
        public static int ResonanceCountMax = 10;
        public static int EchoCountMin = 1;
        public static int EchoCountMax = 3;

        /// <summary>Доля клеток Комнаты вне Резонанса/Эха, которые становятся Жилой (PRD v9 §8.2: "часто", без точного числа).</summary>
        public static float VeinShareOfRemaining = 0.6f;

        /// <summary>
        /// Длина Комнаты в рядах — намеренно щедрая (см. класс-докстрингу про
        /// окно материализации reveal-ahead): чем длиннее Комната, тем
        /// меньше доля её первых рядов может оказаться потеряна для окна.
        /// </summary>
        public static int DefaultRoomLengthRows = 24;

        /// <summary>Доход с одной Жилы до применения множителя Комнаты (PRD v9 §8.2 не называет число).</summary>
        public static int VeinBaseAmount = 30;

        private readonly TunnelGrid _grid;
        private readonly Func<float> _random01;

        public BossRoomGenerator(TunnelGrid grid, Func<float> random01)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _random01 = random01 ?? throw new ArgumentNullException(nameof(random01));
        }

        /// <summary>
        /// Заявляет ряды <paramref name="entryRow"/>+1..<paramref name="entryRow"/>+<paramref name="roomLengthRows"/>
        /// и раскладывает по ним содержимое Комнаты. Идемпотентно только в
        /// пределах одного вызова — вызывать один раз на вход в Комнату.
        /// </summary>
        public void Generate(int entryRow, int roomLengthRows)
        {
            if (roomLengthRows <= 0)
                throw new ArgumentOutOfRangeException(nameof(roomLengthRows), roomLengthRows, "Длина Комнаты должна быть положительной.");

            var lastRow = entryRow + roomLengthRows;
            for (var row = entryRow + 1; row <= lastRow; row++)
                _grid.ClaimRow(row);

            var candidates = new List<GridCoordinate>();
            for (var row = entryRow + 1; row <= lastRow; row++)
            {
                for (var column = 0; column < _grid.Width; column++)
                {
                    var coordinate = new GridCoordinate(row, column);
                    var tile = _grid.GetOrCreateTile(coordinate);
                    if (HasForeignRole(tile)) continue;
                    candidates.Add(coordinate);
                }
            }

            Shuffle(candidates);

            var resonanceCount = Math.Min(RollCount(ResonanceCountMin, ResonanceCountMax), candidates.Count);
            var echoCount = Math.Min(RollCount(EchoCountMin, EchoCountMax), candidates.Count - resonanceCount);

            var index = 0;
            for (; index < resonanceCount; index++)
                _grid.GetOrCreateTile(candidates[index]).MarkBossRoomTile(BossRoomTileKind.Resonance);
            for (; index < resonanceCount + echoCount; index++)
                _grid.GetOrCreateTile(candidates[index]).MarkBossRoomTile(BossRoomTileKind.Echo);
            for (; index < candidates.Count; index++)
            {
                if (_random01() < VeinShareOfRemaining)
                    _grid.GetOrCreateTile(candidates[index]).MarkBossRoomTile(BossRoomTileKind.Vein);
            }
        }

        // Защитно: клетка уже несёт роль от другого генератора (окно
        // материализации reveal-ahead, см. класс-докстрингу) — не
        // перезаписываем, оставляем как есть.
        private static bool HasForeignRole(Tile tile) =>
            tile.IsBlocked || tile.LethalTrap.HasValue || tile.IsManaSource || tile.IsKeySource ||
            tile.IsAltar || tile.IsLever || tile.IsGated || tile.IsBoss || tile.BossRoomTile.HasValue;

        private int RollCount(int min, int max)
        {
            if (max <= min) return min;
            var roll = min + (int)(_random01() * (max - min + 1));
            return Math.Min(roll, max);
        }

        private void Shuffle(List<GridCoordinate> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = (int)(_random01() * (i + 1));
                if (j > i) j = i; // защитно от random01(), вернувшего ровно 1.0
                (list[i], list[j]) = (list[i], list[j]);
            }
        }
    }
}
