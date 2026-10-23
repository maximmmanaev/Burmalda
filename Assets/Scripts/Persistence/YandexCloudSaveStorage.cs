using System;
using System.Runtime.InteropServices;

namespace Burmalda.Persistence
{
    /// <summary>
    /// Облако Яндекс Игр (<c>player.getData/setData</c>, обёртка в <c>YandexGames.jslib</c>). Ответы приходят
    /// из JS через <c>SendMessage</c> на <see cref="SaveController"/> и попадают в <see cref="OnLoaded"/> и
    /// <see cref="OnWritten"/>. Вне WebGL-плеера облака нет: чтение даёт null, запись — false.
    /// </summary>
    public sealed class YandexCloudSaveStorage : ISaveStorage
    {
        /// <summary>Единственный живой экземпляр — адресат ответов из JS.</summary>
        public static YandexCloudSaveStorage Current { get; private set; }

        private readonly string _receiver;
        private Action<string> _pendingRead;
        private Action<bool> _pendingWrite;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void YG_LoadSave(string receiver);
        [DllImport("__Internal")] private static extern void YG_WriteSave(string receiver, string json);
#endif

        public YandexCloudSaveStorage(string receiverName)
        {
            _receiver = receiverName;
            Current = this;
        }

        public void Read(Action<string> onRead)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _pendingRead = onRead;
            YG_LoadSave(_receiver);
#else
            onRead(null);
#endif
        }

        public void Write(string text, Action<bool> onDone)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _pendingWrite = onDone;
            YG_WriteSave(_receiver, text);
#else
            onDone(false);
#endif
        }

        /// <summary>Ответ JS на чтение: текст сохранения, пустая строка (нет данных) или null (ошибка).</summary>
        public void OnLoaded(string text)
        {
            var cb = _pendingRead;
            _pendingRead = null;
            cb?.Invoke(string.IsNullOrEmpty(text) ? null : text);
        }

        public void OnWritten(bool ok)
        {
            var cb = _pendingWrite;
            _pendingWrite = null;
            cb?.Invoke(ok);
        }
    }
}
