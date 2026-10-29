using System.Collections.Generic;
using System.Linq;
using Burmalda.Camp;
using CampModel = Burmalda.Camp.Camp;
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
            ArtifactPool pool, DepthRecord depthRecord, CampModel camp) =>
            new SaveData
            {
                coinsBalance = coins.Balance,
                collectionRecordedIds = new List<string>(collection.RecordedIds),
                poolUnlockedIds = new List<string>(pool.UnlockedIds),
                depthRecordBestTier = depthRecord.BestTier,
                unopenedSarcophagiRanks = camp.UnopenedSarcophagi.Select(x => (int)x.Rank).ToList(),
                unopenedSarcophagiRarities = camp.UnopenedSarcophagi.Select(x => (int)x.Rarity).ToList(),
                unopenedSarcophagiGods = camp.UnopenedSarcophagi.Select(x => (int)x.God).ToList(),
                partItemIds = camp.Progress.Parts.Keys.ToList(),
                partItemCounts = camp.Progress.Parts.Values.ToList(),
                partMisses = Enumerable.Range(0, 3).Select(i => camp.Progress.PartMisses((RelicRarity)i)).ToList(),
                relicMisses = Enumerable.Range(0, 3).Select(i => camp.Progress.RelicMisses((RelicRarity)i)).ToList(),
                firstPartGranted = camp.Progress.FirstPartGranted,
                activeIdolId = camp.Progress.ActiveIdolId ?? "",
                activeTotemId = camp.Progress.ActiveTotemId ?? "",
            };

        // Значение перечисления из параллельного списка; нет записи или значение вне диапазона — по умолчанию.
        private static T SafeEnum<T>(List<int> list, int index, T fallback) where T : struct, System.Enum =>
            list != null && index < list.Count && System.Enum.IsDefined(typeof(T), list[index]) ? (T)System.Enum.ToObject(typeof(T), list[index]) : fallback;

        public static void Apply(SaveData data, PersistentWallet coins, ArtifactCollection collection,
            ArtifactPool pool, DepthRecord depthRecord, CampModel camp)
        {
            if (data == null) return;

            coins.Deposit(data.coinsBalance);
            foreach (var id in data.collectionRecordedIds) collection.Record(id);
            foreach (var id in data.poolUnlockedIds) pool.Unlock(id);
            depthRecord.ReportTier(data.depthRecordBestTier);
            if (data.unopenedSarcophagiRanks != null)
                camp.AddUnopened(data.unopenedSarcophagiRanks.Select((r, i) => new Sarcophagus(
                    (SarcophagusRank)r,
                    SafeEnum(data.unopenedSarcophagiRarities, i, RelicRarity.Rare),
                    SafeEnum(data.unopenedSarcophagiGods, i, God.Leshy))));

            if (data.partItemIds != null && data.partItemCounts != null)
                for (var i = 0; i < data.partItemIds.Count && i < data.partItemCounts.Count; i++)
                    camp.Progress.SetParts(data.partItemIds[i], data.partItemCounts[i]);
            for (var i = 0; i < 3; i++)
            {
                if (data.partMisses != null && i < data.partMisses.Count) camp.Progress.SetPartMisses((RelicRarity)i, data.partMisses[i]);
                if (data.relicMisses != null && i < data.relicMisses.Count) camp.Progress.SetRelicMisses((RelicRarity)i, data.relicMisses[i]);
            }

            camp.Progress.FirstPartGranted = data.firstPartGranted;
            camp.Progress.RestoreActive(data.activeIdolId, data.activeTotemId);
        }
    }
}
