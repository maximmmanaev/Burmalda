using NUnit.Framework;

namespace Burmalda.DebugVisuals.Tests
{
    /// <summary>
    /// Issue #264 («снизить силу и длительность вибрации раскрытия»):
    /// сравнение с прежними значениями (0.6/0.15с, задача «раскрытие
    /// опасности при примеривании») — "короче и слабее прежней", не
    /// конкретные новые числа (владелец подбирает баланс сам, см.
    /// doc-комментарий <see cref="TrapRevealFeedback"/>), тест ловит
    /// именно направление изменения, не сами значения.
    /// </summary>
    public class TrapRevealFeedbackTests
    {
        // Значения ДО issue #264 — зафиксированы здесь как база сравнения,
        // не как то, что поля должны содержать.
        private const float PreviousVibrationStrength = 0.6f;
        private const float PreviousVibrationDurationSeconds = 0.15f;

        [Test]
        public void VibrationStrength_ReducedBelowPreviousDefault()
        {
            Assert.Less(TrapRevealFeedback.VibrationStrength, PreviousVibrationStrength,
                "issue #264: сила вибрации раскрытия обязана быть меньше прежней (0.6)");
        }

        [Test]
        public void VibrationDurationSeconds_ReducedBelowPreviousDefault()
        {
            Assert.Less(TrapRevealFeedback.VibrationDurationSeconds, PreviousVibrationDurationSeconds,
                "issue #264: длительность вибрации раскрытия обязана быть меньше прежней (0.15с)");
        }

        [Test]
        public void VibrationStrength_NotZero_StillPresent_NotFullyRemoved()
        {
            // В отличие от вибрации распада (issue #264, п.2 — убрана
            // ПОЛНОСТЬЮ), вибрация раскрытия только уменьшена, не убрана.
            Assert.Greater(TrapRevealFeedback.VibrationStrength, 0f,
                "вибрацию раскрытия нужно уменьшить, не убрать совсем — в отличие от вибрации распада");
        }

        [Test]
        public void VibrationDurationSeconds_NotZero_StillPresent_NotFullyRemoved()
        {
            Assert.Greater(TrapRevealFeedback.VibrationDurationSeconds, 0f,
                "вибрацию раскрытия нужно уменьшить, не убрать совсем — в отличие от вибрации распада");
        }
    }
}
