using System;
using Burmalda.RunModifiers;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Issue #110 (PRD §19, «Выбор Знамения»): «карточка с текстом цены/
    /// награды... тексты берёшь из существующего каталога, своих не
    /// пишешь». Названия и пары «усложнение ↔ награда» — дословно из PRD v7
    /// раздел 20 и doc-комментариев <see cref="OmenId"/> (закрытый список,
    /// решение product owner'а, не придумано здесь). Отдельная таблица
    /// нужна только потому, что <see cref="OmenId"/> хранит текст в
    /// XML-doc, не в читаемых во время выполнения строковых константах.
    /// </summary>
    public static class OmenCardText
    {
        public readonly struct Card
        {
            public Card(string name, string complication, string reward)
            {
                Name = name;
                Complication = complication;
                Reward = reward;
            }

            public string Name { get; }
            public string Complication { get; }
            public string Reward { get; }
        }

        public static Card Resolve(OmenId omenId) =>
            omenId switch
            {
                OmenId.FragileVault => new Card("Хрупкий Свод", "порог распада плит −25%", "Кристаллы Маны ×1.5"),
                OmenId.HuntingPath => new Card("Ловчая Тропа", "ловушек ×2", "Ключи ×2"),
                OmenId.StingyAltar => new Card("Скупой Алтарь", "1 Алтарь перед Боссом вместо 2", "Реликвия гарантированно даёт Идола"),
                OmenId.HungryBoss => new Card("Голодный Босс", "требуемая энергия Босса +30%", "Монеты ×2 при возврате в Лагерь"),
                OmenId.BlindDescent => new Card("Слепой Спуск", "подвижные ловушки не подсвечиваются заранее", "+1 бесплатный реролл на каждом Алтаре"),
                OmenId.RichVein => new Card("Богатая Жила", "требуемая энергия Босса +50%", "плиты-источники Маны ×2"),
                _ => throw new ArgumentOutOfRangeException(nameof(omenId), omenId, null)
            };

        public static string BuildMessage(OmenId omenId)
        {
            var card = Resolve(omenId);
            return $"{card.Name}: {card.Complication} -> {card.Reward}";
        }
    }
}
