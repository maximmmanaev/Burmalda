namespace Burmalda.Core
{
    /// <summary>Тип угрозы (PRD v10 §8.4) — от чего Амулет защищает; Афиша показывает эти типы у Святилища. Живёт в Core, чтобы ловушки и состояние забега могли его использовать.</summary>
    public enum ThreatType
    {
        /// <summary>Тупой удар — Камень, Валун, Давилка, Стены.</summary>
        Blunt,

        /// <summary>Режущий — Стрелы, Лезвия.</summary>
        Slashing,

        /// <summary>Взрыв — Бомба.</summary>
        Explosion,

        /// <summary>Огонь — Огненная струя.</summary>
        Fire,

        /// <summary>Капкан.</summary>
        Trap,
    }
}
