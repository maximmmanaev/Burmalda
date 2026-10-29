namespace Burmalda.Generation
{
    /// <summary>
    /// Тег награды сегмента (PRD v7 §21, issue #78): "тег награды (Мана /
    /// Монеты / артефакт)" — метаданные для селектора, не фактическая выдача
    /// валюты на плитах. Ключи удалены (v10-3): прежний тег <c>Keys</c> заменён на <see cref="Mana"/>.
    /// </summary>
    public enum SegmentRewardTag
    {
        Mana,
        Coins,
        Artifact
    }
}
