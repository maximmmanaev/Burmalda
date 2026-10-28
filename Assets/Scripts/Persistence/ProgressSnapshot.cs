using System.Collections.Generic;
using Burmalda.Artifacts;
using Burmalda.Currencies;
using Burmalda.Progression;

namespace Burmalda.Persistence
{
    /// <summary>
    /// Снимок/восстановление сохраняемого прогресса (issue #107) — чистые
    /// функции над уже существующими постоянными объектами (не хранит
    /// состояние сама). <see cref="Apply"/> намеренно переиспользует
    /// обычные игровые методы (<c>Deposit</c>/<c>Record</c>/<c>Unlock</c>/
    /// <c>ReportTier</c>/<c>RecordVictory</c>) вместо отдельного API
    /// "восстановления" — все они уже идемпотентны/аддитивны и корректно
    /// работают на свежесозданных (Awake) пустых объектах, новый метод
    /// восстановления не понадобился ни одному из них.
    /// </summary>
    public static class ProgressSnapshot
    {
        public static SaveData Capture(PersistentWallet coins, ArtifactCollection collection,
            ArtifactPool pool, DepthRecord depthRecord) =>
            new SaveData
            {
                coinsBalance = coins.Balance,
                collectionRecordedIds = new List<string>(collection.RecordedIds),
                poolUnlockedIds = new List<string>(pool.UnlockedIds),
                depthRecordBestTier = depthRecord.BestTier
            };

        public static void Apply(SaveData data, PersistentWallet coins, ArtifactCollection collection,
            ArtifactPool pool, DepthRecord depthRecord)
        {
            if (data == null) return;

            coins.Deposit(data.coinsBalance);
            foreach (var id in data.collectionRecordedIds) collection.Record(id);
            foreach (var id in data.poolUnlockedIds) pool.Unlock(id);
            depthRecord.ReportTier(data.depthRecordBestTier);
        }
    }
}
