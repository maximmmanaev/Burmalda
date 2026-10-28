using UnityEngine;

namespace Burmalda.Bootstrap
{
    /// <summary>Печатает версию сборки с хешем коммита при старте (штамп ставит Editor/BuildCommitStamp).</summary>
    public static class BuildInfoLog
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Log() => Debug.Log($"[Burmalda] build {Application.version}");
    }
}
