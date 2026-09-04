namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Категория арта плиты тоннеля (issue "Завести арт в игру"), на которую
    /// маппится <see cref="TileVisualState"/> в <see cref="TileArtKindResolver"/>.
    /// Отдельный enum (а не сразу <c>UnityEngine.Texture2D</c>) — чтобы сам
    /// маппинг состояние→категория оставался чистой функцией, тестируемой
    /// без сцены и без Unity Texture API, по тому же принципу, по которому
    /// <see cref="TileVisualState"/> отделён от <c>Core.Tile</c>. Реальную
    /// текстуру для категории отдаёт <see cref="TileArtCatalog"/>.
    ///
    /// <see cref="None"/> — состояний, для которых готового арта в текущем
    /// пакете нет (текущая позиция игрока, вход в Комнату Босса — категорий
    /// <c>Boss/</c> в пакете нет, Комната Босса ещё не реализована в Unity,
    /// PRD v8): для них <see cref="TunnelDebugVisual"/> остаётся на
    /// цветном fallback (<see cref="TileDebugColor"/>), а не оставляет плиту
    /// без индикации вовсе.
    /// </summary>
    public enum TileArtKind
    {
        None,
        Fresh,

        /// <summary>
        /// Задача «разрушение плиты» (2026-09-01): <see cref="TileArtKindResolver"/>
        /// больше НЕ возвращает это значение (см. её doc-комментарий) —
        /// распад теперь непрерывный оверлей трещин
        /// (<see cref="TileArtCatalog.CrackMaskTexture"/>), не дискретная
        /// смена <see cref="TileArtKind"/>. Значение и текстура
        /// <c>tile-half-decayed.png</c> оставлены — она служит одним из
        /// двух ИСХОДНИКОВ для генерации маски трещин, слот в
        /// <see cref="TileArtCatalog.Get"/> тоже оставлен (на случай, если
        /// понадобится показать текстуру как есть где-то ещё), просто путь
        /// от <see cref="TileVisualState"/> до неё через резолвер закрыт.
        /// </summary>
        HalfDecayed,

        /// <summary>Тот же статус, что <see cref="HalfDecayed"/> — второй источник маски трещин (<c>tile-about-to-decay.png</c>).</summary>
        AboutToDecay,
        Destroyed,
        Start,
        Blocked,
        Lava,

        /// <summary>
        /// Общая сигнатура для ямы и уже активированного взрыва (issue #163,
        /// см. <c>Core.TrapSignature</c>) — ОДНА текстура на оба типа,
        /// намеренно не выдающая, какая именно ловушка под плитой (см.
        /// <see cref="TileArtKindResolver"/>).
        /// </summary>
        HiddenTrapSignature,

        /// <summary>Общая текстура для обеих разновидностей (стрела/лезвие) — как и <see cref="TileDebugColor.TimedTrapActiveColor"/>.</summary>
        TimedTrapActive,

        /// <summary>
        /// Задача «сделать тоннель играбельным», часть 3: единая сигнатура
        /// для ОБОИХ видов триггера (взрывной и с таймингом) — тот же
        /// принцип, что <see cref="HiddenTrapSignature"/>: факт "здесь
        /// механизм" виден всегда, тип — нет (см.
        /// <see cref="TileArtKindResolver"/>/<see cref="TileDebugColor.TriggerSignatureColor"/>).
        /// Заменяет прежние раздельные ExplosiveTrigger/TimedTrapTrigger.
        /// </summary>
        TriggerSignature,

        /// <summary>Задача «тёплый набор плит»: плита, на которой стоит игрок — раньше белый цветной фолбэк, теперь <c>tile-current-position.png</c>.</summary>
        CurrentPosition,

        /// <summary>Задача «тёплый набор плит»: источник Кристаллов Маны — раньше только цвет (<see cref="TileDebugColor.ManaSourceColor"/>), теперь <c>tile-mana-source.png</c>.</summary>
        ManaSource,

        /// <summary>Задача «тёплый набор плит»: источник Ключей — раньше только цвет, теперь <c>tile-key-source.png</c>.</summary>
        KeySource,

        /// <summary>Задача «тёплый набор плит»: рычаг — раньше только цвет, теперь <c>tile-lever.png</c>.</summary>
        Lever,

        /// <summary>Задача «тёплый набор плит»: закрытые ворота — раньше только цвет (прежний <c>tile-gate-closed.png</c> оказался непригоден, см. историю <see cref="TileArtKindResolver"/>), теперь настоящая плита пола.</summary>
        GateClosed,

        /// <summary>Открытые ворота — <c>tile-gate-open.png</c>, собран владельцем из второго варианта решётки (рама сохранена, прутья убраны).</summary>
        GateOpen,

        /// <summary>Алтарь (<c>Core.Tile.IsAltar</c>) — <c>tile-altar.png</c>.</summary>
        Altar,

        /// <summary>
        /// Баг с устройства (владелец, 2026-09-04, «вход в Комнату
        /// Босса — структурная плита маршрута, она обязана читаться»):
        /// раньше <see cref="TileArtKindResolver"/> возвращал
        /// <see cref="None"/> для <c>Core.Tile.IsBoss</c> (в тёплом наборе
        /// не было отдельной текстуры под Босса вовсе) — на устройстве это
        /// падало на холодный <see cref="TileDebugColor.BossColor"/>,
        /// единственную несовместимую с палитрой плиту на маршруте.
        /// Заводить новую текстуру не в скоупе агента (авторство контента,
        /// docs/rules/forbidden-actions.md) — переиспользует
        /// <c>tile-altar.png</c> (см. <see cref="TileArtCatalog.Get"/>, та
        /// же "структурная, не подверженная распаду" категория, что и
        /// Алтарь), но с собственным тёплым тоном
        /// (<see cref="TunnelDebugVisual.BossArtTint"/>) — Босс и Алтарь
        /// стоят рядом на маршруте (2 Алтаря перед Боссом, PRD v9 §8.1) и не
        /// должны визуально сливаться в одну и ту же плиту.
        /// </summary>
        Boss
    }
}
