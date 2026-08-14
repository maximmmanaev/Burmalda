using System;
using Burmalda.Artifacts;
using Burmalda.Currencies;

namespace Burmalda.Camp
{
    /// <summary>
    /// Лагерь (PRD раздел 11, issue #27) — перманентный шоп: "Апгрейды
    /// Идолов и Тотема за монеты (постоянные)", "разлок топовых амулетов/
    /// талисманов/идолов и лучших тотемов за кристаллы", "расширение
    /// общего пула артефактов через принесённые реликвии". Не хранит
    /// собственное состояние — оперирует уже существующими постоянными
    /// кошельками/пулом (см. <c>Currencies.CurrencyController</c>,
    /// <c>Altar.AltarController.Pool</c>).
    ///
    /// PRD не называет конкретные "топовые" артефакты — <see cref="TryUnlockArtifact"/>
    /// принимает Id и цену внешне, механизм общий для любого вида
    /// артефакта, а не привязан к заранее захардкоженному списку.
    /// </summary>
    public sealed class Camp
    {
        private readonly PersistentWallet _coins;
        private readonly PersistentWallet _crystals;
        private readonly ArtifactPool _pool;

        public Camp(PersistentWallet coins, PersistentWallet crystals, ArtifactPool pool)
        {
            _coins = coins ?? throw new ArgumentNullException(nameof(coins));
            _crystals = crystals ?? throw new ArgumentNullException(nameof(crystals));
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

        /// <summary>Разлокирует вид артефакта в общем пуле за Кристаллы. False, если уже разлочен или Кристаллов не хватило.</summary>
        public bool TryUnlockArtifact(string artifactId, int cost)
        {
            if (_pool.IsUnlocked(artifactId)) return false;
            if (!_crystals.Spend(cost)) return false;

            _pool.Unlock(artifactId);
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
