using System;

namespace Burmalda.Platform
{
    /// <summary>Пустая реализация для редактора, тестов и платформ без SDK: ничего не делает.</summary>
    public sealed class NullPlatformServices : IPlatformServices
    {
        public bool IsInitialized { get; private set; }
        public string Language => null;

        public event Action Initialized;
#pragma warning disable CS0067 // у пустой реализации паузы не бывает
        public event Action Paused;
        public event Action Resumed;
#pragma warning restore CS0067

        public void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;
            Initialized?.Invoke();
        }

        public void SignalReady() { }
        public void GameplayStart() { }
        public void GameplayStop() { }
    }
}
