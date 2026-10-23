using System;

namespace Burmalda.Platform
{
    /// <summary>
    /// Яндекс Игры: логика очередности вызовов поверх <see cref="IYandexBridge"/>. До готовности SDK
    /// «ready» и «start» запоминаются и уходят после <see cref="OnSdkInitialized"/> (сначала ready, потом start);
    /// если SDK не поднялся (<see cref="OnSdkFailed"/>), вызовы молча пропускаются, игра продолжается.
    /// Паузу платформа сама размечает через GameplayAPI.stop/start (документация, game_api_pause/resume),
    /// поэтому здесь события Paused/Resumed только пробрасываются.
    /// </summary>
    public sealed class YandexPlatformServices : IPlatformServices
    {
        private readonly IYandexBridge _bridge;
        private bool _initRequested;
        private bool _failed;
        private bool _readyWanted;
        private bool _readySent;
        private bool _gameplayActive;

        public YandexPlatformServices(IYandexBridge bridge)
        {
            _bridge = bridge;
        }

        public bool IsInitialized { get; private set; }
        public string Language { get; private set; }

        public event Action Initialized;
        public event Action Paused;
        public event Action Resumed;

        public void Initialize()
        {
            if (_initRequested) return;
            _initRequested = true;
            _bridge.Init();
        }

        public void SignalReady()
        {
            _readyWanted = true;
            FlushReady();
        }

        public void GameplayStart()
        {
            if (_gameplayActive) return;
            _gameplayActive = true;
            if (IsInitialized) _bridge.GameplayStart();
        }

        public void GameplayStop()
        {
            if (!_gameplayActive) return;
            _gameplayActive = false;
            if (IsInitialized) _bridge.GameplayStop();
        }

        // Ниже — входы из JS (через PlatformHost).

        public void OnSdkInitialized(string language)
        {
            if (IsInitialized || _failed) return;
            IsInitialized = true;
            Language = string.IsNullOrEmpty(language) ? null : language;
            FlushReady();
            if (_gameplayActive) _bridge.GameplayStart();
            Initialized?.Invoke();
        }

        public void OnSdkFailed() => _failed = true;

        public void OnPause() => Paused?.Invoke();

        public void OnResume() => Resumed?.Invoke();

        private void FlushReady()
        {
            if (!IsInitialized || !_readyWanted || _readySent) return;
            _readySent = true;
            _bridge.Ready();
        }
    }
}
