using Burmalda.Camp;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Мост между <see cref="ReturnJourneySystem.Returned"/> (живая система,
    /// уже создаётся и владеется <see cref="CampController"/> на каждый
    /// забег) и <see cref="RunHudDataSources.LastReturnResult"/> — задача по
    /// экономике "Мана как доход забега, Монеты только в Лагере" (PRD v9
    /// раздел 5/10) требует показать разбивку конвертации на результатах
    /// забега, а событие раньше никто не слушал.
    ///
    /// Ленивый поиск в <see cref="Update"/> — DebugVisuals читает Camp, а не
    /// наоборот (см. <c>Burmalda.DebugVisuals.asmdef</c> — ссылка на
    /// <c>Burmalda.Camp</c> добавлена для этого моста). До задачи
    /// "композиционный корень RunBootstrap" тем же принципом (ленивый поиск
    /// + самобутстрап) закрывали и обычные игровые системы (см. удалённый
    /// <c>CurrencyControllerBootstrap</c>) — теперь так остаётся только у
    /// debug-мостов вроде этого, сами системы собирает RunBootstrap.
    ///
    /// <b>Отдельно от <see cref="CampController"/>, а не подписка внутри
    /// него</b>: <see cref="CampController"/> — не-Debug класс, не должен
    /// зависеть от <see cref="DebugVisuals"/> и от статического HUD-состояния.
    ///
    /// <see cref="CampController.Journey"/> пересобирается на каждый забег
    /// (новый инстанс <see cref="ReturnJourneySystem"/>) — подписка
    /// сверяется каждый кадр по ссылке, тот же паттерн, что
    /// <c>CampController.SyncRunStateSubscription</c>.
    /// </summary>
    public sealed class ReturnConversionHudBridge : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(ReturnConversionHudBridge));
            host.AddComponent<ReturnConversionHudBridge>();
            DontDestroyOnLoad(host);
        }

        private CampController _camp;
        private ReturnJourneySystem _subscribedJourney;

        private void Update()
        {
            if (_camp == null) _camp = FindFirstObjectByType<CampController>();
            if (_camp == null) return;

            SyncJourneySubscription();
        }

        private void SyncJourneySubscription()
        {
            var current = _camp.Journey;
            if (ReferenceEquals(current, _subscribedJourney)) return;

            if (_subscribedJourney != null) _subscribedJourney.Returned -= HandleReturned;
            _subscribedJourney = current;
            if (_subscribedJourney != null) _subscribedJourney.Returned += HandleReturned;
        }

        private void HandleReturned(ReturnConversionResult result) => RunHudDataSources.LastReturnResult = result;
    }
}
