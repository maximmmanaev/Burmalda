namespace Burmalda.Core
{
    /// <summary>
    /// Тип плиты внутри Комнаты Босса (PRD v8 §8.2) — существуют только
    /// внутри Комнаты, исчезают на выходе. Живёт в Core (как
    /// <see cref="LethalTrapType"/>/<see cref="TimedTrapType"/>), а не в
    /// сборке BossRoom — <see cref="Tile"/> должен уметь хранить этот тип,
    /// не зная о системе, которая его интерпретирует.
    /// </summary>
    public enum BossRoomTileKind
    {
        /// <summary>+N Кристаллов Маны, умноженных на текущий множитель Комнаты (PRD v8 §8.2).</summary>
        Vein,

        /// <summary>+0.5 к множителю Комнаты до конца Комнаты (PRD v8 §8.2).</summary>
        Resonance,

        /// <summary>Тикающее накопление пока стоишь; плита под ногами рушится вдвое быстрее обычной (PRD v8 §8.2).</summary>
        Rift
    }
}
