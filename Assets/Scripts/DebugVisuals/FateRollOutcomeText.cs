using Burmalda.FateRoll;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Текст и цвет отклика на <see cref="Burmalda.RunLifecycle.RunState.FateRolled"/>
    /// (PRD v10 §16). Чистая функция исход→(текст, цвет), без Unity-зависимостей
    /// кроме <see cref="Color"/>.
    /// </summary>
    public static class FateRollOutcomeText
    {
        public static readonly Color InPlaceColor = new Color(0f, 220f / 255f, 160f / 255f);
        public static readonly Color AltarColor = new Color(1f, 160f / 255f, 0f);
        public static readonly Color DeathColor = new Color(1f, 40f / 255f, 40f / 255f);

        public static string BuildMessage(FateRollResult result)
        {
            var dice = $"{result.Die1}+{result.Die2}" + (result.Modifier != 0 ? $"{result.Modifier:+#;-#}" : "") + $" = {result.Sum}";
            return result.Outcome switch
            {
                FateOutcome.InPlace => $"Бросок {result.RollIndex}: {dice} — на месте, поход продолжается",
                FateOutcome.ToAltar => $"Бросок {result.RollIndex}: {dice} — к последнему Алтарю",
                FateOutcome.Death => $"Бросок {result.RollIndex}: {dice} — смерть",
                _ => result.Outcome.ToString()
            };
        }

        public static Color ResolveColor(FateOutcome outcome) =>
            outcome switch
            {
                FateOutcome.InPlace => InPlaceColor,
                FateOutcome.ToAltar => AltarColor,
                _ => DeathColor
            };
    }
}
