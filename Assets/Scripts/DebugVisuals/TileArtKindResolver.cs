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
            if (state.IsCurrentPosition) return TileArtKind.None;
            if (state.IsStart) return TileArtKind.Start;
            if (state.IsBoss) return TileArtKind.None;
            if (state.IsBlocked) return TileArtKind.Blocked;
            if (state.LethalTrap == LethalTrapType.Lava) return TileArtKind.Lava;
            // Issue #163: яма и активированный взрыв — ОДНА категория, не
            // выдающая точный тип (см. Core.TrapSignature). Пакет содержит
            // отдельные tile-pit.png/tile-explosive-explosion.png, но сюда
            // сознательно подключена только одна из них (TileArtCatalog) —
            // подключить обе значило бы нарушить инвариант "видно, что не
            // так, не видно, что именно" из issue #163.
            if (state.LethalTrap == LethalTrapType.Pit || state.LethalTrap == LethalTrapType.Explosion) return TileArtKind.HiddenTrapSignature;
            if (state.ActiveTimedTrap.HasValue) return TileArtKind.TimedTrapActive;
            if (state.IsExplosiveTrapTrigger) return TileArtKind.ExplosiveTrigger;
            if (state.IsTimedTrapTrigger) return TileArtKind.TimedTrapTrigger;

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
