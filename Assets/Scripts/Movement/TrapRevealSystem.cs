using System;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Задача «раскрытие опасности при примеривании» — заменяет прежнее
    /// правило PRD v9 §4.2 ("сигнатура опасности видна всегда"): триггер
    /// одной из пяти ловушек (Стрелы/Бомбы/Лезвий/Падающего камня/Лавы)
    /// ничем не отличается от обычного пола, пока игрок хотя бы раз не
    /// навёл на плиту палец (<see cref="GridTraceInputController.PreviewTarget"/>).
    /// Владелец, 2026-09-05 «оставить только пять новых ловушек»: прежние
    /// Pit/Explosion и <c>Core.TrapSignature.IsHiddenLethalTrap</c> удалены —
    /// эта система больше не различает "скрытая опасность" от "триггер
    /// ловушки", это одна и та же категория.
    /// Раскрытие — разовое и постоянное: <see cref="Tile.RevealDangerSignature"/>
    /// не имеет обратного метода, "отвёл палец" не прячет сигнатуру обратно
    /// — иначе механика станет проверкой памяти вместо разведки (прямое
    /// требование задачи).
    ///
    /// <b>Рычаг (issue #193) БОЛЬШЕ НЕ входит сюда</b> — владелец пересмотрел
    /// решение (2026-09-04, задача «видимые рычаги, инвариант лавы, размер
    /// награды за Воротами»): «Рычаг — механизм, а не опасность» — видим
    /// ВСЕГДА, собственная текстура (см. <c>DebugVisuals.TileDebugColor.LeverColor</c>/
    /// <c>TileArtKindResolver</c>), не смешивается с сигнатурой опасности.
    /// Возврат к рычагу дорожает нелинейно сам по себе (плиты за спиной
    /// догорают) — накладывать поиск скрытого рычага поверх этого давало
    /// механику, которой либо не пользуются, либо она физически
    /// невозможна; с видимым рычагом решение «успею ли вернуться» игрок
    /// принимает ДО шага, а не после разведки.
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

        // Ровно то же состояние, что рисует единую TriggerSignature в
        // DebugVisuals.TileDebugColor/TileArtKindResolver — активная лава/
        // ловушка с таймингом (LethalTrapType) сюда не входит намеренно,
        // это отдельная, всегда видимая ветка (ActiveTimedTrap).
        //
        // IsLever намеренно НЕ здесь — владелец, 2026-09-04: рычаг видим
        // всегда, не опасность и не скрытый механизм (см. doc-комментарий
        // класса, отменяет решение issue #193).
        private static bool HasHiddenDanger(Tile tile) =>
            tile.ArrowWaveTargetRow.HasValue // триггер Стрелы (issue #213) — тот же приём, что и прочие триггеры ниже
            || tile.IsBombTrigger // триггер Бомбы (issue #214) — тот же приём
            || tile.BladeTactTargetRow.HasValue // триггер Лезвий (issue #215) — тот же приём
            || tile.IsFallingRockTrigger // триггер Падающего камня (issue #217) — тот же приём
            || tile.IsLavaTrigger; // триггер Лавы (issue #216) — тот же приём (сама волна лавы, в отличие от триггера, видна всегда, как статичная Лава)
    }
}
