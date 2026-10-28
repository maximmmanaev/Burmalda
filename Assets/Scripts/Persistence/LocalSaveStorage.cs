using System;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.IO;
#endif
using UnityEngine;

namespace Burmalda.Persistence
{
    /// <summary>
    /// Локальное хранилище. В WebGL-плеере — <c>PlayerPrefs</c> (System.IO там не используем: виртуальная ФС
    /// браузера ненадёжна), в остальных случаях — прежний файл в <c>persistentDataPath</c>.
    /// </summary>
    public sealed class LocalSaveStorage : ISaveStorage
    {
        private const string Key = "burmalda_save";
        private const string FileName = "burmalda_save.json";

#if UNITY_WEBGL && !UNITY_EDITOR
        public void Read(Action<string> onRead)
        {
            onRead(PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null);
        }

        public void Write(string text, Action<bool> onDone)
        {
            PlayerPrefs.SetString(Key, text);
            PlayerPrefs.Save();
            onDone(true);
        }
#else
        private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public void Read(Action<string> onRead)
        {
            try
            {
                onRead(File.Exists(SavePath) ? File.ReadAllText(SavePath) : null);
            }
            catch (IOException)
            {
                onRead(null);
            }
        }

        public void Write(string text, Action<bool> onDone)
        {
            try
            {
                File.WriteAllText(SavePath, text);
                onDone(true);
            }
            catch (IOException)
            {
                onDone(false); // диск недоступен — следующая попытка повторит запись
            }
        }
#endif
    }
}
