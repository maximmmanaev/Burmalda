using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Кнопка ручного рестарта забега поверх экрана (не финальный UI — по
    /// аналогии с <see cref="TunnelDebugVisual"/>, debug-инструмент для
    /// ручного тестирования, не система из PRD). Через
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/> создаёт себя сама
    /// при старте игры — без ручного добавления компонента на сцену
    /// (docs/rules/forbidden-actions.md: не трогаем .unity/.prefab).
    /// Рисуется через <c>OnGUI</c> (immediate mode) — тоже без Canvas/Button
    /// в сцене. Вызывает уже существующий
    /// <see cref="GridTraceInputController.Restart"/>, который пересобирает
    /// мир/трейл с нуля; все остальные системы забега уже подписаны на
    /// его <see cref="GridTraceInputController.RunStarted"/> и пересоберут
    /// себя сами (см. паттерн, общий для всех Controller'ов проекта).
    /// </summary>
    public sealed class RestartButton : MonoBehaviour
    {
        private const float ButtonWidth = 220f;
        private const float ButtonHeight = 90f;
        private const float Margin = 24f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(RestartButton));
            host.AddComponent<RestartButton>();
            DontDestroyOnLoad(host);
        }

        private GridTraceInputController _input;
        private GUIStyle _style;

        private void Update()
        {
            // Ленивый поиск — как и другие Controller'ы в проекте, находит
            // GridTraceInputController независимо от порядка Awake между
            // разными компонентами сцены.
            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
        }

        private void OnGUI()
        {
            if (_input == null) return;

            _style ??= new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold };

            var rect = new Rect(Screen.width - ButtonWidth - Margin, Margin, ButtonWidth, ButtonHeight);
            if (GUI.Button(rect, "RESTART", _style))
                _input.Restart();
        }
    }
}
