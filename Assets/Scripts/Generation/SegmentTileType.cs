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

        /// <summary>
        /// Тайник за Воротами (задача «видимые рычаги, инвариант лавы,
        /// размер награды за Воротами», владелец, 2026-09-04): источник
        /// Ключей, но сумма — из <see cref="SegmentTemplate.GateVaultPurchases"/>
        /// этого шаблона через <see cref="GateVaultPricing.ComputeVaultKeys"/>,
        /// не общий <c>Currencies.CurrencyController.KeysPerSource</c>. Не
        /// более одного на шаблон (см. <see cref="SegmentTemplate"/>) — тот
        /// же принцип, что у <see cref="Lever"/>.
        /// </summary>
        GateVaultKeySource,

        /// <summary>Клетка-Алтарь (PRD раздел 7, issue #19) — проходима, запускает Ритуал.</summary>
        Altar,

        /// <summary>Точка встречи с Боссом (PRD раздел 8, issue #22) — проходима, встреча разрешается автоматически.</summary>
        Boss,

        /// <summary>
        /// Баг с устройства (владелец, 2026-09-04, «новых ловушек в игре
        /// нет») — пять типов ловушек Спринта 13a (issues #213–#217,
        /// <c>Movement.TurnBasedThreatScheduler</c>) были написаны и покрыты
        /// тестами, но ни для одной не было символа шаблона, поэтому ни одна
        /// не могла попасть на реальную трассу. Пять значений ниже это
        /// закрывают — по аналогии с <see cref="TimedTrapArrowTrigger"/>, но
        /// для НОВОЙ механики (см. doc-комментарии соответствующих систем в
        /// <c>Burmalda.Movement</c>, они не взаимозаменяемы со старой).
        ///
        /// Триггер волны «Стрела» (issue #213) — цель по умолчанию: ряд
        /// самого триггера, направление слева-направо (владелец не задавал
        /// разного направления по шаблонам явно — единообразный дефолт,
        /// см. <see cref="SegmentRowProvider.ApplyTileType"/>).
        /// </summary>
        ArrowWaveTrigger,

        /// <summary>Триггер «Бомбы» (issue #214) — площадь взрыва центрирована на самой этой плите, без отдельной цели.</summary>
        BombTrigger,

        /// <summary>Триггер такта «Лезвий» (issue #215) — цель по умолчанию: ряд самого триггера (тот же принцип, что <see cref="ArrowWaveTrigger"/>).</summary>
        BladeTactTrigger,

        /// <summary>Триггер «Падающего камня» (issue #217) — камень падает на саму эту плиту, без отдельной цели.</summary>
        FallingRockTrigger,

        /// <summary>Триггер волны «Лава» (issue #216) — волна расходится от самой этой плиты назад по тоннелю, без отдельной цели.</summary>
        LavaWaveTrigger
    }
}
