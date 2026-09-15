using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Burmalda.DebugVisuals.Tests
{
    /// <summary>
    /// Issue #264 («убрать вибрацию при разрушении/распаде плитки — оставить
    /// только звук/визуал»): регрессия на ПОЛНОЕ отсутствие вибро-кода в
    /// <see cref="DecayPulseController"/>, не только на то, что он "просто не
    /// вызывается" — reflection ищет любой метод/поле с "Vibrat" в имени
    /// (без учёта регистра), а не конкретную сигнатуру, которая могла бы
    /// незаметно остаться неиспользуемой при рефакторинге.
    /// </summary>
    public class DecayPulseControllerTests
    {
        [Test]
        public void HasNoVibrationRelatedMethodOrField_VibrationRemovedEntirely_NotJustDisconnected()
        {
            var type = typeof(DecayPulseController);
            var members = type
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Select(m => m.Name)
                .Concat(type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Select(f => f.Name));

            var vibrationMember = members.FirstOrDefault(name => name.IndexOf("Vibrat", System.StringComparison.OrdinalIgnoreCase) >= 0);

            Assert.IsNull(vibrationMember,
                $"issue #264: вибрация при распаде должна быть убрана полностью, но найден член '{vibrationMember}' — похоже, вибро-код оставлен, просто не вызывается");
        }

        [Test]
        public void DecayCollapseFeedback_HasNoVibrationStrengthField_CapabilityRemovedNotJustDefaultedToZero()
        {
            // Убрана вся возможность (не оставлена как "0 по умолчанию") —
            // иначе владелец мог бы снова включить её через дебаг-панель
            // (которой тоже больше нет), что противоречило бы "убрать
            // полностью" (в отличие от вибрации раскрытия — issue #264, п.3,
            // та уменьшена, не убрана).
            var field = typeof(DecayCollapseFeedback).GetField("PulseVibrationStrength", BindingFlags.Public | BindingFlags.Static);

            Assert.IsNull(field, "issue #264: DecayCollapseFeedback.PulseVibrationStrength должен быть удалён, не оставлен неиспользуемым");
        }
    }
}
