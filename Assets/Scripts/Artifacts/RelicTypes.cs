namespace Burmalda.Artifacts
{
    /// <summary>Бог / дух-хранитель Святилища (PRD v10 §5.2). Порядок = номер Яруса − 1.</summary>
    public enum God
    {
        Leshy,
        Mokosh,
        Perun,
        Humbaba,
        Ereshkigal,
        Enlil,
        Memegwesi,
        Mishibijiu,
        Animikii,
        Chernobog,
    }

    /// <summary>Мир (PRD v10 §5.2): определяет пул Реликвий, предлагаемых на Алтаре.</summary>
    public enum Mythology
    {
        Slavic,
        Sumerian,
        Ojibwe,
    }

    /// <summary>Редкость Реликвии (PRD v10 §20.1): у каждого бога по одной.</summary>
    public enum RelicRarity
    {
        Rare,
        Epic,
        Legendary,
    }
}
