using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Issue #157 (спайк дискретного ввода, ТОЛЬКО ветка spike/discrete-input-dwell,
    /// никогда не мержится в main) — дебаг-панель для
    /// <see cref="GridTraceInputController.DwellEnabled"/>/<see cref="GridTraceInputController.StepDurationSeconds"/>,
    /// как просит issue #157 ("длительность шага, тумблер dwell on/off").
    ///
    /// <c>OnGUI</c>, без Canvas/.prefab — установленный в проекте паттерн
    /// для debug-инструментов (см. <see cref="RestartButton"/>,
    /// <see cref="RunFeedbackDebugUI"/>), самобутстрапится через
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/>.
    /// </summary>
    public sealed class DiscreteInputDebugPanel : MonoBehaviour
    {
        private const float PanelWidth = 420f;
        private const float PanelHeight = 170f;
        private const float Margin = 24f;

        private const float MinStepDurationSeconds = 0.25f;
        private const float MaxStepDurationSeconds = 0.4f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(DiscreteInputDebugPanel));
            host.AddComponent<DiscreteInputDebugPanel>();
            DontDestroyOnLoad(host);
        }

        private GridTraceInputController _input;
        private GUIStyle _toggleStyle;
        private GUIStyle _labelStyle;

        private void Update()
        {
            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
        }

        private void OnGUI()
        {
            if (_input == null) return;

            _toggleStyle ??= new GUIStyle(GUI.skin.toggle) { fontSize = 24 };
            _labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 22 };

            var rect = new Rect(Margin, Margin, PanelWidth, PanelHeight);
            GUI.Box(rect, GUIContent.none);

            GUILayout.BeginArea(new Rect(rect.x + 16, rect.y + 12, rect.width - 32, rect.height - 24));
            _input.DwellEnabled = GUILayout.Toggle(_input.DwellEnabled, " Dwell (примеривание)", _toggleStyle);

            GUILayout.Space(8);
            GUILayout.Label($"Step duration: {_input.StepDurationSeconds * 1000f:0}ms", _labelStyle);
            _input.StepDurationSeconds = GUILayout.HorizontalSlider(_input.StepDurationSeconds, MinStepDurationSeconds, MaxStepDurationSeconds);
            GUILayout.EndArea();
        }
    }
}
