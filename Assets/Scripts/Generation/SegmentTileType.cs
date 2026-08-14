namespace Burmalda.Generation
{
    /// <summary>
    /// Тип отдельной плиты внутри шаблона сегмента (PRD v7 §21, issue #78).
    /// Значения переиспользуют уже существующую механику ловушек (раздел
    /// 4.2) — сегментная генерация заменяет только СПОСОБ расстановки
    /// (авторские шаблоны вместо броска на каждую плиту), не саму механику
    /// ловушек. См. <see cref="SegmentRowProvider"/> — маппинг на методы
    /// <c>Tile.Mark*</c>.
    /// </summary>
    public enum SegmentTileType
    {
        /// <summary>Обычная проходимая плита.</summary>
        Open,

        /// <summary>Статичное препятствие (PRD 4.2, issue #9).</summary>
        Blocked,

        /// <summary>Статичная смертельная ловушка — яма (PRD 4.2, issue #9).</summary>
        Pit,

        /// <summary>Статичная смертельная ловушка — лава (PRD 4.2, issue #9).</summary>
        Lava,

        /// <summary>
        /// Триггер динамической мгновенной ловушки (взрыв, issue #10) — цель
        /// автоматически следующая плита по глубине (тот же столбец), как в
        /// <c>TunnelObstacleGenerator</c>. Запрещён на последнем ряду шаблона
        /// (см. <see cref="SegmentTemplate"/>) — цель обязана попадать внутрь
        /// того же сегмента.
        /// </summary>
        ExplosiveTrigger,

        /// <summary>Триггер подвижной ловушки с таймингом — стрела (issue #45). Те же ограничения, что у <see cref="ExplosiveTrigger"/>.</summary>
        TimedTrapArrowTrigger,

        /// <summary>Триггер подвижной ловушки с таймингом — лезвие (issue #45). Те же ограничения, что у <see cref="ExplosiveTrigger"/>.</summary>
        TimedTrapBladeTrigger,

        /// <summary>
        /// Рычаг (PRD 4.2, раздел 21, issue #51) — не ловушка. Активация
        /// (проход трейла через эту плиту) открывает все плиты
        /// <see cref="LeverGate"/> того же шаблона. Не более одного на шаблон
        /// (см. <see cref="SegmentTemplate"/>).
        /// </summary>
        Lever,

        /// <summary>
        /// Плита короткого бокового прохода к артефакту (issue #51) —
        /// непроходима, пока связанный <see cref="Lever"/> того же шаблона не
        /// активирован. Требует ровно одного <see cref="Lever"/> в шаблоне.
        /// </summary>
        LeverGate,

        /// <summary>Источник Кристаллов Маны (PRD раздел 5, issue #12) — проходима, не ловушка.</summary>
        ManaSource,

        /// <summary>Источник Ключей (PRD раздел 5, issue #12) — проходима, не ловушка.</summary>
        KeySource,

        /// <summary>Клетка-Алтарь (PRD раздел 7, issue #19) — проходима, запускает Ритуал.</summary>
        Altar
    }
}
