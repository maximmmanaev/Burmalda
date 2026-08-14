using System;
using System.Collections.Generic;
using System.Linq;
using Burmalda.Artifacts;
using Burmalda.Currencies;

namespace Burmalda.Altar
{
    /// <summary>
    /// Ритуал (PRD раздел 7, issues #19/#20/#81) — витрина, запускаемая
    /// достижением клетки-Алтаря (см. <see cref="AltarTriggerSystem"/>).
    /// Предлагает основной слот (Талисман ИЛИ Амулет — из разблокированных в
    /// <see cref="ArtifactPool"/>, до первой победы над Боссом пул пуст и
    /// <see cref="PrimaryChest"/> остаётся null, что и означает "пройти
    /// мимо без потерь" при отсутствии Ключей — см. <see cref="AltarTriggerSystem"/>)
    /// и отдельно Сундук Рун — оба можно купить за Ключи, необязательно
    /// только один. Тёмный товар (PRD v7 §7) — вне релиза, issue #103.
    ///
    /// Цены сундуков (<see cref="TalismanOrAmuletChestCost"/>, <see cref="RuneChestCost"/>)
    /// и коэффициент продажи обратно (<see cref="SellCoefficient"/>) —
    /// черновые значения, предмет баланса (Спринт 10,
    /// docs/rules/forbidden-actions.md).
    /// </summary>
    public sealed class Ritual
    {
        public const int TalismanOrAmuletChestCost = 80;
        public const int RuneChestCost = 50;
        public const double SellCoefficient = 0.5;

        private static readonly Rune[] RuneCatalog =
        {
            new Rune("rune-idol-line", "Руна Идола", "идол"),
            new Rune("rune-totem-line", "Руна Тотема", "тотем"),
        };

        private readonly ArtifactPool _pool;
        private readonly Func<float> _random01;
        private int _rerollCount;

        public Ritual(ArtifactPool pool, Func<float> random01)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _random01 = random01 ?? throw new ArgumentNullException(nameof(random01));
            RollOffers();
        }

        /// <summary>Основной слот — Талисман/Амулет (изредка Реликвия, PRD раздел 7). Null, если ничего не разблокировано в Пуле.</summary>
        public Chest PrimaryChest { get; private set; }

        public RuneChest RuneChestOffer { get; private set; }

        /// <summary>Цена следующего реролла — ×1.5 за каждый предыдущий в этом посещении (PRD v7 §7).</summary>
        public int NextRerollCost => RerollPricing.CostForRerollNumber(_rerollCount + 1);

        /// <summary>Меняет оба предложенных сундука за Ключи. Возвращает false, если Ключей не хватило — предложения не меняются.</summary>
        public bool TryReroll(RunCurrencyAccumulator keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (!keys.Spend(NextRerollCost)) return false;

            _rerollCount++;
            RollOffers();
            return true;
        }

        /// <summary>Покупает основной слот и добавляет содержимое во временный билд забега. False, если слот пуст или Ключей не хватило.</summary>
        public bool TryPurchasePrimary(RunCurrencyAccumulator keys, RunArtifactLoadout loadout)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (PrimaryChest == null) return false;
            if (!keys.Spend(PrimaryChest.Cost)) return false;

            var content = PrimaryChest switch
            {
                AmuletChest amuletChest => (Artifact)amuletChest.Content,
                TalismanChest talismanChest => talismanChest.Content,
                _ => null
            };
            if (content != null) loadout.Acquire(content);
            PrimaryChest = null; // куплен — "распродан" до реролла/нового посещения, повторно не покупается
            return true;
        }

        /// <summary>Покупает Сундук Рун. Возвращает полученную Руну (применение к Идолу/Тотему — выбор игрока, см. issue #21) или null, если сундук уже куплен либо Ключей не хватило.</summary>
        public Rune TryPurchaseRuneChest(RunCurrencyAccumulator keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (RuneChestOffer == null) return null;
            if (!keys.Spend(RuneChestOffer.Cost)) return null;

            var content = RuneChestOffer.Content;
            RuneChestOffer = null; // куплен — "распродан" до реролла/нового посещения
            return content;
        }

        /// <summary>Цена продажи артефакта обратно Алтарю — дешевле цены покупки (PRD раздел 7).</summary>
        public int ComputeSellPrice(Artifact artifact) => (int)(TalismanOrAmuletChestCost * SellCoefficient);

        /// <summary>Какие Созвучия сломаются, если продать <paramref name="artifact"/> — для предупреждения игрока перед продажей (PRD v7 §7).</summary>
        public IReadOnlyList<ResonanceType> PreviewResonanceLossOnSell(Artifact artifact, RunArtifactLoadout loadout)
        {
            var before = loadout.ActiveResonances();
            var remainingTags = loadout.Acquired.Where(a => a != artifact).Select(a => a.Tags).ToList();
            var after = ResonanceCalculator.Compute(remainingTags, unityConditionMet: false);
            return before.Except(after).ToList();
        }

        /// <summary>Продаёт артефакт из билда забега обратно Алтарю за Ключи. False, если артефакта нет в билде.</summary>
        public bool TrySell(Artifact artifact, RunArtifactLoadout loadout, RunCurrencyAccumulator keys)
        {
            if (!loadout.Remove(artifact)) return false;
            keys.Add(ComputeSellPrice(artifact));
            return true;
        }

        private void RollOffers()
        {
            PrimaryChest = RollPrimaryChest();
            RuneChestOffer = new RuneChest(RuneChestCost, RuneCatalog[_random01() < 0.5f ? 0 : 1]);
        }

        private Chest RollPrimaryChest()
        {
            var candidates = new List<Artifact>();
            foreach (var amulet in ArtifactCatalog.Amulets)
                if (_pool.IsUnlocked(amulet.Id)) candidates.Add(amulet);
            foreach (var talisman in ArtifactCatalog.Talismans)
                if (_pool.IsUnlocked(talisman.Id)) candidates.Add(talisman);

            if (candidates.Count == 0) return null;

            var index = Math.Min((int)(_random01() * candidates.Count), candidates.Count - 1);
            var picked = candidates[index];

            return picked is Amulet amulet
                ? (Chest)new AmuletChest(TalismanOrAmuletChestCost, amulet)
                : new TalismanChest(TalismanOrAmuletChestCost, (Talisman)picked);
        }
    }
}
