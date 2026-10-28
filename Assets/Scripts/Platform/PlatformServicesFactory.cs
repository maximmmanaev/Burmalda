namespace Burmalda.Platform
{
    /// <summary>Выбирает реализацию: WebGL-плеер — Яндекс Игры, всё остальное (редактор, тесты, другие платформы) — пустая.</summary>
    public static class PlatformServicesFactory
    {
        public static IPlatformServices Create()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Select(true);
#else
            return Select(false);
#endif
        }

        /// <summary>Сам выбор вынесен отдельно, чтобы его можно было проверить в EditMode.</summary>
        public static IPlatformServices Select(bool isWebGlPlayer)
        {
            if (isWebGlPlayer) return new YandexPlatformServices(new YandexJsBridge());
            return new NullPlatformServices();
        }
    }
}
