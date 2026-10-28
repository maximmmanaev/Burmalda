using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Burmalda.Persistence
{
    public enum SaveReadStatus
    {
        /// <summary>Данных нет (пусто, null, сбой чтения).</summary>
        Empty,
        Ok,
        /// <summary>Не разобралось как JSON или нет нужных полей.</summary>
        Corrupt,
        /// <summary>Записано более новой версией игры — не читаем и не перезаписываем.</summary>
        TooNew
    }

    public readonly struct SaveReadResult
    {
        public readonly SaveReadStatus Status;
        public readonly SaveData Data;
        public readonly long SavedAt;

        public SaveReadResult(SaveReadStatus status, SaveData data = null, long savedAt = 0)
        {
            Status = status;
            Data = data;
            SavedAt = savedAt;
        }
    }

    /// <summary>
    /// Текстовый формат сохранения: <see cref="SaveEnvelope"/> с версией. Файл без версии — прежний формат
    /// (голый <see cref="SaveData"/>, до облака) — читается как версия 0. Прочитанные данные приводятся в
    /// допустимый вид (без отрицательных чисел и пустых id), чтобы испорченный файл не ронял игру.
    /// При смене формата: поднять <see cref="CurrentVersion"/> и добавить ветку миграции в <see cref="Deserialize"/>.
    /// </summary>
    public static class SaveFormat
    {
        public const int CurrentVersion = 1;

        public static string Serialize(SaveData data, long savedAtUnix) =>
            JsonUtility.ToJson(new SaveEnvelope
            {
                version = CurrentVersion,
                savedAt = savedAtUnix,
                payload = JsonUtility.ToJson(data)
            });

        public static SaveReadResult Deserialize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new SaveReadResult(SaveReadStatus.Empty);

            SaveEnvelope env;
            try
            {
                env = JsonUtility.FromJson<SaveEnvelope>(text);
            }
            catch (ArgumentException)
            {
                return new SaveReadResult(SaveReadStatus.Corrupt);
            }
            if (env == null) return new SaveReadResult(SaveReadStatus.Corrupt);

            if (env.version > CurrentVersion) return new SaveReadResult(SaveReadStatus.TooNew);

            if (env.version == 0 && string.IsNullOrEmpty(env.payload))
            {
                // Версия 0: прежний файл без оболочки. Признак — известное поле, иначе «{}» сошёл бы за пустой прогресс.
                if (!text.Contains("\"coinsBalance\"")) return new SaveReadResult(SaveReadStatus.Corrupt);
                return Parse(text, 0);
            }

            if (env.version < 1 || string.IsNullOrEmpty(env.payload)) return new SaveReadResult(SaveReadStatus.Corrupt);
            return Parse(env.payload, env.savedAt);
        }

        private static SaveReadResult Parse(string json, long savedAt)
        {
            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (ArgumentException)
            {
                return new SaveReadResult(SaveReadStatus.Corrupt);
            }
            if (data == null) return new SaveReadResult(SaveReadStatus.Corrupt);
            return new SaveReadResult(SaveReadStatus.Ok, Sanitize(data), savedAt);
        }

        private static SaveData Sanitize(SaveData d)
        {
            d.coinsBalance = Math.Max(0, d.coinsBalance);
            d.depthRecordBestTier = Math.Max(0, d.depthRecordBestTier);
            d.collectionRecordedIds = CleanIds(d.collectionRecordedIds);
            d.poolUnlockedIds = CleanIds(d.poolUnlockedIds);
            return d;
        }

        private static List<string> CleanIds(List<string> ids) =>
            ids == null ? new List<string>() : ids.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
    }
}
