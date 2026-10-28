using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Burmalda.EditorTools
{
    /// <summary>
    /// Сборка WebGL из командной строки (<c>scripts/build-webgl.sh</c>, issue #326) —
    /// проверка платформы MVP (Яндекс Игры). Лимит платформы (100 МБ) считается по
    /// распакованным файлам, поэтому на время сборки сжатие отключается, после сборки
    /// значение из ProjectSettings возвращается.
    /// </summary>
    public static class WebGLBuild
    {
        public const string OutputPath = "Builds/WebGL";

        public static void Build()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            var originalCompression = PlayerSettings.WebGL.compressionFormat;
            BuildReport report;
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = OutputPath,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                });
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = originalCompression;
            }

            Debug.Log($"[WebGLBuild] {report.summary.result}, ошибок {report.summary.totalErrors}, предупреждений {report.summary.totalWarnings}");
            if (report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
