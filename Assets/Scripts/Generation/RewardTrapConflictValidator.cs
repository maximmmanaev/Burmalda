using System.Collections.Generic;
using Burmalda.Movement;

namespace Burmalda.Generation
{
    /// <summary>
    /// Задача «награда никогда не лежит на ловушке» (владелец, Спринт «Стены
    /// вместо ловушек», задача 2): "Плита-источник Маны или Ключей не может
    /// одновременно нести ловушку или её триггер. Награда — приманка к риску
    /// маршрута, а не капкан: игрок должен рисковать длиной пути и распадом,
    /// а не получать смерть за то, что подобрал то, ради чего свернул."
    ///
    /// <b>Почему это не то же самое, что <see cref="Tile.GuardAgainstConflictingRole"/>
    /// (Core, «двойные флаги на плитах»).</b> Тот страж ловит попытку
    /// пометить ОДНУ И ТУ ЖЕ плиту двумя взаимоисключающими ролями — и
    /// технически он уже не пропустит явный конфликт (расширен этой же
    /// задачей на пять триггеров ловушек). Но реальный источник бага
    /// «триггер уничтожает награду под собой» (шаблоны «выкуп»/«последний-
    /// рывок»/«развилка-цены», подтверждено владельцем как намеренная
    /// механика 2026-09-01, отменено этой задачей) — НЕ про одну плиту:
    /// триггер и награда — РАЗНЫЕ плиты шаблона, а связь между ними
    /// возникает только В РАНТАЙМЕ, когда область поражения триггера
    /// (<c>Movement.BombTrapSystem.ComputeBlastArea</c>, весь ряд
    /// <c>ArrowWaveTrapSystem</c>/<c>BladeTactTrapSystem</c>, ряды назад
    /// <c>LavaWaveTrapSystem</c>) докатывается до соседней плиты и вызывает
    /// <see cref="Tile.TransitionToLethalTrap"/> — метод, который НАМЕРЕННО
    /// пишет поверх любой прежней роли, включая награду (см. его
    /// doc-комментарий). Этот класс проверяет тот же исход СТАТИЧЕСКИ, на
    /// этапе авторинга — повторяет области поражения каждого триггера один
    /// в один с рантайм-системами, не дублируя их код (площадь Бомбы читает
    /// <see cref="Movement.BombTrapSystem.RadiusTiles"/> напрямую, глубина
    /// волны Лавы — <see cref="Movement.LavaWaveTrapSystem.MaxRows"/> —
    /// оба mutable static, если владелец раздвинет их на дебаг-панели,
    /// гарантия этого класса относится к значениям НА МОМЕНТ ПРОВЕРКИ, не
    /// навсегда — приемлемо, это debug-стресс-параметры, не отгружаемый
    /// баланс).
    ///
    /// <see cref="FallingRockTrigger"/> НЕ может создать такой конфликт в
    /// принципе — камень падает только на саму плиту-триггер
    /// (<c>Movement.FallingRockTrapSystem</c>), а плита не может
    /// одновременно быть триггером и наградой (<see cref="SegmentTileType"/>
    /// — одно значение на клетку) — см. <see cref="AffectedCells"/>.
    /// </summary>
    public static class RewardTrapConflictValidator
    {
        /// <summary>Один найденный конфликт — награда, оказавшаяся в области поражения чужого триггера.</summary>
        public readonly struct Conflict
        {
            public Conflict(int rewardRow, int rewardColumn, SegmentTileType rewardType, int triggerRow, int triggerColumn, SegmentTileType triggerType)
            {
                RewardRow = rewardRow;
                RewardColumn = rewardColumn;
                RewardType = rewardType;
                TriggerRow = triggerRow;
                TriggerColumn = triggerColumn;
                TriggerType = triggerType;
            }

            public int RewardRow { get; }
            public int RewardColumn { get; }
            public SegmentTileType RewardType { get; }
            public int TriggerRow { get; }
            public int TriggerColumn { get; }
            public SegmentTileType TriggerType { get; }

            public override string ToString() =>
                $"награда {RewardType} ({RewardRow},{RewardColumn}) в области поражения {TriggerType} ({TriggerRow},{TriggerColumn})";
        }

        /// <summary>Все конфликты «награда в области поражения триггера» в шаблоне, как он есть — для прогона по каталогу.</summary>
        public static IReadOnlyList<Conflict> FindConflicts(SegmentTemplate template)
        {
            var conflicts = new List<Conflict>();
            for (var r = 0; r < template.RowCount; r++)
            for (var c = 0; c < template.Width; c++)
            {
                var triggerType = template.TileAt(r, c);
                if (!IsTriggerType(triggerType)) continue;

                foreach (var (rr, rc) in AffectedCells(template, r, c, triggerType))
                {
                    var rewardType = template.TileAt(rr, rc);
                    if (IsRewardType(rewardType))
                        conflicts.Add(new Conflict(rr, rc, rewardType, r, c, triggerType));
                }
            }
            return conflicts;
        }

