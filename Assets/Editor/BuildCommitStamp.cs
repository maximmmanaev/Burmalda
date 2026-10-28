using System.Diagnostics;
using UnityEditor;

namespace Burmalda.EditorTools
{
    /// <summary>
    /// Штампует сборку хешем коммита (docs/rules/git-workflow.md, «интеграционная ветка»):
    /// на время сборки <c>PlayerSettings.bundleVersion</c> = «версия+хеш», игра печатает его в лог
    /// при старте (<c>Burmalda.Bootstrap.BuildInfoLog</c>). После сборки версия возвращается.
    /// </summary>
    public static class BuildCommitStamp
    {
        /// <summary>Ставит версию с хешем HEAD; возвращает прежнюю версию для <see cref="Restore"/>.</summary>
        public static string Apply()
        {
            var original = PlayerSettings.bundleVersion;
            var baseVersion = original.Contains("+") ? original.Substring(0, original.IndexOf('+')) : original;
            var hash = ReadHeadHash();
            if (!string.IsNullOrEmpty(hash)) PlayerSettings.bundleVersion = $"{baseVersion}+{hash}";
            return original;
        }

        public static void Restore(string original) => PlayerSettings.bundleVersion = original;

        private static string ReadHeadHash()
        {
            try
            {
                var info = new ProcessStartInfo("git", "rev-parse --short HEAD")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WorkingDirectory = System.IO.Directory.GetCurrentDirectory()
                };
                using var process = Process.Start(info);
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return process.ExitCode == 0 ? output : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
