namespace Burmalda.Generation
{
    /// <summary>
    /// Тег награды сегмента (PRD v7 §21, issue #78): "тег награды (Мана /
    /// Ключи / Монеты / артефакт)" — метаданные для селектора, не
    /// фактическая выдача валюты на плитах (валюты — Спринт 4, артефакты —
    /// Спринт 5). Примеры именованных шаблонов из PRD — "россыпь Ключей",
    /// "жила Маны" — соответствуют <see cref="Keys"/>/<see cref="Mana"/>.
    /// </summary>
    public enum SegmentRewardTag
    {
        Mana,
        Keys,
        Coins,
        Artifact
    }
}
