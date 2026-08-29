using System;
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
    }
}
