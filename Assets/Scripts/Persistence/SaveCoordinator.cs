using System;

namespace Burmalda.Persistence
{
    /// <summary>
    /// Порядок работы с сохранением, без Unity: загрузка один раз, затем запись при изменениях.
    /// До конца загрузки ничего не пишется — иначе пустой прогресс затёр бы сохранённый (<c>Apply</c> аддитивный,
    /// его нельзя вызвать дважды). Запись не чаще <see cref="MinWriteIntervalSeconds"/> (лимит SDK на
    /// <c>player.setData</c> — 100 запросов за 5 минут), по одной в полёте; изменение, пришедшее во время
    /// паузы между записями, уходит следующим тиком. Сохранение более новой версии игры не перезаписывается.
    /// </summary>
    public sealed class SaveCoordinator
    {
        public const float MinWriteIntervalSeconds = 3f;
        public const float WriteTimeoutSeconds = 30f;

        private readonly ISaveStorage _storage;
        private readonly Func<SaveData> _capture;
        private readonly Action<SaveData> _apply;
        private readonly Func<long> _unixNow;

        private bool _dirty;
        private bool _inFlight;
        private int _writeId;
        private float _now;
        private float _lastSendAt = float.NegativeInfinity;

        public SaveCoordinator(ISaveStorage storage, Func<SaveData> capture, Action<SaveData> apply, Func<long> unixNow)
        {
            _storage = storage;
            _capture = capture;
            _apply = apply;
            _unixNow = unixNow;
        }

        public bool IsLoaded { get; private set; }
        public bool IsBlocked { get; private set; }
        public SaveReadStatus LoadStatus { get; private set; }

        public void Load(Action onLoaded = null)
        {
            _storage.Read(text =>
            {
                var result = SaveFormat.Deserialize(text);
                LoadStatus = result.Status;
                if (result.Status == SaveReadStatus.Ok) _apply(result.Data);
                IsBlocked = result.Status == SaveReadStatus.TooNew;
                IsLoaded = true;
                onLoaded?.Invoke();
            });
        }

        public void MarkDirty() => _dirty = true;

        public void Tick(float now)
        {
            _now = now;
            if (_inFlight && now - _lastSendAt > WriteTimeoutSeconds) _inFlight = false; // ответ не пришёл — не блокируем сохранение навсегда
            if (!CanWrite() || _inFlight) return;
            if (now - _lastSendAt < MinWriteIntervalSeconds) return;
            Send();
        }

        /// <summary>Срочная запись (сворачивание, уход со страницы): без паузы между записями.</summary>
        public void Flush()
        {
            if (!CanWrite()) return;
            Send();
        }

        private bool CanWrite() => IsLoaded && !IsBlocked && _dirty;

        private void Send()
        {
            _dirty = false;
            _inFlight = true;
            _lastSendAt = _now;
            var id = ++_writeId;
            _storage.Write(SaveFormat.Serialize(_capture(), _unixNow()), ok =>
            {
                if (id == _writeId) _inFlight = false;
                if (!ok) _dirty = true;
            });
        }
    }
}
