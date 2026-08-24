using UnityEngine;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals.HudDesign
{
    /// <summary>
    /// Анимация счётчика при подборе (design-spec.md, «Заметки дизайнера»:
    /// "обновляются анимацией счётчика при каждом подборе") — плавно
    /// подтягивает отображаемое число к целевому за <see cref="DurationSeconds"/>,
    /// вместо мгновенной замены текста. Презентационный слой поверх уже
    /// существующего <c>RunCurrencyAccumulator.Total</c> — самого числа не
    /// трогает, только то, как быстро оно "доезжает" на экране.
    /// </summary>
    public sealed class HudCounterAnimator : MonoBehaviour
    {
        private const float DurationSeconds = 0.35f;

        private Text _text;
        private float _displayedValue;
        private float _targetValue;
        private float _elapsedSeconds;
        private float _fromValue;

        private void Awake() => _text = GetComponent<Text>();

        /// <summary>Задаёт новое целевое значение — если оно отличается от текущего, запускает досчёт с текущего отображаемого числа.</summary>
        public void SetTarget(int value)
        {
            if (Mathf.Approximately(_targetValue, value)) return;
            _fromValue = _displayedValue;
            _targetValue = value;
            _elapsedSeconds = 0f;
        }

        /// <summary>Мгновенно показывает значение без анимации — для первого кадра/рестарта забега.</summary>
        public void SetImmediate(int value)
        {
            _displayedValue = value;
            _targetValue = value;
            _fromValue = value;
            _elapsedSeconds = DurationSeconds;
            _text.text = Mathf.RoundToInt(_displayedValue).ToString();
        }

        private void Update()
        {
            if (_elapsedSeconds >= DurationSeconds) return;

            _elapsedSeconds += Time.deltaTime;
            var t = Mathf.Clamp01(_elapsedSeconds / DurationSeconds);
            var eased = t * t * (3f - 2f * t); // SmoothStep
            _displayedValue = Mathf.Lerp(_fromValue, _targetValue, eased);
            _text.text = Mathf.RoundToInt(_displayedValue).ToString();
        }
    }
}
