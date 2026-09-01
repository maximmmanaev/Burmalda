using Burmalda.Core;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Маппинг <see cref="TileVisualState"/> → <see cref="TileArtKind"/> для
    /// <see cref="TunnelDebugVisual"/> — ТОЧНО те же ветки и тот же порядок
    /// приоритета, что и <see cref="TileDebugColor.Resolve"/> (первая
    /// генерация арта заменяет цвет текстурой, не меняет логику "что
    /// показывать когда", см. задачу "Завести арт в игру"). Состояния, для
    /// которых в пакете нет готового арта (текущая позиция, вход в Комнату
    /// Босса), возвращают <see cref="TileArtKind.None"/> — вызывающий код
    /// решает, чем их закрыть (см. doc-комментарий <see cref="TileArtKind"/>).
    ///
    /// Чистая функция — как и <see cref="TileDebugColor.Resolve"/>,
    /// тестируется без сцены и без Unity Texture API.
    /// </summary>
    public static class TileArtKindResolver
    {
        // Задача «двойные флаги на плитах» — тот же порядок и то же
        // обоснование, что TileDebugColor.Resolve (см. её doc-комментарий):
        // поведение (Destroyed/Blocked/Lava/LethalTrap/TimedTrapActive/
        // триггер) проверяется раньше ролевых/декоративных
        // (Boss/Altar/Gated/Lever/источники) — вторая линия обороны на
        // случай коллизии ролей, раньше порядок был обратным.
        public static TileArtKind Resolve(TileVisualState state)
        {
            if (state.IsDestroyed) return TileArtKind.Destroyed;
            // Задача «тёплый набор плит»: раньше None (белый цветной
            // фолбэк, самый невнятный объект на экране по плейтесту
            // владельца) — теперь tile-current-position.png.
            if (state.IsCurrentPosition) return TileArtKind.CurrentPosition;
            if (state.IsStart) return TileArtKind.Start;
            if (state.IsBlocked) return TileArtKind.Blocked;
            if (state.LethalTrap == LethalTrapType.Lava) return TileArtKind.Lava;
            // Задача «раскрытие опасности при примеривании» (PRD v9 §4.2
            // заменяется): яма и активированный взрыв — ОДНА категория, не
            // выдающая точный тип (см. Core.TrapSignature) — общий
            // tile-hidden-trap-signature.png, но только ПОСЛЕ того, как
            // игрок навёл на плиту (state.IsDangerSignatureRevealed, см.
            // Movement.TrapRevealSystem). Пока не раскрыта — плита
            // неотличима от обычного пола, ветка ничего не возвращает и
            // проваливается ниже, до градиента распада в конце.
            if ((state.LethalTrap == LethalTrapType.Pit || state.LethalTrap == LethalTrapType.Explosion) && state.IsDangerSignatureRevealed)
                return TileArtKind.HiddenTrapSignature;
            if (state.ActiveTimedTrap.HasValue) return TileArtKind.TimedTrapActive;
            // Единая сигнатура триггера (задача «сделать тоннель играбельным»,
            // часть 3) — тот же принцип, что HiddenTrapSignature выше:
            // отдельный TileArtKind, но задача «тёплый набор плит»
            // (владелец, прямое указание) отдаёт под него ТУ ЖЕ САМУЮ
            // текстуру, что и под HiddenTrapSignature (см. TileArtCatalog.Get)
            // — PRD v9 §4.2, ловушку от механизма не отличить без Идола Чутья.
            // Та же гейт-логика раскрытия, что и у ямы/взрыва выше.
            if ((state.IsExplosiveTrapTrigger || state.IsTimedTrapTrigger) && state.IsDangerSignatureRevealed)
                return TileArtKind.TriggerSignature;

            // Комнаты Босса ещё нет в Unity (PRD v8) — под неё в тёплом
            // наборе по-прежнему нет текстуры, остаётся на цветном фолбэке.
            if (state.IsBoss) return TileArtKind.None;
            if (state.IsAltar) return TileArtKind.Altar;
            // Задача «тёплый набор плит»: прежние tile-gate-closed.png/
            // tile-gate-open.png (лист мелких UI-иконок и полный рендер
            // комнаты — не тайлы пола) заменены настоящими плитами пола из
            // тёплого набора; tile-lever.png — тоже новое. Момент открытия
            // ворот обязан читаться мгновенно (задача) — раздельные
            // GateClosed/GateOpen, не одна текстура на оба состояния.
            if (state.IsGated) return state.IsLeverGateOpen ? TileArtKind.GateOpen : TileArtKind.GateClosed;
            if (state.IsLever) return TileArtKind.Lever;

            // Источники валюты (часть 1 задачи «сделать тоннель играбельным»):
            // раньше None (в пакете были только иконки Icons/Currencies/, не
            // тайлы пола) — задача «тёплый набор плит» добавила
            // tile-mana-source.png/tile-key-source.png.
            if (state.IsManaSource) return TileArtKind.ManaSource;
            if (state.IsKeySource) return TileArtKind.KeySource;

            return ResolveDecayGradient(state.DecayProgress01);
        }

        // Задача «разрушение плиты» (владелец, 2026-09-01): "Ответ —
        // анимация. Больше статичных стадий — тупиковый путь." Раньше здесь
        // делили 0..1 на трети (Fresh/HalfDecayed/AboutToDecay) — три
        // дискретных текстуры не дают игроку прицелиться в окно Отклика «На
        // волоске» (PRD §19/28.4), потому что 70% и 95% распада читались
        // одинаково. Теперь ВСЕГДА Fresh — прогресс распада передаётся
        // непрерывным оверлеем трещин поверх текстуры
        // (см. TunnelDebugVisual.ApplyVisual/UpdateCrackOverlay), не сменой
        // TileArtKind. tile-half-decayed.png/tile-about-to-decay.png не
        // удалены — остались на диске как ИСХОДНИКИ для маски трещин
        // (tile-crack-mask.png, TileArtCatalog.CrackMaskTexture), но больше
        // не самостоятельные состояния этого enum. Обоснование записано в
        // docs/wiki/roadmap.md ("Почему анимация распада, а не больше
        // стадий текстуры"), чтобы к вопросу не возвращались.
        private static TileArtKind ResolveDecayGradient(float progress01) => TileArtKind.Fresh;
    }
}
