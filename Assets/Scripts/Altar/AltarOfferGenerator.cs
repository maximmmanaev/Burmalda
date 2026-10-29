using Burmalda.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Burmalda.Artifacts;

namespace Burmalda.Altar
{
    /// <summary>Вход генератора предложений Алтаря (PRD v10 §13.2).</summary>
    public sealed class AltarOfferRequest
    {
        /// <summary>Ярус ближайшего Святилища впереди (определяет мифологию пула).</summary>
        public int ShrineYarus { get; set; }

        /// <summary>Это Алтарь #2 Яруса — последний перед Святилищем (гарантия Амулета против угроз Афиши, если её ещё не было).</summary>
        public bool IsLastAltarBeforeShrine { get; set; }

        /// <summary>На Алтарях этого Яруса уже предлагался защитный Амулет.</summary>
        public bool ProtectiveAmuletAlreadyOffered { get; set; }

        /// <summary>Типы угроз Святилища, показанные Афишей.</summary>
        public IReadOnlyCollection<ThreatType> AfishaThreats { get; set; } = Array.Empty<ThreatType>();

        /// <summary>Подобран тайник за Воротами — на этом Алтаре третий вариант.</summary>
        public bool VaultBonus { get; set; }

        /// <summary>Открытые Реликвии (id) — Пул <see cref="ArtifactPool"/>.</summary>
        public Func<string, bool> IsOpened { get; set; }

        public RunRelicBuild Build { get; set; }

        /// <summary>Сид этого Алтаря — детерминизм по сиду похода.</summary>
        public int Seed { get; set; }
    }

    /// <summary>Результат генерации: варианты и пометки происхождения.</summary>
    public sealed class AltarOffer
    {
        public AltarOffer(IReadOnlyList<RelicDefinition> options, bool containsProtectiveAmulet, bool thirdFromTalisman)
        {
            Options = options;
            ContainsProtectiveAmulet = containsProtectiveAmulet;
            ThirdFromTalisman = thirdFromTalisman;
        }

        public IReadOnlyList<RelicDefinition> Options { get; }

        /// <summary>Среди вариантов есть Амулет, защищающий от угрозы с Афиши.</summary>
        public bool ContainsProtectiveAmulet { get; }

        /// <summary>Третий вариант выпал по Талисману «Ключ подземных врат».</summary>
        public bool ThirdFromTalisman { get; }
    }

    /// <summary>
    /// Предложение Алтаря «выбор 1 из 2» (PRD v10 §13.2). Правила: варианты — открытые Реликвии мифологии ближайшего
    /// Святилища, при нехватке — добор из любых открытых; Реликвия из текущего билда не предлагается; минимум на одном
    /// Алтаре Яруса — Амулет против угрозы с Афиши (если открыт и билд ещё не защищён); третий вариант — от тайника за
    /// Воротами или Талисмана «Ключ подземных врат» (шанс <see cref="ThirdOptionTalismanChance"/>), но не больше
    /// <see cref="MaxOptions"/>. Детерминировано по <see cref="AltarOfferRequest.Seed"/>.
    /// </summary>
    public static class AltarOfferGenerator
    {
        public const int BaseOptions = 2;
        public const int MaxOptions = 3;

        /// <summary>Шанс третьего варианта у Талисмана «Ключ подземных врат» [CONFIG, 33%, §20.2].</summary>
        public static float ThirdOptionTalismanChance = 0.33f;

        public static AltarOffer Generate(AltarOfferRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Build == null) throw new ArgumentException("Нужен билд забега.", nameof(request));
            if (request.IsOpened == null) throw new ArgumentException("Нужен предикат открытых Реликвий.", nameof(request));

            var rng = new Random(request.Seed);
            var want = BaseOptions;
            var thirdFromTalisman = false;
            if (request.VaultBonus) want++;
            else if (request.Build.Contains(RelicCatalog.ThirdOptionTalismanId) && rng.NextDouble() < ThirdOptionTalismanChance)
            {
                want++;
                thirdFromTalisman = true;
            }

            want = Math.Min(want, MaxOptions);

            var shrineMythology = GodTable.ForYarus(Math.Min(Math.Max(request.ShrineYarus, 1), GodTable.YarusCount)).Mythology;
            var open = RelicCatalog.All.Where(r => request.IsOpened(r.Id) && !request.Build.Contains(r.Id)).ToList();
            var mythPool = open.Where(r => r.Mythology == shrineMythology).ToList();
            var chosen = new List<RelicDefinition>();
            var protective = false;

            if (request.IsLastAltarBeforeShrine && !request.ProtectiveAmuletAlreadyOffered
                && request.AfishaThreats.Count > 0 && !request.Build.ProtectsAgainstAny(request.AfishaThreats))
            {
                var protectors = open.Where(r => r.Category == ArtifactCategory.Amulet && r.Protects.Any(request.AfishaThreats.Contains)).ToList();
                var preferred = protectors.Where(r => r.Mythology == shrineMythology).ToList();
                var source = preferred.Count > 0 ? preferred : protectors;
                if (source.Count > 0)
                {
                    chosen.Add(source[rng.Next(source.Count)]);
                    protective = true;
                }
            }

            Fill(chosen, mythPool, want, rng);
            Fill(chosen, open, want, rng);

            Shuffle(chosen, rng);
            return new AltarOffer(chosen, protective, thirdFromTalisman);
        }

        private static void Fill(List<RelicDefinition> chosen, List<RelicDefinition> from, int want, Random rng)
        {
            var rest = from.Where(r => !chosen.Contains(r)).ToList();
            while (chosen.Count < want && rest.Count > 0)
            {
                var i = rng.Next(rest.Count);
                chosen.Add(rest[i]);
                rest.RemoveAt(i);
            }
        }

        private static void Shuffle(List<RelicDefinition> list, Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
