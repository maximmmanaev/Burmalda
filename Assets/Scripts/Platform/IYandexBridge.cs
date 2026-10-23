namespace Burmalda.Platform
{
    /// <summary>Вызовы из C# в JS-обёртку SDK (<c>Assets/Plugins/WebGL/YandexGames.jslib</c>). Вынесено ради фейка в тестах.</summary>
    public interface IYandexBridge
    {
        void Init();
        void Ready();
        void GameplayStart();
        void GameplayStop();
    }
}
