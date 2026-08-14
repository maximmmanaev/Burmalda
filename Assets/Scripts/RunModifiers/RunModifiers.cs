namespace Burmalda.RunModifiers
{
    /// <summary>
    /// Единая точка применения модификаторов забега (PRD v7 раздел 20,
    /// issue #84): "обязательна, иначе эффекты Знамений/Созвучий/Рун
    /// расползутся по системам и станут неотлаживаемыми". Один активный
    /// Знамение на забег (или null — «Чистый спуск», без штрафа и без
    /// награды, PRD v7 §20); Созвучия/Руны сюда пока не сведены — они
    /// применяются каждое своей уже существующей системой
    /// (<c>Artifacts.ResonanceCalculator</c>, <c>Totem.UpgradeLevel</c>) и
    /// не выражаются числовым модификатором на этот класс — риск "не единой
    /// точки" остаётся частично, задокументирован здесь честно, а не скрыт.
    ///
    /// Каждое свойство ниже — ОДНА строка PRD-таблицы, все числа — из
    /// текста issue #84 дословно. Реально читают эти свойства только
    /// системы, для которых интеграция была безопасна без риска регресса
    /// уже стабильного, протестированного кода (см. changelog Спринта 9):
    /// <c>Decay.TrailDecaySystem</c> (через её собственный конструкторный
    /// параметр, а не прямую ссылку на этот класс — Decay.asmdef
    /// сознательно не получает зависимость от RunModifiers), аналогично
    /// <c>Currencies.TrailTileCurrencySystem</c>, <c>Altar.Ritual</c>
    /// (свободные рероллы) и <c>Camp.ReturnJourneySystem</c> (Монеты при
    /// возврате). Требуемая энергия Босса — читается вызывающей стороной
    /// (Boss.BossController) при построении уже существующего
    /// параметра-делегата <c>requiredEnergyForTier</c>, без изменений в
    /// Boss.BossEncounterSystem.
    ///
    /// Оставшиеся эффекты (плотность ловушек/плиток-источников Маны — нужна
    /// правка контент-генерации в Generation; «1 Алтарь перед Боссом» и
    /// гарантированный Идол от Реликвии — базовая механика "2 Алтаря перед
    /// Боссом" в проекте ещё не реализована вовсе, см. README строку
    /// `Boss/`; подсветка ловушек — вообще нет UI/визуального слоя) —
    /// СВОЙСТВА здесь есть (готовы к использованию), но ни одна система их
    /// пока не читает — честно задокументированный пробел, не молчаливое
    /// решение.
    ///
    /// Живой Controller, выбирающий Знамение в Лагере перед спуском и
    /// прокидывающий готовый экземпляр этого класса во все Controller'ы,
    /// перечисленные выше, на каждый <c>GridTraceInputController.RunStarted</c>
    /// — тоже не реализован в этом спринте: это тронуло бы 5 уже
    /// стабильных MonoBehaviour без возможности живой компиляции в Unity
    /// Editor в этой сессии (см. docs/wiki/changelog.md).
    /// </summary>
    public sealed class RunModifiers
    {
        /// <summary>Нейтральный экземпляр — эквивалент «Чистого спуска» (PRD v7 §20).</summary>
        public static RunModifiers None { get; } = new RunModifiers(null);

        public RunModifiers(OmenId? activeOmen)
        {
            ActiveOmen = activeOmen;
        }

        public OmenId? ActiveOmen { get; }

        /// <summary>Хрупкий Свод: порог распада плит −25%.</summary>
        public float DecayThresholdMultiplier => ActiveOmen == OmenId.FragileVault ? 0.75f : 1f;

        /// <summary>Хрупкий Свод: Кристаллы Маны ×1.5.</summary>
        public float ManaCrystalRewardMultiplier => ActiveOmen == OmenId.FragileVault ? 1.5f : 1f;

        /// <summary>Ловчая Тропа: ловушек ×2 (не подключено — нужна правка Generation).</summary>
        public float TrapDensityMultiplier => ActiveOmen == OmenId.HuntingPath ? 2f : 1f;

        /// <summary>Ловчая Тропа: Ключи ×2.</summary>
        public float KeyRewardMultiplier => ActiveOmen == OmenId.HuntingPath ? 2f : 1f;

        /// <summary>Скупой Алтарь: 1 Алтарь перед Боссом вместо 2 (не подключено — базовая механика "2 Алтаря" ещё не реализована).</summary>
        public int AltarsBeforeBossCount => ActiveOmen == OmenId.StingyAltar ? 1 : 2;

        /// <summary>Скупой Алтарь: Реликвия гарантированно даёт Идола (не подключено — нет генерации содержимого Реликвии).</summary>
        public bool RelicGuaranteesIdol => ActiveOmen == OmenId.StingyAltar;

        /// <summary>Голодный Босс/Богатая Жила: требуемая энергия Босса ×1.3/×1.5.</summary>
        public float BossRequiredEnergyMultiplier =>
            ActiveOmen switch
            {
                OmenId.HungryBoss => 1.3f,
                OmenId.RichVein => 1.5f,
                _ => 1f
            };

        /// <summary>Голодный Босс: Монеты ×2 при возврате в Лагерь.</summary>
        public float CoinsOnReturnMultiplier => ActiveOmen == OmenId.HungryBoss ? 2f : 1f;

        /// <summary>Слепой Спуск: подвижные ловушки не подсвечиваются заранее (не подключено — нет UI-подсветки вообще).</summary>
        public bool TimedTrapsHiddenUntilTriggered => ActiveOmen == OmenId.BlindDescent;

        /// <summary>Слепой Спуск: +1 бесплатный реролл на каждом Алтаре.</summary>
        public int BonusFreeRerollsPerAltar => ActiveOmen == OmenId.BlindDescent ? 1 : 0;

        /// <summary>Богатая Жила: плиты-источники Маны ×2 (не подключено — нужна правка Generation).</summary>
        public float ManaSourceTileDensityMultiplier => ActiveOmen == OmenId.RichVein ? 2f : 1f;
    }
}
