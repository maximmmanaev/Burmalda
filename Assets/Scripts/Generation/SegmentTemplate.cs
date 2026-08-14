using System;

namespace Burmalda.Generation
{
    /// <summary>
    /// Авторский шаблон сегмента трассы (PRD v7 §21, issue #78): "тоннель
    /// собирается не поплиточно, а из сегментов — авторских шаблонов на
    /// 5–8 рядов существующей сетки". Хранит фиксированную (не случайную)
    /// раскладку плит — сам случайный выбор, КАКОЙ шаблон применить дальше,
    /// делает <see cref="SegmentSelector"/>, не этот класс.
    /// </summary>
    public sealed class SegmentTemplate
    {
        public const int MinRowCount = 5;
        public const int MaxRowCount = 8;
        public const int MinDifficultyTier = 1;
        public const int MaxDifficultyTier = 5;

        private readonly SegmentTileType[,] _tiles;

        public SegmentTemplate(string name, int difficultyTier, SegmentRewardTag rewardTag, SegmentTileType[,] tiles)
        {
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            if (difficultyTier < MinDifficultyTier || difficultyTier > MaxDifficultyTier)
                throw new ArgumentOutOfRangeException(nameof(difficultyTier), difficultyTier,
                    $"Тег сложности сегмента должен быть в диапазоне [{MinDifficultyTier}, {MaxDifficultyTier}] (PRD v7 §21).");

            var rowCount = tiles.GetLength(0);
            if (rowCount < MinRowCount || rowCount > MaxRowCount)
                throw new ArgumentException(
                    $"Шаблон сегмента должен занимать [{MinRowCount}, {MaxRowCount}] рядов (PRD v7 §21), получено {rowCount}.",
                    nameof(tiles));

            Name = name;
            DifficultyTier = difficultyTier;
            RewardTag = rewardTag;
            RowCount = rowCount;
            Width = tiles.GetLength(1);
            _tiles = tiles;

            ValidateNoTriggerOnLastRow();
            ValidateLeverGates();
        }

        /// <summary>Имя шаблона (для авторинга/отладки — не показывается игроку напрямую).</summary>
        public string Name { get; }

        /// <summary>Тег сложности 1..5 (PRD v7 §21) — используется <see cref="SegmentSelector"/> для отбора по кривой сложности Яруса.</summary>
        public int DifficultyTier { get; }

        /// <summary>Тег награды (Мана/Ключи/Монеты/артефакт, PRD v7 §21).</summary>
        public SegmentRewardTag RewardTag { get; }

        /// <summary>Число рядов шаблона, 5..8.</summary>
        public int RowCount { get; }

        /// <summary>Ширина шаблона в плитах — должна совпадать с шириной тоннеля при применении (см. <see cref="SegmentRowProvider"/>).</summary>
        public int Width { get; }

        /// <summary>Тип плиты по локальным координатам внутри шаблона (0,0 — вход, RowCount-1 — выход).</summary>
        public SegmentTileType TileAt(int row, int column) => _tiles[row, column];

        // Триггер на последнем ряду шаблона указывал бы на плиту за его
        // пределами (следующий ряд — уже другой сегмент) — см.
        // SegmentRowProvider.ApplyTemplate: цель триггера всегда "тот же
        // столбец, следующий ряд", вычисляется в абсолютных координатах, но
        // должна физически принадлежать ЭТОМУ шаблону, иначе граница между
        // сегментами становится источником трудноотлаживаемых багов.
        private void ValidateNoTriggerOnLastRow()
        {
            var lastRow = RowCount - 1;
            for (var c = 0; c < Width; c++)
            {
                var type = _tiles[lastRow, c];
                if (type == SegmentTileType.ExplosiveTrigger || type == SegmentTileType.TimedTrapArrowTrigger || type == SegmentTileType.TimedTrapBladeTrigger)
                    throw new ArgumentException(
                        $"Триггер ловушки на последнем ряду шаблона (row={lastRow}, column={c}) — цель вышла бы за пределы сегмента.",
                        nameof(_tiles));
            }
        }

        // Ровно один Lever на шаблон (issue #51): "открывает короткий
        // боковой проход" — один рычаг, один локальный проход в пределах
        // этого же шаблона. LeverGate без Lever или больше одного Lever —
        // авторская ошибка, а не runtime-случай.
        private void ValidateLeverGates()
        {
            var leverCount = 0;
            var gateCount = 0;
            for (var r = 0; r < RowCount; r++)
                for (var c = 0; c < Width; c++)
                {
                    if (_tiles[r, c] == SegmentTileType.Lever) leverCount++;
                    else if (_tiles[r, c] == SegmentTileType.LeverGate) gateCount++;
                }

            if (leverCount > 1)
                throw new ArgumentException($"Больше одного Lever в шаблоне ({leverCount}) — issue #51 предполагает ровно один рычаг на шаблон.", nameof(_tiles));
            if (gateCount > 0 && leverCount == 0)
                throw new ArgumentException("LeverGate без единственного Lever в том же шаблоне.", nameof(_tiles));
        }
    }
}
