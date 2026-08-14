using System.Linq;
using UnityEditor;

namespace Burmalda.EditorTools
{
    /// <summary>
    /// Сборка билда из командной строки (<c>Unity -batchmode -quit
    /// -executeMethod ...</c>) — для быстрой тестовой сборки на подключённое
    /// устройство без ручных кликов в Build Settings. Не привязано ни к
    /// одной игровой системе — чистая build-инфраструктура.
    /// </summary>
    public static class BuildScript
    {
        /// <summary>Собирает Android APK (не AAB — устанавливается напрямую через adb install) из сцен, включённых в Build Settings.</summary>
        public static void BuildAndroid()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            EditorUserBuildSettings.buildAppBundle = false;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Builds/Android/Burmalda.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
