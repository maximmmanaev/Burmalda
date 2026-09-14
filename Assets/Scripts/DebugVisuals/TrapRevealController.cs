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
    /// сочинять конкретный дизайн звука не в скоупе агента.
    ///
    /// <b>Issue #264 (2026-09-14, «сменить звук раскрытия ловушки на
    /// скрежет/трение тяжёлого предмета о каменный пол»):</b> раньше здесь
    /// был короткий восходящий синус-бип ("TrapRevealBeep") — заменён
    /// процедурным "скрежетом" (<see cref="BuildRevealClip"/>, клип
    /// "TrapRevealStoneScrape") — тот же принцип, что и раньше (функциональная
    /// заглушка под описание, не финальный саунд-дизайн, готового/близкого
    /// звукового ассета в проекте по-прежнему нет — заводить его не в
    /// скоупе агента). Синтез — фильтрованный шум (мягче чистого шипения)
    /// с "зернистой" амплитудной модуляцией понижающейся частоты (имитирует
    /// неровное трение тяжёлого предмета, замедляющееся к концу), не чистый
    /// тон — узнаваемо ДРУГОЙ звук на слух, чем у
    /// <see cref="DecayPulseController"/> (низкий "стук") и новый щелчок шага
    /// (<see cref="StepClickController"/>).
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

        // Скрежет/трение тяжёлого предмета о камень (issue #264, см.
        // класс-докстринг — процедурная заглушка под описание, не финальный
        // саунд-дизайн). Короче секунды — "короткий скрежет", не долгий гул.
        private const float ClipDurationSeconds = 0.25f;
        private const int SampleRate = 44100;

        // Однополюсный low-pass коэффициент — сглаживает белый шум до
        // более глухого "каменного" тембра, не резкого шипения.
        private const float NoiseSmoothing = 0.5f;

        // "Зерно" скрежета — амплитудная модуляция, имитирующая неровное
        // трение (не гладкий непрерывный шум). Частота падает к концу клипа
        // — как будто тяжёлый предмет замедляется, а не едет равномерно.
        private const float GrainStartHz = 90f;
        private const float GrainEndHz = 45f;

        private static AudioClip BuildRevealClip()
        {
            var sampleCount = Mathf.RoundToInt(ClipDurationSeconds * SampleRate);
            var clip = AudioClip.Create("TrapRevealStoneScrape", sampleCount, 1, SampleRate, false);

            // Фиксированный сид — один и тот же клип при каждой пересборке
            // (детерминированность, не критично для звука, но избегает
            // сюрпризов при повторных вызовах в рамках одного запуска).
            var random = new System.Random(2026_09_14);
            var samples = new float[sampleCount];
            var filteredNoise = 0f;
            var grainPhase = 0f;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleCount;

                var whiteNoise = (float)(random.NextDouble() * 2.0 - 1.0);
                filteredNoise += (whiteNoise - filteredNoise) * NoiseSmoothing;

                var grainHz = Mathf.Lerp(GrainStartHz, GrainEndHz, t);
                grainPhase += 2f * Mathf.PI * grainHz / SampleRate;
                var grain = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(grainPhase));

                var envelope = Mathf.Sin(Mathf.PI * t); // плавный подъём-спад — без щелчка на границах клипа
                samples[i] = filteredNoise * grain * envelope * 0.55f;
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
