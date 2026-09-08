using Burmalda.Core;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Маппинг <see cref="TileVisualState"/> → <see cref="TileArtKind"/> для
    /// <see cref="TunnelDebugVisual"/> — ТОЧНО те же ветки и тот же порядок
    /// приоритета, что и <see cref="TileDebugColor.Resolve"/> (первая
    /// генерация арта заменяет цвет текстурой, не меняет логику "что
    /// показывать когда", см. задачу "Завести арт в игру"). Состояния, для
    /// которых в пакете нет готового арта (сейчас — только содержимое самой
    /// Комнаты Босса, Жила/Резонанс/Эхо), возвращают <see cref="TileArtKind.None"/>
    /// — вызывающий код решает, чем их закрыть (см. doc-комментарий
    /// <see cref="TileArtKind"/>).
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
            // Баг с устройства (владелец, 2026-09-05, «стрела остаётся
            // смертельной навсегда») — см. подробный doc-комментарий той же
            // ветки в TileDebugColor.Resolve: настоящая причина — у четырёх
            // LethalTrapType Спринта 13a (ArrowWave/BombBlast/BladeTact/
            // LavaWave) не было ветки здесь вовсе, армированная плита
            // выглядела обычным полом. Угроза ПРЯМО СЕЙЧАС — видна ВСЕГДА,
            // без гейта примеривания.
            if (state.LethalTrap == LethalTrapType.ArrowWave || state.LethalTrap == LethalTrapType.BombBlast ||
                state.LethalTrap == LethalTrapType.BladeTact || state.LethalTrap == LethalTrapType.LavaWave)
                return TileArtKind.TimedTrapActive;
            // Задача «раскрытие опасности при примеривании» (PRD v9 §4.2):
            // триггер одной из пяти ловушек не отличим от обычного пола,
            // пока игрок хотя бы раз не навёл на плиту
            // (state.IsDangerSignatureRevealed, см. Movement.TrapRevealSystem).
            // Пока не раскрыт — ветка ничего не возвращает и проваливается
            // ниже, до градиента распада в конце.
            if (state.IsTrapTrigger && state.IsDangerSignatureRevealed)
                return TileArtKind.TriggerSignature;

            // Баг с устройства (владелец, 2026-09-04): вход в Комнату — уже
            // TileArtKind.Boss (см. её doc-комментарий), структурная плита
            // маршрута обязана читаться. Содержимое САМОЙ Комнаты
            // (Жила/Резонанс/Эхо) по прямому указанию задачи остаётся на
            // цветном фолбэке — Комната целиком получит свой визуальный
            // режим в Спринте 20, чинить его по частям сейчас не просили.
            if (state.IsBoss) return TileArtKind.Boss;
            if (state.BossRoomTile.HasValue) return TileArtKind.None;
            if (state.IsAltar) return TileArtKind.Altar;
            // Задача «тёплый набор плит»: прежние tile-gate-closed.png/
            // tile-gate-open.png (лист мелких UI-иконок и полный рендер
            // комнаты — не тайлы пола) заменены настоящими плитами пола из
            // тёплого набора. Момент открытия ворот обязан читаться
            // мгновенно (задача) — раздельные GateClosed/GateOpen, не одна
            // текстура на оба состояния. Ворота — не рычаг, видны ВСЕГДА
            // (issue #193: "награды и преграды видны всегда, опасность и
            // механизмы — нет") — проверяется РАНЬШЕ рычага ниже (та же
            // "поведение важнее декоративного" логика, что и у остальных
            // веток в этом порядке), хотя вживую Gated и Lever на одной
            // плите не бывают (взаимоисключающие роли, Tile.
            // GuardAgainstConflictingRole).
            if (state.IsGated) return state.IsLeverGateOpen ? TileArtKind.GateOpen : TileArtKind.GateClosed;
            // Владелец, 2026-09-04 (отменяет issue #193): рычаг — механизм,
            // не опасность, видим ВСЕГДА, собственная текстура
            // (TileArtKind.Lever/tile-lever.png), не гейтится примериванием
            // и не делит сигнатуру с триггерами ловушек выше. Подсказка
            // направления на закрытых Воротах (TunnelDebugVisual,
            // Tile.LeverCoordinate) остаётся — необязательна теперь, но
            // лишней не будет.
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
