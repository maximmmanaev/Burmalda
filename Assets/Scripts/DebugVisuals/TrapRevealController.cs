using Burmalda.Core;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Задача «раскрытие опасности при примеривании»: тонкий driver (тот же
    /// паттерн, что <see cref="TilePreviewController"/>/<see cref="PickupFeedback"/>) —
    /// тикает <see cref="TrapRevealSystem"/> текущим
    /// <see cref="GridTraceInputController.PreviewTarget"/> каждый кадр и на
    /// <see cref="TrapRevealSystem.SignatureRevealed"/> проигрывает звук и
    /// вибрацию (оба требования задачи — "проигрывается звук",
    /// "срабатывает вибрация устройства").
    ///
    /// <b>Звук — процедурный, без арт-ассета</b>, тем же прямым прецедентом,
    /// что <see cref="PickupFeedback"/> для партиклов (её doc-комментарий:
    /// "прямой запрос владельца продукта... без единого арт-ассета") — в
    /// проекте нет ни одного AudioClip-ассета и звукового пайплайна, а
    /// сочинять конкретный дизайн звука не в скоупе агента. Короткий
    /// синус-бип через <see cref="AudioClip.Create"/> — функциональная
    /// заглушка ("звук проигрался"), не финальный саунд-дизайн.
    ///
    /// <b>Вибрация — <see cref="Handheld.Vibrate"/></b>, единственный
    /// портативный способ без нативного платформенного кода: у него нет
    /// параметров длительности/силы (фиксированный импульс ОС). Оба
    /// параметра задачи ("длительность раскрытия, сила вибрации" — в
    /// дебаг-панель, см. <see cref="TrapRevealFeedback"/>) применены как
    /// можем средствами базового API — сила ниже порога полностью
    /// отключает вибрацию, длительность управляет тем, сколько раз подряд
    /// (с фиксированным шагом) повторяется импульс, грубо симулируя более
    /// долгий "гул" на платформах без амплитудного контроля.
    /// </summary>
    public sealed class TrapRevealController : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(TrapRevealController));
            host.AddComponent<TrapRevealController>();
            DontDestroyOnLoad(host);
        }

        private GridTraceInputController _input;
        private TrapRevealSystem _system;
        private TunnelGrid _systemGrid;
        private AudioSource _audioSource;
        private AudioClip _revealClip;

        private void Update()
        {
            // Ленивый поиск/пересборка — тот же принцип, что у остальных
            // debug/game driver'ов проекта (порядок Awake между объектами
            // сцены не гарантирован, Grid пересоздаётся на каждый забег —
            // сверяем по ссылке, не только на null, чтобы пересобрать
            // систему на новый Grid после рестарта, а не тикать старый).
            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
            if (_input == null || _input.Grid == null) return;

            if (_system == null || !ReferenceEquals(_systemGrid, _input.Grid))
            {
                _system = new TrapRevealSystem(_input.Grid);
                _systemGrid = _input.Grid;
                _system.SignatureRevealed += OnSignatureRevealed;
            }

            // ExamineTarget, не PreviewTarget — скрытая смертельная ловушка
            // всегда даёт CanAdvanceTo=false (см. её doc-комментарий), а
            // PreviewTarget требует CanAdvanceTo. ExamineTarget — то же
            // соседство под пальцем, но без этого ограничения, иначе
            // разведать ловушку было бы физически невозможно.
            _system.Tick(_input.ExamineTarget);
        }

        private void OnSignatureRevealed(GridCoordinate coordinate)
        {
            PlayRevealSound();
            PlayRevealVibration();
        }

        private void PlayRevealSound()
        {
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            if (_revealClip == null) _revealClip = BuildRevealClip();

            _audioSource.PlayOneShot(_revealClip);
        }

        // Короткий восходящий синус-бип (см. класс-докстринг — процедурная заглушка).
        private const float ClipDurationSeconds = 0.12f;
        private const int SampleRate = 44100;
        private const float StartFrequencyHz = 660f;
        private const float EndFrequencyHz = 990f;

        private static AudioClip BuildRevealClip()
        {
            var sampleCount = Mathf.RoundToInt(ClipDurationSeconds * SampleRate);
            var clip = AudioClip.Create("TrapRevealBeep", sampleCount, 1, SampleRate, false);

            var samples = new float[sampleCount];
            var phase = 0f;
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleCount;
                var frequency = Mathf.Lerp(StartFrequencyHz, EndFrequencyHz, t);
                phase += 2f * Mathf.PI * frequency / SampleRate;
                var envelope = 1f - t; // линейный спад громкости — избегает щелчка на конце клипа
                samples[i] = Mathf.Sin(phase) * envelope * 0.5f;
            }

            clip.SetData(samples, 0);
            return clip;
        }

        private void PlayRevealVibration()
        {
            if (TrapRevealFeedback.VibrationStrength <= 0f || TrapRevealFeedback.VibrationDurationSeconds <= 0f) return;

            // Handheld.Vibrate() не берёт параметров — повторяем импульс с
            // фиксированным шагом на протяжении VibrationDurationSeconds,
            // число повторов растёт с VibrationStrength (грубая имитация
            // "силы" через частоту импульсов, см. класс-докстринг).
            var pulseIntervalSeconds = Mathf.Lerp(0.12f, 0.04f, Mathf.Clamp01(TrapRevealFeedback.VibrationStrength));
            var pulseCount = Mathf.Max(1, Mathf.RoundToInt(TrapRevealFeedback.VibrationDurationSeconds / pulseIntervalSeconds));
            StartCoroutine(VibratePulses(pulseCount, pulseIntervalSeconds));
        }

        private System.Collections.IEnumerator VibratePulses(int count, float interval)
        {
            for (var i = 0; i < count; i++)
            {
                Handheld.Vibrate();
                if (i < count - 1) yield return new WaitForSeconds(interval);
            }
        }
    }
}
