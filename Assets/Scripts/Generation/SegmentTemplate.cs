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

        /// <param name="gateVaultPurchases">
        /// Размер тайника за Воротами этого шаблона, в покупках (задача
        /// «видимые рычаги, инвариант лавы, размер награды за Воротами»,
        /// владелец, 2026-09-04) — см. <see cref="GateVaultPurchases"/> и
        /// <see cref="GateVaultPricing"/>. Обязателен, если в шаблоне есть
        /// <see cref="SegmentTileType.GateVaultKeySource"/>, иначе должен
        /// быть null — см. <see cref="ValidateGateVault"/>.
        /// </param>
        public SegmentTemplate(string name, int difficultyTier, SegmentRewardTag rewardTag, SegmentTileType[,] tiles, double? gateVaultPurchases = null)
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
            GateVaultPurchases = gateVaultPurchases;

            ValidateNoTriggerOnLastRow();
            ValidateLeverGates();
            ValidateGateVault();
            ValidateTriggerTargetsDoNotConflict();
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

        /// <summary>
        /// Размер тайника за Воротами ЭТОГО шаблона, в покупках (владелец,
        /// 2026-09-04: «1 для короткого крюка, 1.5 для длинного» — не
        /// формула, конкретные значения по шаблону задаёт авторинг). Null,
        /// если в шаблоне нет <see cref="SegmentTileType.GateVaultKeySource"/>.
        /// Фактическая сумма Ключей — <see cref="GateVaultPricing.ComputeVaultKeys"/>.
        /// </summary>
        public double? GateVaultPurchases { get; }

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
            // Issue #193: "рычаг существует только рядом со своими Воротами
            // — отдельно стоящий рычаг вне связки бессмыслен" — открывать
            // нечего, независимо от того, виден рычаг всегда (владелец,
            // 2026-09-04) или был скрыт (более раннее решение того же
            // issue). Обратная сторона проверки выше.
            if (leverCount > 0 && gateCount == 0)
                throw new ArgumentException("Lever без единственного LeverGate в том же шаблоне — рычаг без ворот бессмыслен (issue #193).", nameof(_tiles));
        }

        // Не более одного тайника за Ворота на шаблон (задача «размер
        // награды за Воротами», владелец, 2026-09-04) — тот же принцип, что
        // у Lever выше: GateVaultPurchases одна на шаблон, нескольким
        // тайникам разных размеров было бы некуда деться. Обязателен, если
        // GateVaultKeySource есть; должен быть null, если его нет — молчаливо
        // проигнорированный параметр конструктора маскировал бы опечатку
        // авторинга (забыли поставить 'v' или, наоборот, лишний параметр).
        private void ValidateGateVault()
        {
            var vaultCount = 0;
            for (var r = 0; r < RowCount; r++)
                for (var c = 0; c < Width; c++)
                    if (_tiles[r, c] == SegmentTileType.GateVaultKeySource) vaultCount++;

            if (vaultCount > 1)
                throw new ArgumentException($"Больше одного GateVaultKeySource в шаблоне ({vaultCount}) — GateVaultPurchases одна на шаблон.", nameof(_tiles));
            if (vaultCount == 1 && !GateVaultPurchases.HasValue)
                throw new ArgumentException("GateVaultKeySource есть в шаблоне, но GateVaultPurchases не задан.", nameof(_tiles));
            if (vaultCount == 0 && GateVaultPurchases.HasValue)
                throw new ArgumentException("GateVaultPurchases задан, но GateVaultKeySource в шаблоне нет — параметр не был бы использован.", nameof(GateVaultPurchases));
        }

        // Найдено на реальном билде (2026-09-01, живой забег до Яруса 6):
        // необработанный InvalidOperationException — SegmentRowProvider.
        // ApplyTileType целится ExplosiveTrigger'ом ВСЕГДА в (Row+1, тот же
        // столбец) (см. её doc-комментарий) — эта клетка гарантированно
        // внутри ЭТОГО шаблона (ValidateNoTriggerOnLastRow выше), но не
        // проверялось, ЧЕМ она сама авторски размечена.
        //
        // Владелец, продолжение той же задачи (2026-09-01): исходная
        // формулировка стража на Tile ("плита не может нести две
        // взаимоисключающие роли") была ДЕФЕКТОМ ПОСТАНОВКИ, не шаблонов —
        // страж описывал ГЕНЕРАЦИЮ (два генератора не пишут в одну плиту
        // при постройке тоннеля) и РАНТАЙМ (взрыв уничтожает награду под
        // собой — намеренная механика шаблонов «выкуп»/«последний-рывок»)
        // одним и тем же правилом. Теперь разделено по фазам: рантайм-
        // переход (см. <c>Core.Tile.TransitionToLethalTrap</c>) НЕ
        // проверяется здесь вовсе — эта проверка ловит только то, что
        // ОСТАЛОСЬ настоящей ошибкой авторинга: цель триггера, которая сама
        // статично размечена как Blocked/Pit/Lava/Lever/LeverGate/Altar/
        // Boss (сущности, которые взрыв не должен и не может "уничтожить" —
        // не награда, а другая структурная роль). ManaSource/KeySource
        // сознательно ИСКЛЮЧЕНЫ из проверки — см. <see cref="IsExclusiveRole"/>.
        //
        // ТОЛЬКО ExplosiveTrigger — намеренно не TimedTrapArrowTrigger/
        // TimedTrapBladeTrigger: их цель активируется
        // <c>Movement.TimedTrapSystem</c> через <c>Tile.ArmTimedTrap</c>,
        // который вообще не завязан на эксклюзивные роли — этот класс крэша
        // для них физически невозможен.
        private void ValidateTriggerTargetsDoNotConflict()
        {
            for (var r = 0; r < RowCount; r++)
            for (var c = 0; c < Width; c++)
            {
                if (_tiles[r, c] != SegmentTileType.ExplosiveTrigger) continue;

                // r+1 гарантированно < RowCount — иначе ValidateNoTriggerOnLastRow
                // уже бросил бы исключение (вызывается раньше в конструкторе).
                var targetType = _tiles[r + 1, c];
                if (!IsExclusiveRole(targetType)) continue;

                throw new ArgumentException(
                    $"Триггер взрыва (row={r}, column={c}) целится в клетку (row={r + 1}, column={c}), которая сама авторски размечена как '{targetType}' — эта роль (в отличие от ManaSource/KeySource) не предназначена для уничтожения взрывом, похоже на ошибку авторинга, не на намеренную механику «награда сгорает».",
                    nameof(_tiles));
            }
        }

        // Роли, которые ExplosiveTrigger НЕ должен заставать на своей цели —
        // структурные состояния, не рассчитанные на переход в LethalTrap.
        // ManaSource/KeySource НАМЕРЕННО не входят (владелец, 2026-09-01):
        // "триггер должен уметь уничтожать ключ под собой" — см.
        // <c>Core.Tile.TransitionToLethalTrap</c>. Open и сами триггеры тоже
        // не входят — "быть триггером" не гейтится этим инвариантом.
        private static bool IsExclusiveRole(SegmentTileType type) => type switch
        {
            SegmentTileType.Blocked => true,
            SegmentTileType.Pit => true,
            SegmentTileType.Lava => true,
            SegmentTileType.Lever => true,
            SegmentTileType.LeverGate => true,
            SegmentTileType.Altar => true,
            SegmentTileType.Boss => true,
            _ => false,
        };
    }
}
