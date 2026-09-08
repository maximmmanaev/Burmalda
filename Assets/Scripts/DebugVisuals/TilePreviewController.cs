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
    /// <b>Issue #163 (скрытые ловушки):</b> сводка о плите уважает
    /// <see cref="TrapInsight"/> — триггер одной из пяти ловушек показывает
    /// общую строку ("Триггер механизма") вместо точного типа, пока тип не
    /// раскрыт Идолом Чутья (по умолчанию выключен — базовая строка доступна
    /// без единого Идола, точный тип — нет). Владелец, 2026-09-05 «оставить
    /// только пять новых ловушек»: <c>Core.TrapSignature</c> и понятие
    /// "скрытая ловушка" (яма/активированный взрыв) удалены — сама ловушка
    /// (в отличие от её триггера) видна всегда, без гейта Идолом.
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

            // Владелец, 2026-09-05 «оставить только пять новых ловушек»:
            // понятие "скрытая ловушка" (яма/сработавший взрыв,
            // Core.TrapSignature) удалено вместе с Pit/Explosion — из пяти
            // оставшихся LethalTrapType ни один не бывает скрытым: Лава
            // видна всегда по замыслу, а ArrowWave/BombBlast/BladeTact/
            // LavaWave — активная угроза ПРЯМО СЕЙЧАС, тоже видна всегда
            // (issue #163 больше не применяется к самой ловушке — только к
            // её триггеру, см. ниже). Точный тип без Идола Чутья и раньше
            // не скрывался для Лавы — теперь так же для всех пяти.
            if (tile.LethalTrap.HasValue)
                lines.Add($"Смертельная ловушка: {tile.LethalTrap.Value}");

            // Задача «сделать тоннель играбельным», часть 3: базовая строка
            // ("здесь механизм") видна всегда, точный тип — только с Идолом
            // Чутья (тот же гейт, что TileArtKindResolver/TileDebugColor
            // применяют к цвету/арту через IsDangerSignatureRevealed).
            //
            // Рычаг (IsLever) сюда НЕ входит (владелец, 2026-09-04, отменяет
            // issue #193) — он не скрытый триггер ловушки, у него своя
            // безусловная строка ниже, без гейта Идолом Чутья.
            var isTrapTrigger = tile.ArrowWaveTargetRow.HasValue || tile.IsBombTrigger ||
                tile.BladeTactTargetRow.HasValue || tile.IsFallingRockTrigger || tile.IsLavaTrigger;
            if (isTrapTrigger)
            {
                lines.Add("Триггер механизма");
                if (TrapInsight.HasTrapTypeInsight)
                {
                    string exactType;
                    if (tile.ArrowWaveTargetRow.HasValue) exactType = "волна стрел";
                    else if (tile.IsBombTrigger) exactType = "бомба";
                    else if (tile.BladeTactTargetRow.HasValue) exactType = "такт лезвий";
                    else if (tile.IsFallingRockTrigger) exactType = "падающий камень";
                    else exactType = "волна лавы";
                    lines.Add($"(Идол Чутья) Точный тип: {exactType}");
                }
            }
            if (tile.IsLever) lines.Add("Рычаг");
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

            // Задача «разрушение плиты», продолжение (владелец, 2026-09-01):
            // сегментная генерация ТЕПЕРЬ намеренно совмещает ловушку и
            // валюту на одной плите (шаблоны «выкуп»/«последний-рывок» —
            // "триггер уничтожает собственную награду"). Сработавшая
            // ловушка — явная опасность, а не источник награды (владелец,
            // дословно): раз tile.LethalTrap выставлен, строка про Ману/Ключ
            // не показывается вовсе — Tile.IsManaSource/IsKeySource
            // намеренно не сбрасываются (см. их doc-комментарий), но здесь,
            // в презентации, приоритет у опасности. Владелец, 2026-09-05
            // «оставить только пять новых ловушек»: прежний гейт Идолом
            // Жадности (issue #163, "что ценного скрыто под плитой") был про
            // теперь-удалённую скрытую ловушку (яму) — валюта на плите без
            // LethalTrap и раньше была видна безусловно, гейт убран как
            // мёртвый.
            var hasLethalDanger = tile.LethalTrap.HasValue;
            AppendValueLine(lines, tile.IsManaSource && !hasLethalDanger, "Источник Кристаллов Маны");
            AppendValueLine(lines, tile.IsKeySource && !hasLethalDanger, "Источник Ключей");

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

        private static void AppendValueLine(List<string> lines, bool hasValue, string label)
        {
            if (!hasValue) return;
            lines.Add(label);
        }
    }
}
