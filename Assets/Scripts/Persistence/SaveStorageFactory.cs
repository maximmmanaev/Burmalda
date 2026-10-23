namespace Burmalda.Persistence
{
    /// <summary>WebGL-плеер — облако Яндекса как основное и локальное как запасное; иначе только локальное.</summary>
    public static class SaveStorageFactory
    {
        public static ISaveStorage Create(string receiverName)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Select(true, receiverName);
#else
            return Select(false, receiverName);
#endif
        }

        public static ISaveStorage Select(bool isWebGlPlayer, string receiverName)
        {
            var local = new LocalSaveStorage();
            return isWebGlPlayer ? new CompositeSaveStorage(new YandexCloudSaveStorage(receiverName), local) : local;
        }
    }
}
