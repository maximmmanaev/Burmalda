using System;
using UnityEngine;

namespace Burmalda.Platform
{
    /// <summary>
    /// Пауза по сигналу платформы (требования 1.3, 4.7: звук и игра стоят, пока открыта реклама, чужая вкладка и т. п.).
    /// По умолчанию — <c>Time.timeScale = 0</c> и <c>AudioListener.pause</c>; прежний timeScale возвращается при возобновлении.
    /// </summary>
    public sealed class PlatformPauseController : IDisposable
    {
        private readonly IPlatformServices _platform;
        private readonly Action<bool> _apply;
        private bool _paused;
        private float _savedTimeScale = 1f;

        public PlatformPauseController(IPlatformServices platform, Action<bool> apply = null)
        {
            _platform = platform;
            _apply = apply ?? ApplyToUnity;
            _platform.Paused += OnPaused;
            _platform.Resumed += OnResumed;
        }

        public void Dispose()
        {
            _platform.Paused -= OnPaused;
            _platform.Resumed -= OnResumed;
        }

        private void OnPaused()
        {
            if (_paused) return;
            _paused = true;
            _apply(true);
        }

        private void OnResumed()
        {
            if (!_paused) return;
            _paused = false;
            _apply(false);
        }

        private void ApplyToUnity(bool paused)
        {
            if (paused)
            {
                _savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = _savedTimeScale;
            }
            AudioListener.pause = paused;
        }
    }
}
