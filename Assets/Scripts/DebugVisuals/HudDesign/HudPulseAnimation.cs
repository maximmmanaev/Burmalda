using UnityEngine;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals.HudDesign
{
    /// <summary>
    /// Мягкая пульсация («pulseGlow», 2.4с — см. design-spec.md, «Число +
    /// множитель») — лёгкое дыхание масштаба/альфы золотой «таблетки»
    /// множителя. Чистая презентационная анимация, не игровая логика.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class HudPulseAnimation : MonoBehaviour
    {
        private const float PeriodSeconds = 2.4f;
        private const float MinScale = 0.97f;
        private const float MaxScale = 1.03f;

        private RectTransform _rect;

        private void Awake() => _rect = (RectTransform)transform;

        private void Update()
        {
            var phase = Mathf.PingPong(Time.time, PeriodSeconds / 2f) / (PeriodSeconds / 2f);
            var eased = phase * phase * (3f - 2f * phase); // SmoothStep — тот же приём, что TunnelCameraSoftBoundary
            _rect.localScale = Vector3.one * Mathf.Lerp(MinScale, MaxScale, eased);
        }
    }
}
