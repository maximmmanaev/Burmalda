using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Issue #190 (Спринт 12a, «Гигиена и композиционный корень»):
    /// <c>Application.targetFrameRate=60</c> уже настроен
    /// (<see cref="FrameRateSetup"/>), но живого измерения фактического
    /// FPS/времени кадра в игре не было — только конфигурация цели. Тонкий
    /// driver (тот же паттерн, что <see cref="TilePreviewController"/>) —
    /// тикает <see cref="FrameRateMonitor"/> каждый кадр
    /// <c>Time.unscaledDeltaTime</c> (не <c>Time.deltaTime</c> — измерение
    /// производительности не должно зависеть от <c>Time.timeScale</c>,
    /// даже если в проекте сейчас его никто не меняет) и рисует сводку
    /// через <c>OnGUI</c>, гейтится <see cref="RunHudToggles.ShowFrameRateOverlay"/>
    /// (дефолт ВЫКЛ, тумблер "FPS" в <see cref="RunHudTogglePanel"/>).
    ///
    /// Не самобутстрапится как игровая система — чисто отладочный
    /// компонент, тот же принцип, что у остальных здесь (<see cref="TrapDensityDebugPanel"/>
    /// и т.д.): включается на КАЖДОЙ сцене через
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/>, не участвует в
    /// сборке систем забега (<c>Bootstrap.RunBootstrap</c>) и ничего не
    /// трогает в .unity/.prefab.
    ///
    /// Правый нижний угол экрана — единственный, ещё не занятый ни одной
    /// debug-кнопкой/панелью (см. <see cref="DebugPanelLayout"/>: верхний
    /// левый — CAM, верхний правый — RESTART/ECO/TO CAMP, верхний центр —
    /// HUD, нижний левый — GEN).
    /// </summary>
    public sealed class FrameRateOverlay : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(FrameRateOverlay));
            host.AddComponent<FrameRateOverlay>();
            DontDestroyOnLoad(host);
        }

        private const float BoxWidth = 220f;
        private const float BoxHeight = 90f;
        private const float Margin = DebugPanelLayout.Margin;

        private readonly FrameRateMonitor _monitor = new FrameRateMonitor();

        private void Update() => _monitor.Tick(Time.unscaledDeltaTime);

        private void OnGUI()
        {
            if (!RunHudToggles.ShowFrameRateOverlay) return;

            var text = $"FPS: {Mathf.RoundToInt(_monitor.SmoothedFps)}\nКадр: {_monitor.LastFrameTimeSeconds * 1000f:0.0} мс";
            var style = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            var rect = new Rect(Screen.width - BoxWidth - Margin, Screen.height - BoxHeight - Margin, BoxWidth, BoxHeight);
            GUI.Box(rect, text, style);
        }
    }
}
