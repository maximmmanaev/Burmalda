using Burmalda.D20;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Issue #110 (PRD §19, «d20-бросок»): текстовый и цветовой отклик на
    /// <see cref="Burmalda.RunLifecycle.RunState.D20Resolved"/> — событие уже
    /// существует, подписчиков не было. Чистая функция состояние→(текст,
    /// цвет), без Unity-зависимостей кроме <see cref="Color"/> — по аналогии
    /// с <see cref="TileDebugColor.Resolve"/>. Текст — дословно из
    /// doc-комментариев <see cref="D20Outcome"/> (PRD раздел 9), не
    /// придуман заново.
    /// </summary>
    public static class D20OutcomeDebugText
    {
        public static readonly Color FortuneColor = new Color(0f, 220f / 255f, 160f / 255f); // TileDebugColor.FreshColor — тот же "хорошо"
        public static readonly Color KnockbackColor = new Color(1f, 160f / 255f, 0f); // TileDebugColor.HalfDecayedColor — тот же "штраф, не конец"
        public static readonly Color DeathColor = new Color(1f, 40f / 255f, 40f / 255f); // TileDebugColor.AboutToDecayColor — тот же "смерть"

        public static string BuildMessage(D20Outcome outcome) =>
            outcome switch
            {
                D20Outcome.Fortune => "d20: Fortune (15-20) — повезло, экспедиция продолжается без потерь",
                D20Outcome.Knockback => "d20: Knockback (10-14) — откат к последнему Алтарю, прогресс за ним потерян",
                D20Outcome.Death => "d20: Death (1-9) — смерть, вся временная добыча забега потеряна",
                _ => outcome.ToString()
            };

        public static Color ResolveColor(D20Outcome outcome) =>
            outcome switch
            {
                D20Outcome.Fortune => FortuneColor,
                D20Outcome.Knockback => KnockbackColor,
                D20Outcome.Death => DeathColor,
                _ => Color.white
            };
    }
}
