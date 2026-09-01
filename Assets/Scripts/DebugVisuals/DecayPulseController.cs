using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Задача «разрушение плиты» (владелец, 2026-09-01), пункт 3:
    /// "непрерывный сигнал (визуал + звук + вибрация) в последней трети
    /// распада" — прямо закрывает первый (единственный оставшийся
    /// незакрытым) критерий приёмки issue #194: "приближение обвала подаётся
    /// НЕПРЕРЫВНЫМ сигналом... не позволяют игроку попасть в окно Отклика
    /// «На волоске» после 85% порога распада". Визуальная часть сигнала —
    /// <see cref="TunnelDebugVisual.ApplyVisual"/> (пульс альфы оверлея
    /// трещин на текущей плите), этот класс — только звук/вибрация, тот же
    /// паттерн разделения, что <see cref="TrapRevealController"/> относительно
    /// <see cref="TilePreviewController"/>.
    ///
    /// В отличие от <see cref="TrapRevealController"/> (одноразовое событие
    /// при раскрытии), здесь сигнал ПОВТОРЯЕТСЯ, пока игрок стоит на плите
    /// в последней трети распада, и УЧАЩАЕТСЯ по мере приближения к
    /// разрушению — от одного пульса раз в <see cref="MaxPulseIntervalSeconds"/>
    /// до почти непрерывного дребезга у самого порога, вместо одного
    /// дискретного щелчка на пороге "последняя треть".
    /// </summary>
    public sealed class DecayPulseController : MonoBehaviour
    {
        // Задача: "в последней трети распада" — структурный порог механики
        // (как и деление на трети раньше в TileArtKindResolver до задачи
        // «разрушение плиты»), не балансное число, поэтому не в
        // DecayCollapseFeedback/дебаг-панели, как и thresholds у
        // TrapRevealController.BuildRevealClip. Public — та же константа
        // переиспользуется TunnelDebugVisual.UpdateCrackOverlay для
        // визуальной половины того же непрерывного сигнала (звук/вибро
        // здесь, крупная пульсация оверлея трещин — там), один источник
        // истины вместо двух чисел, которые могли бы разойтись.
        public const float LastThirdThreshold01 = 2f / 3f;

        // Частота пульса растёт от одного раза в MaxPulseIntervalSeconds на
        // входе в последнюю треть до MinPulseIntervalSeconds у самого
        // порога разрушения — непрерывность обеспечивается именно ростом
        // ЧАСТОТЫ, не одним фиксированным интервалом (иначе сигнал снова
        // стал бы дискретным "щелчком", который задача явно просит не
        // допустить).
        private const float MaxPulseIntervalSeconds = 0.9f;
        private const float MinPulseIntervalSeconds = 0.12f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(DecayPulseController));
            host.AddComponent<DecayPulseController>();
            DontDestroyOnLoad(host);
        }

        private GridTraceInputController _input;
        private AudioSource _audioSource;
        private AudioClip _pulseClip;
        private bool _wasInDangerZone;
        private float _nextPulseTime;

        private void Update()
        {
            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;

            var tile = _input.Grid.GetOrCreateTile(_input.Trail.CurrentPosition);
            var inDangerZone = !tile.IsDestroyed && tile.DecayProgress01 >= LastThirdThreshold01;

            if (!inDangerZone)
            {
                _wasInDangerZone = false;
                return;
            }

            if (!_wasInDangerZone)
            {
                // Первый пульс — сразу на входе в последнюю треть, не
                // спустя MaxPulseIntervalSeconds ожидания (игрок должен
                // почувствовать сигнал в тот же момент, когда он появился).
                _wasInDangerZone = true;
                _nextPulseTime = Time.time;
            }

            if (Time.time < _nextPulseTime) return;

            var withinLastThird01 = Mathf.InverseLerp(LastThirdThreshold01, 1f, Mathf.Clamp01(tile.DecayProgress01));
            var interval = Mathf.Lerp(MaxPulseIntervalSeconds, MinPulseIntervalSeconds, withinLastThird01);
            _nextPulseTime = Time.time + interval;

            PlayPulseSound();
            PlayPulseVibration();
        }

        // Короткий низкий "стук" — намеренно отличается от восходящего
        // синус-бипа TrapRevealController.BuildRevealClip (та же процедурная
        // заглушка без арт-ассета, см. её doc-комментарий), чтобы игрок на
        // слух отличал "здесь скрытая опасность" от "плита под тобой вот-вот
        // рухнет" — разные контексты, разный звук.
        private const float ClipDurationSeconds = 0.08f;
        private const int SampleRate = 44100;
        private const float FrequencyHz = 180f;

        private void PlayPulseSound()
        {
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            if (_pulseClip == null) _pulseClip = BuildPulseClip();

            _audioSource.PlayOneShot(_pulseClip);
        }

        private static AudioClip BuildPulseClip()
        {
            var sampleCount = Mathf.RoundToInt(ClipDurationSeconds * SampleRate);
            var clip = AudioClip.Create("DecayPulseThud", sampleCount, 1, SampleRate, false);

            var samples = new float[sampleCount];
            var phase = 0f;
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleCount;
                phase += 2f * Mathf.PI * FrequencyHz / SampleRate;
                var envelope = 1f - t; // линейный спад — без щелчка на конце клипа
                samples[i] = Mathf.Sin(phase) * envelope * 0.6f;
            }

            clip.SetData(samples, 0);
            return clip;
        }

        private void PlayPulseVibration()
        {
            // Handheld.Vibrate() без параметров — как и TrapRevealController,
            // но здесь один импульс на пульс (не серия): повторяемость самого
            // сигнала уже обеспечивает Update(), а не длина одной вибрации.
            if (DecayCollapseFeedback.PulseVibrationStrength <= 0f) return;
            Handheld.Vibrate();
        }
    }
}
