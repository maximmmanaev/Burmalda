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
        HalfDecayed,
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
        ExplosiveTrigger,
        TimedTrapTrigger
    }
}
