using System;
using System.Collections.Generic;
using System.Linq;
using Burmalda.Artifacts;
using Burmalda.Currencies;

namespace Burmalda.Camp
{
    /// <summary>
    /// Лагерь (PRD v9 раздел 11, issue #27) — перманентный шоп: "Апгрейды
    /// Идолов и Тотема за монеты (постоянные)", "расширение общего пула
    /// артефактов через принесённые реликвии". Не хранит собственное
    /// состояние — оперирует уже существующим постоянным кошельком/пулом
    /// (см. <c>Currencies.CurrencyController</c>, <c>Altar.AltarController.Pool</c>).
    ///
    /// <b>Разлок топовых артефактов больше не покупается ни за какую
    /// валюту</b> (v9, задача по экономике "Мана как доход забега, Монеты
    /// только в Лагере", Блокер 1): в v8 здесь был <c>TryUnlockArtifact</c>,
    /// тративший Кристаллы — вместе с удалением Кристаллов из игры метод
    /// удалён без замены. Разлок теперь выдаётся исключительно двумя уже
    /// существующими путями, оба безусловны и не требуют валюты:
    /// доставкой Реликвии (<see cref="OpenRelic"/>) и выполнением
    /// Достижения (<c>Achievements.AchievementTracker.Evaluate</c>).
    /// </summary>
    public sealed class Camp
    {
        private readonly PersistentWallet _coins;
        private readonly ArtifactPool _pool;
        private readonly List<Sarcophagus> _unopened = new List<Sarcophagus>();

        private readonly Random _rng;

        public Camp(PersistentWallet coins, ArtifactPool pool, CollectionProgress progress = null, Random rng = null)
        {
            _coins = coins ?? throw new ArgumentNullException(nameof(coins));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            Progress = progress ?? new CollectionProgress();
            _rng = rng ?? new Random();
        }

        /// <summary>Постоянный прогресс сбора: части Идолов/Тотемов, защита от невезения, флаг первой части (§21, §29).</summary>
        public CollectionProgress Progress { get; }

        /// <summary>Игроку предложен выбор «чья часть» (§21.4) — пока он не выбрал, следующий Саркофаг не открывается.</summary>
        public IReadOnlyList<CollectibleItem> PendingPartChoice { get; private set; }

        /// <summary>Игрок выбрал, в какой предмет уходит часть.</summary>
        public event Action<CollectibleItem> PartChosen;

        /// <summary>Привезённые, но ещё не открытые Саркофаги — от менее ярких к более ярким (PRD v10 §22.3).</summary>
        public IReadOnlyList<Sarcophagus> UnopenedSarcophagi => _unopened;

        /// <summary>Очередь неоткрытых изменилась (привезли или открыли) — для сохранения.</summary>
        public event Action UnopenedChanged;

        /// <summary>Открыт очередной Саркофаг (Монеты уже зачислены).</summary>
        public event Action<SarcophagusContents> SarcophagusOpened;

        /// <summary>Привезённые Саркофаги встают в очередь; порядок — по рангу (стабильно).</summary>
        public void AddUnopened(IEnumerable<Sarcophagus> sarcophagi)
        {
            if (sarcophagi == null) throw new ArgumentNullException(nameof(sarcophagi));
            _unopened.AddRange(sarcophagi);
            // От менее ярких к более ярким (§22.3): по редкости, затем по рангу бога. OrderBy стабилен.
            var ordered = _unopened.OrderBy(s => (int)s.Rarity).ThenBy(s => (int)s.Rank).ToList();
            _unopened.Clear();
            _unopened.AddRange(ordered);
            UnopenedChanged?.Invoke();
        }

        /// <summary>Открывает следующий Саркофаг: Монеты зачисляются. Null, если открывать нечего.</summary>
        public SarcophagusContents? OpenNext()
        {
            if (_unopened.Count == 0 || PendingPartChoice != null) return null;
            var next = _unopened[0];
            _unopened.RemoveAt(0);
            var loot = SarcophagusLootRoller.Roll(next, Progress, _pool, _rng);
            _coins.Deposit(loot.Coins);
            if (loot.Kind == SarcophagusLootKind.Relic) _pool.Unlock(loot.Relic.Id); // открыта навсегда, попадает в предложения Алтарей
            if (loot.NeedsPartChoice) PendingPartChoice = loot.PartChoices;
            var contents = new SarcophagusContents(loot);
            UnopenedChanged?.Invoke();
            SarcophagusOpened?.Invoke(contents);
            return contents;
        }

        /// <summary>Выбор «чья часть» (§21.4): часть уходит в выбранный из предложенных предметов. False, если выбора нет или предмета нет среди предложенных.</summary>
        public bool ChoosePart(string itemId)
        {
            if (PendingPartChoice == null) return false;
            var item = PendingPartChoice.FirstOrDefault(i => i.Id == itemId);
            if (item == null) return false;
            PendingPartChoice = null;
            Progress.AddPart(item.Id);
            PartChosen?.Invoke(item);
            return true;
        }
    }
}
