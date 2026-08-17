using Burmalda.Core;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Debug-цвет плиты по её состоянию (issue #58) — чистая функция, без
    /// сцены и рендер-API, по аналогии с draw() в legacy/burmolda_demo.html:
    /// старт — один цвет, текущая позиция — другой, обычная активная плита
    /// переходит зелёный→оранжевый→красный по мере распада, разрушенная —
    /// тёмная. Не финальный арт — минимальная индикация для ручного
    /// тестирования механик Core/Movement/Decay.
    /// </summary>
    public static class TileDebugColor
    {
        // legacy/burmolda_demo.html, draw(): 'safe' — rgba(80,200,120,...)
        public static readonly Color StartColor = new Color(80f / 255f, 200f / 255f, 120f / 255f);

        // legacy/burmolda_demo.html, draw(): белый маркер текущей позиции трейла
        public static readonly Color CurrentPositionColor = Color.white;

        // legacy/burmolda_demo.html, draw(): активная плита, pct<.5 — rgba(0,220,160,...)
        public static readonly Color FreshColor = new Color(0f, 220f / 255f, 160f / 255f);

        // legacy/burmolda_demo.html, draw(): pct .5..0.8 — rgba(255,160,0,...)
        public static readonly Color HalfDecayedColor = new Color(1f, 160f / 255f, 0f);

        // legacy/burmolda_demo.html, draw(): pct>=.8 — rgba(255,40,40,...)
        public static readonly Color AboutToDecayColor = new Color(1f, 40f / 255f, 40f / 255f);

        // legacy/burmolda_demo.html, draw(): t.state==='destroyed' — #0a0a0e
        public static readonly Color DestroyedColor = new Color(10f / 255f, 10f / 255f, 14f / 255f);

        // legacy/burmolda_demo.html, draw(): t.type==='block' — #140a06
        public static readonly Color BlockedColor = new Color(20f / 255f, 10f / 255f, 6f / 255f);

        // legacy/burmolda_demo.html, draw(): t.type==='pit' — #000 (+внутренний круг #300)
        public static readonly Color PitColor = Color.black;

        // Новый тип из PRD v5 (раздел 4.2) — аналога в прототипе нет (там только 'pit'/'spike').
        // Яркий тревожный оранжево-красный, отличим от градиента распада на глаз.
        public static readonly Color LavaColor = new Color(1f, 90f / 255f, 0f);

        // Issue #10, PRD 4.2 — динамическая мгновенная ловушка после срабатывания
        // триггера. Ярко-жёлтый со вспышкой (условно "взрыв"), не пересекается
        // с остальными тревожными цветами (лава — оранжевый, яма — чёрный).
        public static readonly Color ExplosionColor = new Color(1f, 230f / 255f, 0f);

        // Не финальный арт: плита-триггер (issue #10) сама по себе безопасна,
        // цвет — только чтобы при ручном тестировании видеть, где она есть.
        // Приглушённый жёлтый — отличим и от ExplosionColor, и от градиента распада.
        public static readonly Color ExplosiveTriggerColor = new Color(200f / 255f, 180f / 255f, 40f / 255f);

        // Issue #45, PRD v5 4.2 — плита-цель ловушки с таймингом, пока снаряд/
        // лезвие физически проходит через неё (Tile.IsTimedTrapActive). Единый
        // цвет для обоих видов (стрела/лезвие) — яркая "тревожная" магента, не
        // пересекается ни с одним из уже занятых цветов.
        public static readonly Color TimedTrapActiveColor = new Color(1f, 0f, 200f / 255f);

        // Не финальный арт: плита-триггер (issue #45), как и ExplosiveTriggerColor,
        // сама по себе безопасна. Приглушённая магента — узнаваемо парная к
        // TimedTrapActiveColor, но не путается с ExplosiveTriggerColor (жёлтый).
        public static readonly Color TimedTrapTriggerColor = new Color(160f / 255f, 60f / 255f, 140f / 255f);

        // Вход в Комнату Босса (Tile.IsBoss, PRD v8 §8.1) — единственная
        // строка debug-визуала под механику Босса, которая переживает v8
        // (см. doc-комментарий TileVisualState.IsBoss). Синий — единственный
        // не занятый цвет в этой палитре (зелёный/оранжевый/красный —
        // распад, жёлтый/магента — ловушки, чёрный/тёмный — препятствия).
        public static readonly Color BossColor = new Color(40f / 255f, 40f / 255f, 220f / 255f);

        public static Color Resolve(TileVisualState state)
        {
            if (state.IsDestroyed) return DestroyedColor;
            if (state.IsCurrentPosition) return CurrentPositionColor;
            if (state.IsStart) return StartColor;
            if (state.IsBoss) return BossColor;
            if (state.IsBlocked) return BlockedColor;
            if (state.LethalTrap == LethalTrapType.Pit) return PitColor;
            if (state.LethalTrap == LethalTrapType.Lava) return LavaColor;
            if (state.LethalTrap == LethalTrapType.Explosion) return ExplosionColor;
            if (state.ActiveTimedTrap.HasValue) return TimedTrapActiveColor;
            if (state.IsExplosiveTrapTrigger) return ExplosiveTriggerColor;
            if (state.IsTimedTrapTrigger) return TimedTrapTriggerColor;

            // Остальной тип плиты (валюта/артефакт) добавит сюда же
            // ветвление до градиента распада — препятствие/ловушка уже не
            // участвуют в градиенте, т.к. никогда не начинают распад.
            return ResolveDecayGradient(state.DecayProgress01);
        }

        private static Color ResolveDecayGradient(float progress01)
        {
            var clamped = Mathf.Clamp01(progress01);
            return clamped < 0.5f
                ? Color.Lerp(FreshColor, HalfDecayedColor, clamped / 0.5f)
                : Color.Lerp(HalfDecayedColor, AboutToDecayColor, (clamped - 0.5f) / 0.5f);
        }
    }
}
