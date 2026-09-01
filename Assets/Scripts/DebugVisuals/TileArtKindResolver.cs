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
        public static TileArtKind Resolve(TileVisualState state)
        {
            if (state.IsDestroyed) return TileArtKind.Destroyed;
            // Задача «тёплый набор плит»: раньше None (белый цветной
            // фолбэк, самый невнятный объект на экране по плейтесту
            // владельца) — теперь tile-current-position.png.
            if (state.IsCurrentPosition) return TileArtKind.CurrentPosition;
            if (state.IsStart) return TileArtKind.Start;
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
            if (state.IsBlocked) return TileArtKind.Blocked;
            if (state.LethalTrap == LethalTrapType.Lava) return TileArtKind.Lava;
            // Issue #163/задача «тёплый набор плит»: яма и активированный
            // взрыв — ОДНА категория, не выдающая точный тип (см.
            // Core.TrapSignature) — теперь общий tile-hidden-trap-
            // signature.png, выделенный под эту роль (раньше сюда
            // подключался tile-pit.png по необходимости, без палитры под
            // сигнатуру отдельно).
            if (state.LethalTrap == LethalTrapType.Pit || state.LethalTrap == LethalTrapType.Explosion) return TileArtKind.HiddenTrapSignature;
            if (state.ActiveTimedTrap.HasValue) return TileArtKind.TimedTrapActive;
            // Единая сигнатура триггера (задача «сделать тоннель играбельным»,
            // часть 3) — тот же принцип, что HiddenTrapSignature выше:
            // отдельный TileArtKind, но задача «тёплый набор плит»
            // (владелец, прямое указание) отдаёт под него ТУ ЖЕ САМУЮ
            // текстуру, что и под HiddenTrapSignature (см. TileArtCatalog.Get)
            // — PRD v9 §4.2, ловушку от механизма не отличить без Идола Чутья.
            if (state.IsExplosiveTrapTrigger || state.IsTimedTrapTrigger) return TileArtKind.TriggerSignature;

            // Источники валюты (часть 1 задачи «сделать тоннель играбельным»):
            // раньше None (в пакете были только иконки Icons/Currencies/, не
            // тайлы пола) — задача «тёплый набор плит» добавила
            // tile-mana-source.png/tile-key-source.png.
            if (state.IsManaSource) return TileArtKind.ManaSource;
            if (state.IsKeySource) return TileArtKind.KeySource;

            return ResolveDecayGradient(state.DecayProgress01);
        }

        // TileDebugColor.Resolve плавно интерполирует между тремя цветами по
        // DecayProgress01 — для дискретных текстур плавности нет, поэтому
        // делим 0..1 на трети (а не на половины, как у центра Lerp'а в
        // TileDebugColor): FreshColor/HalfDecayedColor/AboutToDecayColor
        // были рассчитаны как равнозначные опорные точки трёхцветного
        // градиента, трети — ближайший дискретный аналог того же намерения.
        private static TileArtKind ResolveDecayGradient(float progress01)
        {
            if (progress01 < 1f / 3f) return TileArtKind.Fresh;
            return progress01 < 2f / 3f ? TileArtKind.HalfDecayed : TileArtKind.AboutToDecay;
        }
    }
}
