using System.Text;
using Burmalda.Core;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Issue #157 (спайк дискретного ввода, ТОЛЬКО ветка spike/discrete-input-dwell,
    /// никогда не мержится в main) — текстовый HUD "примеривания": пока
    /// палец держит соседнюю плиту (<see cref="GridTraceInputController.DwellTarget"/>),
    /// показывает её распад/содержимое (триггер/валюта/рычаг), не двигая
    /// трейл — сама механика в <see cref="GridTraceInputController"/>.
    /// Во время фиксации шага (<see cref="GridTraceInputController.IsStepLocked"/>)
    /// показывает обратный отсчёт — наглядно, что ввод заблокирован.
    ///
    /// OnGUI, не Canvas — установленный в проекте минимальный паттерн для
    /// текстового debug-HUD (см. <see cref="RunFeedbackDebugUI"/>), не
    /// Canvas/.prefab.
    /// </summary>
    public sealed class DiscreteDwellHud : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(DiscreteDwellHud));
            host.AddComponent<DiscreteDwellHud>();
            DontDestroyOnLoad(host);
        }

        private GridTraceInputController _input;

        private void Update()
        {
            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
        }

        private void OnGUI()
        {
            if (_input == null || !_input.DwellEnabled) return;

            string message;
            if (_input.IsStepLocked)
            {
                message = $"ШАГ… {_input.StepLockRemainingSeconds:0.00}с";
            }
            else if (_input.DwellTarget.HasValue)
            {
                message = DescribeTile(_input.DwellTarget.Value);
            }
            else
            {
                return; // ничего не примеривается сейчас — не занимаем экран пустым боксом
            }

            var style = new GUIStyle(GUI.skin.box) { fontSize = 26, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            GUI.Box(new Rect(20, Screen.height - 220, Screen.width - 40, 160), message, style);
        }

        private string DescribeTile(GridCoordinate coordinate)
        {
            var sb = new StringBuilder();
            sb.Append($"({coordinate.Row}, {coordinate.Column})");

            if (!_input.Grid.TryGetTile(coordinate, out var tile))
            {
                sb.Append(" — пусто (ещё не тронута)");
                return sb.ToString();
            }

            if (tile.LethalTrap.HasValue) sb.Append($" — ЛОВУШКА {tile.LethalTrap.Value}");
            if (tile.IsTimedTrapActive) sb.Append($" — активна ловушка {tile.TimedTrapKind}");
            if (tile.IsBlocked) sb.Append(" — препятствие");
            if (tile.IsGated) sb.Append(tile.IsLeverGateOpen ? " — ворота (открыты)" : " — ворота (закрыты)");
            if (tile.IsLever) sb.Append(" — рычаг");
            if (tile.IsManaSource) sb.Append(" — мана");
            if (tile.IsKeySource) sb.Append(" — ключ");
            if (tile.IsAltar) sb.Append(" — алтарь");
            if (tile.DecayThresholdSeconds.HasValue) sb.Append($" — распад {tile.DecayProgress01 * 100f:0}%");

            return sb.ToString();
        }
    }
}
