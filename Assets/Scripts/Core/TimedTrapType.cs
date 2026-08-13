namespace Burmalda.Core
{
    /// <summary>
    /// Тип подвижной динамической ловушки с таймингом (PRD v5 4.2, issue
    /// #45): «запускается с плиты-триггера и движется по определённой
    /// траектории/полосе в течение нескольких мгновений». Оба варианта
    /// механически идентичны (см. <c>Burmalda.Movement.TimedTrapSystem</c>) —
    /// разделены только для тематической/визуальной разницы, по аналогии с
    /// <see cref="LethalTrapType.Pit"/>/<see cref="LethalTrapType.Lava"/>.
    /// </summary>
    public enum TimedTrapType
    {
        Arrow,
        Blade
    }
}
