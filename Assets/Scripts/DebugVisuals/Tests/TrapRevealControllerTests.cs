using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Burmalda.DebugVisuals.Tests
{
    /// <summary>
    /// Issue #264 («сменить звук раскрытия ловушки на скрежет/трение тяжёлого
    /// предмета о камень»): звук как таковой один в один не проверить тестом
    /// (см. задача) — но то, что вызывающий код (<see cref="TrapRevealController.OnSignatureRevealed"/>)
    /// в нужном месте назначает/проигрывает ИМЕННО обновлённый клип, а не
    /// прежний "TrapRevealBeep" (процедурный синус-бип, удалён этой задачей),
    /// проверяемо через ссылку на построенный <see cref="AudioClip"/> —
    /// рефлексия на приватный статический билдер, тот же паттерн, что
    /// <c>Tests.InvokePrivate</c>/<c>GetPrivateField</c> в остальном проекте.
    /// </summary>
    public class TrapRevealControllerTests
    {
        private static AudioClip InvokeBuildRevealClip()
        {
            var method = typeof(TrapRevealController).GetMethod("BuildRevealClip", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "TrapRevealController.BuildRevealClip не найден рефлексией — переименован/сигнатура изменилась?");
            return (AudioClip)method.Invoke(null, null);
        }

        [Test]
        public void BuildRevealClip_NameNoLongerReferencesOldBeepSound()
        {
            var clip = InvokeBuildRevealClip();

            Assert.IsNotNull(clip);
            StringAssert.DoesNotContain("Beep", clip.name,
                "issue #264: прежний синус-бип ('TrapRevealBeep') должен быть заменён на скрежет/трение о камень");
        }

        [Test]
        public void BuildRevealClip_NameReflectsScrapeSound()
        {
            var clip = InvokeBuildRevealClip();

            StringAssert.Contains("Scrape", clip.name,
                "issue #264: имя клипа должно отражать новый звук (скрежет), чтобы его было легко найти при переслушивании/замене");
        }

        [Test]
        public void BuildRevealClip_ProducesNonEmptyAudio()
        {
            var clip = InvokeBuildRevealClip();

            Assert.Greater(clip.samples, 0);
            Assert.Greater(clip.length, 0f);
        }
    }
}
