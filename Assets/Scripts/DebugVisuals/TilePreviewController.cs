using System.Collections.Generic;
using Burmalda.Core;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Issue #157 (перенос схемы A из спайка в продукт, PRD 4.1 п.1):
    /// "плита обводится контуром, показывается доступная о ней информация".
    /// Тонкий driver (тот же паттерн, что <see cref="TunnelDebugVisualController"/>/
    /// <c>Decay.TrailDecayController</c>) — тикает
    /// <see cref="TilePreviewHighlighter"/> по <see cref="GridTraceInputController.PreviewTarget"/>
    /// и рисует текстовую сводку о примериваемой плите через <c>OnGUI</c>
    /// (тот же уровень исполнения, что <see cref="RunFeedbackDebugUI"/> —
    /// не финальный арт, но уже ПРОДУКТОВАЯ функциональность, не спайк:
    /// показывается всегда, без тумблера).
    ///
    /// Самобутстрапится через <see cref="RuntimeInitializeOnLoadMethodAttribute"/> —
    /// не трогает .unity/.prefab (docs/rules/forbidden-actions.md), как и
    /// остальные Controller'ы этого уровня в проекте.
    ///
    /// <b>Issue #163 (скрытые ловушки):</b> сводка о плите теперь уважает
    /// <see cref="Core.TrapSignature"/>/<see cref="TrapInsight"/> — скрытая
    /// ловушка (яма/активированный взрыв) показывает общую сигнатуру
    /// вместо точного типа, пока не раскрыта соответствующим крючком под
    /// Идола (по умолчанию оба крючка выключены — базовая сигнатура
    /// доступна без единого Идола, точный тип/содержимое — нет).
    /// </summary>
    public sealed class TilePreviewController : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(TilePreviewController));
            host.AddComponent<TilePreviewController>();
            DontDestroyOnLoad(host);
        }

        private static readonly Color HighlightColor = new Color(1f, 0.85f, 0.15f);

        private GridTraceInputController _input;
        private TilePreviewHighlighter _highlighter;

        private void Update()
        {
            // Ленивый поиск/создание — тот же паттерн, что и у остальных
            // debug-driver'ов проекта (порядок Awake между объектами сцены
            // не гарантирован).
            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
            if (_input == null) return;

            if (_highlighter == null) _highlighter = new TilePreviewHighlighter(transform, HighlightColor);

            if (_input.PreviewTarget.HasValue) _highlighter.Show(_input.PreviewTarget.Value, _input.Projection);
            else _highlighter.Hide();
        }

        private void OnDestroy() => _highlighter?.Dispose();

        private void OnGUI()
        {
            if (_input == null || _input.Grid == null || !_input.PreviewTarget.HasValue) return;

            var text = BuildTileSummary(_input.PreviewTarget.Value);
            var style = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.UpperLeft, wordWrap = true };
            GUI.Box(new Rect(20f, Screen.height - 200f, 420f, 180f), text, style);
        }

        /// <summary>Сводка о плите СТРОГО из существующих полей <see cref="Tile"/> — распад/содержимое (PRD 4.1 п.1: "доступная информация").</summary>
        private string BuildTileSummary(GridCoordinate coordinate)
        {
            if (!_input.Grid.TryGetTile(coordinate, out var tile))
                return $"{coordinate}\nнераскрытая плита";

            var lines = new List<string> { coordinate.ToString(), $"Распад: {Mathf.RoundToInt(tile.DecayProgress01 * 100f)}%" };

            if (tile.IsBlocked) lines.Add("Препятствие (заблокирована)");

            // Issue #163: скрытые ловушки (яма/активированный взрыв) — базовая
            // сигнатура ВСЕГДА (без единого Идола, см. TrapInsight), точный
            // тип только с Идолом Чутья. Лава — вне этого правила, видна как раньше.
            var isHiddenTrap = TrapSignature.IsHiddenLethalTrap(tile);
            if (tile.LethalTrap.HasValue && !isHiddenTrap)
                lines.Add($"Смертельная ловушка: {tile.LethalTrap.Value}"); // Lava
            else if (isHiddenTrap)
            {
                lines.Add("Сигнатура опасности: с этой плитой что-то не так");
                if (TrapInsight.HasTrapTypeInsight) lines.Add($"(Идол Чутья) Точный тип: {tile.LethalTrap.Value}");
            }

            if (tile.ExplosiveTrapTarget.HasValue) lines.Add("Триггер взрывной ловушки");
            if (tile.TimedTrapTarget.HasValue) lines.Add($"Триггер ловушки с таймингом: {tile.TimedTrapKind}");
            if (tile.IsTimedTrapActive) lines.Add("Ловушка с таймингом СЕЙЧАС активна"); // реальная угроза прямо сейчас — не скрывается, см. TrapSignature
            if (tile.IsLever) lines.Add("Рычаг");
            if (tile.IsGated) lines.Add(tile.IsLeverGateOpen ? "Ворота (открыты)" : "Ворота (закрыты)");

            // Issue #163, Идол Жадности: "что ценного скрыто под плитой" — на
            // скрытой ловушке ценность видна, только если она раскрыта
            // Идолом; на обычной (не скрытой) плите валюта была видна и
            // остаётся видна всегда, это не предмет issue #163. Сейчас
            // сегментная генерация не совмещает ловушку и валюту на одной
            // плите — веткой ниже владелец продукта воспользуется, когда
            // такой контент появится.
            AppendValueLine(lines, tile.IsManaSource, "Источник Кристаллов Маны", isHiddenTrap);
            AppendValueLine(lines, tile.IsKeySource, "Источник Ключей", isHiddenTrap);

            if (tile.IsAltar) lines.Add("Алтарь");
            if (tile.IsBoss) lines.Add("Точка Босса");

            return string.Join("\n", lines);
        }

        private static void AppendValueLine(List<string> lines, bool hasValue, string label, bool isHiddenTrap)
        {
            if (!hasValue) return;
            if (isHiddenTrap && !TrapInsight.HasTrapContentsInsight) return; // скрыто, пока не раскрыто Идолом Жадности

            lines.Add(isHiddenTrap ? $"{label} (Идол Жадности)" : label);
        }
    }
}
