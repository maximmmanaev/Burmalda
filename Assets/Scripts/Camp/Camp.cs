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

        public Camp(PersistentWallet coins, ArtifactPool pool)
        {
            _coins = coins ?? throw new ArgumentNullException(nameof(coins));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        }

        /// <summary>Апгрейдит первый пассив Идола за Монеты. False, если Монет не хватило.</summary>
        public bool TryUpgradeIdolPassiveA(Idol idol, int cost)
        {
            if (!_coins.Spend(cost)) return false;
            idol.UpgradePassiveA();
            return true;
        }

        /// <summary>Апгрейдит второй пассив Идола за Монеты. False, если Монет не хватило.</summary>
        public bool TryUpgradeIdolPassiveB(Idol idol, int cost)
        {
            if (!_coins.Spend(cost)) return false;
            idol.UpgradePassiveB();
            return true;
        }

        /// <summary>Апгрейдит активную способность Тотема за Монеты. False, если Монет не хватило.</summary>
        public bool TryUpgradeTotem(Totem totem, int cost)
        {
            if (!_coins.Spend(cost)) return false;
            totem.UpgradeLevel();
            return true;
        }

        /// <summary>
        /// Открывает принесённую Реликвию — расширяет общий пул новым
        /// Идолом/Тотемом (PRD раздел 11). Какой именно артефакт выпадает —
        /// решение вызывающей стороны (см. PRD раздел 8: "Тотем ИЛИ Идол"),
        /// этот метод только применяет уже сделанный выбор.
        /// </summary>
        public void OpenRelic(Relic relic, Artifact grantedIdolOrTotem)
        {
            if (relic == null) throw new ArgumentNullException(nameof(relic));
            if (grantedIdolOrTotem == null) throw new ArgumentNullException(nameof(grantedIdolOrTotem));

            _pool.Unlock(grantedIdolOrTotem.Id);
        }

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
            var ordered = _unopened.OrderBy(s => (int)s.Rank).ToList(); // OrderBy стабилен
            _unopened.Clear();
            _unopened.AddRange(ordered);
            UnopenedChanged?.Invoke();
        }

        /// <summary>Открывает следующий Саркофаг: Монеты зачисляются. Null, если открывать нечего.</summary>
        public SarcophagusContents? OpenNext()
        {
            if (_unopened.Count == 0) return null;
            var next = _unopened[0];
            _unopened.RemoveAt(0);
            var contents = next.Open();
            _coins.Deposit(contents.Coins);
            UnopenedChanged?.Invoke();
            SarcophagusOpened?.Invoke(contents);
            return contents;
        }
    }
}
