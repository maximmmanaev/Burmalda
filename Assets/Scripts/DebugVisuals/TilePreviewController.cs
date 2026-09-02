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
    ///
    /// <b>Задача 2 (отладочный HUD):</b> текстовая сводка (координаты/%
    /// распада/тип ловушки) — единственный отладочный вывод в игре с точным
    /// % распада и (при раскрытом Идолом) точным типом ловушки. Раньше
    /// показывалась всегда, теперь — только при
    /// <see cref="RunHudToggles.ShowTileDebugOverlay"/> (дефолт ВЫКЛ, тумблер
    /// в <see cref="RunHudTogglePanel"/>) — по прямому требованию задачи:
    /// этот уровень детализации не должен утекать в обычный (не отладочный)
    /// HUD. Подсветка контура (<see cref="_highlighter"/>) тумблером НЕ
    /// гейтится — это часть механики примеривания (PRD 4.1 п.1), не
    /// диагностика.
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
            if (!RunHudToggles.ShowTileDebugOverlay) return;
            // ExamineTarget, не PreviewTarget (задача «раскрытие опасности
            // при примеривании») — смертельная ловушка всегда даёт
            // CanAdvanceTo=false, значит PreviewTarget для нее никогда не
            // установится; сводка о плите — диагностика, должна показывать
            // и то, на что нельзя шагнуть, не только то, на что можно.
            if (_input == null || _input.Grid == null || !_input.ExamineTarget.HasValue) return;

            var text = BuildTileSummary(_input.ExamineTarget.Value);
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

            // Issue #163: скрытая ловушка (яма) — базовая сигнатура ВСЕГДА
            // (без единого Идола, см. TrapInsight), точный тип только с
            // Идолом Чутья. Лава — вне этого правила, видна как раньше.
            //
            // Задача «разрушение плиты», продолжение (владелец, 2026-09-01):
            // сработавший взрыв — БОЛЬШЕ НЕ скрытая ловушка (см.
            // Core.TrapSignature), но и не "видна как Лава" безусловно с
            // точным типом — тот же принцип, что IsTimedTrapActive ниже
            // (активная угроза прямо сейчас, ОБЩЕЕ сообщение, не точный тип
            // без Идола Чутья) — иначе резолверы (реюз TimedTrapActive,
            // никакого точного типа без раскрытия) и эта текстовая сводка
            // разошлись бы в том, что считается "точным типом".
            var isHiddenTrap = TrapSignature.IsHiddenLethalTrap(tile);
            if (tile.LethalTrap == LethalTrapType.Explosion)
            {
                lines.Add("Ловушка сработала — опасно ПРЯМО СЕЙЧАС");
                if (TrapInsight.HasTrapTypeInsight) lines.Add("(Идол Чутья) Точный тип: взрывная ловушка");
            }
            else if (tile.LethalTrap.HasValue && !isHiddenTrap)
                lines.Add($"Смертельная ловушка: {tile.LethalTrap.Value}"); // Lava
            else if (isHiddenTrap)
            {
                lines.Add("Сигнатура опасности: с этой плитой что-то не так");
                if (TrapInsight.HasTrapTypeInsight) lines.Add($"(Идол Чутья) Точный тип: {tile.LethalTrap.Value}");
            }

            // Задача «сделать тоннель играбельным», часть 3: раньше эти две
            // строки выдавали точный тип триггера безусловно (тем самым
            // TileDebugColor/TileArtKindResolver гейтили цвет/текстуру
            // единой сигнатурой, а эта строка — нет, несогласованность).
            // Теперь тот же гейт, что и у скрытой ловушки ниже — базовая
            // строка ("здесь механизм") видна всегда, точный тип только с
            // Идолом Чутья.
            //
            // Issue #193 (владелец, 2026-09-01): рычаг (IsLever) — ТОЖЕ
            // механизм, та же общая строка/гейт, что у триггеров ловушек —
            // "две разновидности механизма с разной видимостью научат
            // игрока неверному правилу", отдельной ветки для рычага больше
            // нет.
            if (tile.ExplosiveTrapTarget.HasValue || tile.TimedTrapTarget.HasValue || tile.IsLever)
            {
                lines.Add("Триггер механизма");
                if (TrapInsight.HasTrapTypeInsight)
                {
                    string exactType;
                    if (tile.IsLever) exactType = "рычаг";
                    else if (tile.ExplosiveTrapTarget.HasValue) exactType = "взрывная ловушка";
                    else exactType = tile.TimedTrapKind.ToString();
                    lines.Add($"(Идол Чутья) Точный тип: {exactType}");
                }
            }
            if (tile.IsTimedTrapActive) lines.Add("Ловушка с таймингом СЕЙЧАС активна"); // реальная угроза прямо сейчас — не скрывается, см. TrapSignature
            // Ворота — преграда, видна ВСЕГДА (issue #193: "награды и
            // преграды видны всегда, опасность и механизмы — нет"), в
            // отличие от рычага выше.
            if (tile.IsGated)
            {
                lines.Add(tile.IsLeverGateOpen ? "Ворота (открыты)" : "Ворота (закрыты)");
                // Issue #193: "закрытые Ворота указывают направление на
                // свою открывашку" — без подсказки поиск скрытого рычага
                // был бы наугад, а каждое примеривание стоит игроку
                // распада. Координата видна в текстовой сводке безусловно
                // (обвод/визуальная стрелка на самой плите — TunnelDebugVisual);
                // сам факт "рычаг существует" не тайна (Ворота уже видны),
                // тайна — только ГДЕ он и КАКОЙ он ТОЧНО.
                if (!tile.IsLeverGateOpen && tile.LeverCoordinate.HasValue)
                    lines.Add($"Открывашка: {tile.LeverCoordinate.Value}");
            }

            // Issue #163, Идол Жадности: "что ценного скрыто под плитой" — на
            // скрытой ловушке ценность видна, только если она раскрыта
            // Идолом; на обычной (не скрытой) плите валюта была видна и
            // остаётся видна всегда, это не предмет issue #163.
            //
            // Задача «разрушение плиты», продолжение (владелец, 2026-09-01):
            // сегментная генерация ТЕПЕРЬ намеренно совмещает ловушку и
            // валюту на одной плите (шаблоны «выкуп»/«последний-рывок» —
            // "триггер уничтожает собственную награду"). Сработавшая
            // ловушка — явная опасность, а не источник награды (владелец,
            // дословно): раз tile.LethalTrap выставлен (взрыв сработал),
            // строка про Ману/Ключ не показывается вовсе — Tile.IsManaSource/
            // IsKeySource намеренно не сбрасываются (см. их doc-комментарий),
            // но здесь, в презентации, приоритет у опасности.
            var hasLethalDanger = tile.LethalTrap.HasValue;
            AppendValueLine(lines, tile.IsManaSource && !hasLethalDanger, "Источник Кристаллов Маны", isHiddenTrap);
            AppendValueLine(lines, tile.IsKeySource && !hasLethalDanger, "Источник Ключей", isHiddenTrap);

            if (tile.IsAltar) lines.Add("Алтарь");
            if (tile.IsBoss) lines.Add("Точка Босса");
            if (tile.BossRoomTile.HasValue) lines.Add($"Комната Босса: {DescribeBossRoomTile(tile.BossRoomTile.Value)}");

            return string.Join("\n", lines);
        }

        // Задача «Комната Босса» (владелец, 2026-09-02): человекочитаемое
        // описание для отладочного примеривания — содержимое Комнаты видно
        // всегда (награда, не опасность), нет смысла прятать точный тип за
        // сигнатурой, как для ловушек/рычага.
        private static string DescribeBossRoomTile(BossRoomTileKind kind)
        {
            switch (kind)
            {
                case BossRoomTileKind.Vein: return "Жила (+Мана)";
                case BossRoomTileKind.Resonance: return "Резонанс (+0.5 к множителю)";
                case BossRoomTileKind.Echo: return "Эхо (×2 к множителю)";
                case BossRoomTileKind.Rift: return "Разлом";
                default: return kind.ToString();
            }
        }

        private static void AppendValueLine(List<string> lines, bool hasValue, string label, bool isHiddenTrap)
        {
            if (!hasValue) return;
            if (isHiddenTrap && !TrapInsight.HasTrapContentsInsight) return; // скрыто, пока не раскрыто Идолом Жадности

            lines.Add(isHiddenTrap ? $"{label} (Идол Жадности)" : label);
        }
    }
}
