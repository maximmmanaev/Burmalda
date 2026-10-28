namespace Burmalda.Platform
{
    /// <summary>Разметка геймплея для платформы: start на старте забега, stop на его конце, повторы игнорируются.</summary>
    public sealed class GameplayMarks
    {
        private readonly IPlatformServices _platform;
        private bool _active;

        public GameplayMarks(IPlatformServices platform)
        {
            _platform = platform;
        }

        public void RunStarted()
        {
            if (_active) return;
            _active = true;
            _platform.GameplayStart();
        }

        public void RunEnded()
        {
            if (!_active) return;
            _active = false;
            _platform.GameplayStop();
        }
    }
}
