namespace Burmalda.RunModifiers
{
    /// <summary>
    /// Знамение Шахты (PRD v7 раздел 20, issue #84) — закрытый список из 6
    /// штук, буквально названных в тексте issue; в отличие от артефактов
    /// (открытый, авторский каталог), здесь enum, а не строковый Id.
    /// </summary>
    public enum OmenId
    {
        /// <summary>Порог распада плит −25% → Кристаллы Маны ×1.5.</summary>
        FragileVault,

        /// <summary>Ловушек ×2 → Ключи ×2.</summary>
        HuntingPath,

        /// <summary>1 Алтарь перед Боссом вместо 2 → Реликвия гарантированно даёт Идола.</summary>
        StingyAltar,

        /// <summary>Требуемая энергия Босса +30% → Монеты ×2 при возврате в Лагерь.</summary>
        HungryBoss,

        /// <summary>Подвижные ловушки не подсвечиваются заранее → +1 бесплатный реролл на каждом Алтаре.</summary>
        BlindDescent,

        /// <summary>Требуемая энергия Босса +50% → плиты-источники Маны ×2.</summary>
        RichVein
    }
}