        /// <summary>
        /// Поставил бы триггер типа <paramref name="candidateTriggerType"/>
        /// в (<paramref name="row"/>, <paramref name="column"/>) под угрозу
        /// хотя бы одну СУЩЕСТВУЮЩУЮ награду <paramref name="template"/>?
        /// Используется <see cref="SegmentRowProvider"/> перед тем, как
        /// <c>ExtraTrapDensity</c> закрепит случайный выбор на живой плите —
        /// кандидатная клетка всегда <see cref="SegmentTileType.Open"/> в
        /// статичном шаблоне (иначе `ExtraTrapDensity` её не тронул бы, см.
        /// <see cref="SegmentRowProvider.ApplyTileType"/>), поэтому
        /// достаточно смотреть на статичную раскладку — плиты, которые сам
        /// же `ExtraTrapDensity` мог бы обработать раньше в этом же проходе,
        /// никогда не награда (см. doc-комментарий класса).
        /// </summary>
        public static bool WouldEndangerAnyReward(SegmentTemplate template, int row, int column, SegmentTileType candidateTriggerType)
        {
            foreach (var (rr, rc) in AffectedCells(template, row, column, candidateTriggerType))
                if (IsRewardType(template.TileAt(rr, rc)))
                    return true;
            return false;
        }

        /// <summary>
        /// Клетки шаблона, которые триггер типа <paramref name="triggerType"/>
        /// в (<paramref name="row"/>, <paramref name="column"/>) рано или
        /// поздно переводит в <see cref="Tile.TransitionToLethalTrap"/> —
        /// повторяет области поражения рантайм-систем Movement один в один
        /// (см. doc-комментарий класса), обрезано по границам ШАБЛОНА (не
        /// сетки тоннеля — при проверке одного шаблона soседних сегментов
        /// ещё не существует, тот же принцип, что уже принят
        /// <see cref="SegmentReachabilityValidator"/>).
        /// </summary>
        private static IEnumerable<(int Row, int Column)> AffectedCells(SegmentTemplate template, int row, int column, SegmentTileType triggerType)
        {
            switch (triggerType)
            {
                case SegmentTileType.ArrowWaveTrigger:
                case SegmentTileType.BladeTactTrigger:
                    // SegmentRowProvider.ApplyTileType: цель по умолчанию —
                    // ряд самого триггера. ArrowWaveTrapSystem продвигается
                    // столбец за столбцом от края до края — весь ряд рано
                    // или поздно опасен. BladeTactTrapSystem — симметричные
                    // кольца от краёв к центру (ComputeRingColumns) — тоже
                    // покрывают весь ряд целиком, независимо от ширины.
                    for (var c = 0; c < template.Width; c++)
                        yield return (row, c);
                    break;

                case SegmentTileType.BombTrigger:
                    // BombTrapSystem.ComputeBlastArea: квадрат радиуса
                    // RadiusTiles вокруг триггера (включая саму плиту),
                    // обрезанный по границе.
                    var radius = BombTrapSystem.RadiusTiles;
                    for (var deltaRow = -radius; deltaRow <= radius; deltaRow++)
                    for (var deltaColumn = -radius; deltaColumn <= radius; deltaColumn++)
                    {
                        var candidateRow = row + deltaRow;
                        var candidateColumn = column + deltaColumn;
                        if (candidateRow >= 0 && candidateRow < template.RowCount &&
                            candidateColumn >= 0 && candidateColumn < template.Width)
                            yield return (candidateRow, candidateColumn);
                    }
                    break;

                case SegmentTileType.LavaWaveTrigger:
                    // LavaWaveTrapSystem: ряд триггера (offset 0) и до
                    // MaxRows-1 рядов НАЗАД (в сторону убывания Row), весь
                    // ряд целиком на каждом шаге. Инварианты «не ряд игрока/
                    // не ряд впереди» и «не Алтарь» — рантайм-only (знание о
                    // текущей позиции игрока/о том, что плита стала Алтарём,
                    // недоступно на этапе авторинга) — эта проверка
                    // сознательно консервативнее: считает область поражения
                    // максимальной, не полагаясь на то, что рантайм её
                    // сузит.
                    for (var offset = 0; offset < LavaWaveTrapSystem.MaxRows; offset++)
                    {
                        var candidateRow = row - offset;
                        if (candidateRow < 0) break;
                        for (var c = 0; c < template.Width; c++)
                            yield return (candidateRow, c);
                    }
                    break;

                // SegmentTileType.FallingRockTrigger и любой прочий тип:
                // камень падает только на саму плиту-триггер
                // (Movement.FallingRockTrapSystem) — плита не может
                // одновременно быть триггером и наградой (SegmentTileType —
                // одно значение на клетку), конфликт физически невозможен,
                // проверять нечего.
                default:
                    yield break;
            }
        }

        private static bool IsTriggerType(SegmentTileType type) => type switch
        {
            SegmentTileType.ArrowWaveTrigger => true,
            SegmentTileType.BombTrigger => true,
            SegmentTileType.BladeTactTrigger => true,
            SegmentTileType.LavaWaveTrigger => true,
            SegmentTileType.FallingRockTrigger => true,
            _ => false,
        };

        private static bool IsRewardType(SegmentTileType type) => type switch
        {
            SegmentTileType.ManaSource => true,
            SegmentTileType.KeySource => true,
            SegmentTileType.GateVaultKeySource => true,
            _ => false,
        };
    }
}
