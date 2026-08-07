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

        public static Color Resolve(TileVisualState state)
        {
            if (state.IsDestroyed) return DestroyedColor;
            if (state.IsCurrentPosition) return CurrentPositionColor;
            if (state.IsStart) return StartColor;
            if (state.IsBlocked) return BlockedColor;

            // Остальной тип плиты (яма/лава/валюта/артефакт) из Traps (#9)
            // добавит сюда же ветвление до градиента распада — препятствие
            // уже не участвует в градиенте, т.к. никогда не начинает распад.
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
