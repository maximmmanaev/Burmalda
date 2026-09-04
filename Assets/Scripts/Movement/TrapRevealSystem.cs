using System;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Задача «раскрытие опасности при примеривании» — заменяет прежнее
    /// правило PRD v9 §4.2 ("сигнатура опасности видна всегда"): скрытая
    /// опасность (яма/сработавший взрыв — <see cref="TrapSignature.IsHiddenLethalTrap"/>
    /// — или триггер механизма, взрывной/с таймингом/рычаг — issue #193,
    /// владелец: "две разновидности механизма с разной видимостью научат
    /// игрока неверному правилу", рычаг больше не исключение) ничем не
    /// отличается от обычного пола, пока игрок хотя бы раз не навёл на
    /// плиту палец (<see cref="GridTraceInputController.PreviewTarget"/>). Раскрытие —
    /// разовое и постоянное: <see cref="Tile.RevealDangerSignature"/> не
    /// имеет обратного метода, "отвёл палец" не прячет сигнатуру обратно —
    /// иначе механика станет проверкой памяти вместо разведки (прямое
    /// требование задачи).
    ///
    /// <b>Не про точный тип</b> — тип по-прежнему только с
    /// <see cref="TrapInsight.HasTrapTypeInsight"/> (Идол Чутья); этот класс
    /// раскрывает только факт "здесь что-то не так", ту же общую сигнатуру,
    /// что рисуют <c>DebugVisuals.TileDebugColor</c>/<c>TileArtKindResolver</c>.
    ///
    /// Чистый С#-класс, тестируется без сцены — тикается тонким
    /// MonoBehaviour-driver'ом (<see cref="DebugVisuals.TrapRevealController"/>)
    /// с текущим <see cref="GridTraceInputController.PreviewTarget"/> на
    /// каждый кадр, тот же паттерн, что <c>Decay.TrailDecaySystem</c>/
    /// <c>Movement.TimedTrapSystem</c>. Не хранит ссылку на
    /// <see cref="GridTraceInputController"/> напрямую — только на
    /// <see cref="TunnelGrid"/>, координату примеривания передаёт вызывающая
    /// сторона в <see cref="Tick"/>.
    /// </summary>
    public sealed class TrapRevealSystem
    {
        private readonly TunnelGrid _grid;

        /// <summary>Срабатывает, когда сигнатура плиты РЕАЛЬНО раскрывается впервые — не на каждый кадр примеривания уже раскрытой плиты. Потребитель — обратная связь (звук/вибрация).</summary>
        public event Action<GridCoordinate> SignatureRevealed;

        public TrapRevealSystem(TunnelGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        /// <summary>
        /// Раскрывает сигнатуру примериваемой плиты, если у неё есть скрытая
        /// опасность и она ещё не раскрыта. Не-op для null/невалидной/уже
        /// раскрытой/безопасной на вид плиты — безопасно вызывать каждый
        /// кадр с текущим <see cref="GridTraceInputController.PreviewTarget"/>.
        /// </summary>
        public void Tick(GridCoordinate? previewTarget)
        {
            if (!previewTarget.HasValue) return;
            if (!_grid.TryGetTile(previewTarget.Value, out var tile)) return;
            if (tile.IsDangerSignatureRevealed) return;
            if (!HasHiddenDanger(tile)) return;

            tile.RevealDangerSignature();
            SignatureRevealed?.Invoke(previewTarget.Value);
        }

        // Ровно те же состояния, что делят одну сигнатуру в
        // DebugVisuals.TileDebugColor/TileArtKindResolver (HiddenTrapSignature/
        // TriggerSignature) — лава и активная ловушка с таймингом сюда не
        // входят намеренно (TrapSignature.IsHiddenLethalTrap их и так
        // исключает; ActiveTimedTrap — отдельная, всегда видимая ветка).
        //
        // Issue #193 (владелец, 2026-09-01): "две разновидности механизма с
        // разной видимостью научат игрока неверному правилу" — рычаг
        // (IsLever) теперь ТОЖЕ скрытый механизм, той же сигнатурой, что
        // взрывной/тайминговый триггер (не отдельная категория). Правило
        // одно: награды и преграды видны всегда, опасность и механизмы —
        // нет, тип различает только Идол Чутья.
        private static bool HasHiddenDanger(Tile tile) =>
            TrapSignature.IsHiddenLethalTrap(tile)
            || tile.ExplosiveTrapTarget.HasValue
            || tile.TimedTrapTarget.HasValue
            || tile.ArrowWaveTargetRow.HasValue // триггер Стрелы (issue #213) — тот же приём, что и прочие триггеры выше
            || tile.IsBombTrigger // триггер Бомбы (issue #214) — тот же приём
            || tile.BladeTactTargetRow.HasValue // триггер Лезвий (issue #215) — тот же приём
            || tile.IsLever;
    }
}
