using Burmalda.Bootstrap;
using Burmalda.Movement;
using Burmalda.RunLifecycle;
using UnityEngine;

namespace Burmalda.Platform
{
    /// <summary>
    /// Точка входа платформенного слоя: создаётся из кода (сцену не правим), поднимает сервисы, подписывает паузу,
    /// после готовности SDK шлёт «ready», размечает геймплей по забегу и принимает вызовы из JS (SendMessage).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlatformHost : MonoBehaviour
    {
        /// <summary>Имя объекта: JS шлёт ему SendMessage.</summary>
        public const string ReceiverName = "YandexPlatformReceiver";

        public static IPlatformServices Services { get; private set; }

        private PlatformPauseController _pause;
        private GameplayMarks _marks;
        private GridTraceInputController _input;
        private RunController _run;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<PlatformHost>() != null) return;

            var host = new GameObject(ReceiverName);
            host.AddComponent<PlatformHost>();
            DontDestroyOnLoad(host);
        }

        private void Awake()
        {
            Services = PlatformServicesFactory.Create();
            _pause = new PlatformPauseController(Services);
            _marks = new GameplayMarks(Services);
            Services.Initialized += OnInitialized;
            Services.Initialize();
        }

        private void OnDestroy()
        {
            Disconnect();
            if (Services != null) Services.Initialized -= OnInitialized;
            _pause?.Dispose();
            Services = null;
        }

        private void OnInitialized()
        {
            Debug.Log($"[Platform] SDK готов, язык: {Services.Language ?? "не определён"}");
            Services.SignalReady();
        }

        private void Update()
        {
            if (_input != null) return;
            var bootstrap = RunBootstrap.Instance;
            if (bootstrap == null || bootstrap.Input == null || bootstrap.Run == null) return;

            _input = bootstrap.Input;
            _run = bootstrap.Run;
            _input.RunStarted += _marks.RunStarted;
            _run.RunDied += OnRunDied;
            // первый RunStarted случился раньше подписки
            _marks.RunStarted();
        }

        private void Disconnect()
        {
            if (_input != null) _input.RunStarted -= _marks.RunStarted;
            if (_run != null) _run.RunDied -= OnRunDied;
            _input = null;
            _run = null;
        }

        private void OnRunDied(string reason) => _marks.RunEnded();

        // Вызываются из .jslib через SendMessage (аргумент — всегда строка).
        private void OnSdkInitialized(string language) => (Services as YandexPlatformServices)?.OnSdkInitialized(language);
        private void OnSdkFailed(string error)
        {
            Debug.LogWarning($"[Platform] SDK не поднялся: {error}");
            (Services as YandexPlatformServices)?.OnSdkFailed();
        }
        private void OnPause(string unused) => (Services as YandexPlatformServices)?.OnPause();
        private void OnResume(string unused) => (Services as YandexPlatformServices)?.OnResume();
    }
}
