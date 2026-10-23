using System.Runtime.InteropServices;

namespace Burmalda.Platform
{
    /// <summary>
    /// Реальный мост в JS. Вне WebGL-плеера (редактор, другие платформы) вызовы пустые —
    /// фабрика сюда в этих случаях и не попадает, защита от прямого создания.
    /// </summary>
    public sealed class YandexJsBridge : IYandexBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void YG_Init(string receiverName);
        [DllImport("__Internal")] private static extern void YG_Ready();
        [DllImport("__Internal")] private static extern void YG_GameplayStart();
        [DllImport("__Internal")] private static extern void YG_GameplayStop();

        public void Init() => YG_Init(PlatformHost.ReceiverName);
        public void Ready() => YG_Ready();
        public void GameplayStart() => YG_GameplayStart();
        public void GameplayStop() => YG_GameplayStop();
#else
        public void Init() { }
        public void Ready() { }
        public void GameplayStart() { }
        public void GameplayStop() { }
#endif
    }
}
