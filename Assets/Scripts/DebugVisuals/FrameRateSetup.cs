using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Явный <see cref="Application.targetFrameRate"/> — найдено при
    /// проверке на устройстве во время задачи 2 (не связано с HUD
    /// напрямую): без явного значения и при <c>vSyncCount: 0</c> (дефолт
    /// Unity 3D URP шаблона, <c>ProjectSettings/QualitySettings.asset</c>,
    /// не менялся с Sprint 1, issue #4) Android может выбирать неровный
    /// frame pacing на дисплеях с переменной частотой обновления (Realme
    /// RMX3370 — 120Hz) — на глаз читается как лаг/просадки движения, не
    /// связанные с логикой камеры (проверено: ни <c>Choreographer: Skipped
    /// frames</c>, ни GC-пауз в logcat во время движения не найдено —
    /// технической просадки FPS не подтверждено, но frame pacing без
    /// явного target — реальный, воспроизводимый гэп конфигурации).
    ///
    /// 60, не 120: остальные покадровые системы проекта (в первую очередь
    /// <c>Movement.TunnelCameraFollow</c>) не тестировались и не тюнились
    /// под 120 тиков/с — закладывать вдвое больше кадров в секунду без
    /// плейтеста было бы самостоятельным решением по ощущению/балансу
    /// (docs/rules/forbidden-actions.md). <c>vSyncCount</c> НЕ трогается —
    /// именно при 0 <see cref="Application.targetFrameRate"/> вообще
    /// применяется (Unity игнорирует его, пока vSyncCount &gt; 0).
    ///
    /// Чистый runtime API, самобутстрапится через
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/> — не трогает
    /// .unity/.prefab (docs/rules/forbidden-actions.md).
    /// </summary>
    public static class FrameRateSetup
    {
        public const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            Application.targetFrameRate = TargetFrameRate;
        }
    }
}
