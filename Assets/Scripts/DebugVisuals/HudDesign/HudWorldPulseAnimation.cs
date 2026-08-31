using UnityEngine;

namespace Burmalda.DebugVisuals.HudDesign
{
    /// <summary>
    /// Та же мягкая пульсация, что <see cref="HudPulseAnimation"/> (2.4с
    /// SmoothStep-дыхание масштаба), но для world-space объектов (обычный
    /// <see cref="Transform"/>, не <see cref="RectTransform"/>) — красная
    /// рамка примеривания плитки (<see cref="RunHudDesignOverlay"/>) стоит
    /// в мире, не в Canvas.
    /// </summary>
    public sealed class HudWorldPulseAnimation : MonoBehaviour
    {
        private const float PeriodSeconds = 2.4f;
        private const float MinScale = 0.95f;
        private const float MaxScale = 1.08f;

        private void Update()
        {
            var phase = Mathf.PingPong(Time.time, PeriodSeconds / 2f) / (PeriodSeconds / 2f);
            var eased = phase * phase * (3f - 2f * phase);
            transform.localScale = Vector3.one * Mathf.Lerp(MinScale, MaxScale, eased);
        }
    }
}
