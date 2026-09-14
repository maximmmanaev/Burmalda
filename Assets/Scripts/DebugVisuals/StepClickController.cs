using Burmalda.Core;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Issue #264 («звук щелчка при наступании на каждую обычную плитку»):
    /// короткий, ненавязчивый звук на КАЖДЫЙ успешный шаг по плитке, у
    /// которой нет скрытой опасности и которая не смертельна прямо сейчас —
    /// слушает <see cref="GridTraceTrail.PositionChanged"/> (не
    /// <see cref="GridTraceTrail.Advanced"/> — задача прямо просит "на
    /// каждый шаг", а не только на первое посещение новой плитки, #61).
    ///
    /// Тот же паттерн самобутстрапа/переподписки на смену трейла при
    /// рестарте, что <see cref="PickupFeedback"/> — см. её doc-комментарий.
    /// Звук — процедурный, без арт-ассета (см. doc-комментарий
    /// <see cref="TrapRevealController"/> насчёт того, почему), намеренно
    /// СЛАБЕЕ и КОРОЧЕ звуков ловушек/распада — играет на каждый шаг, не
    /// должен утомлять за десятки шагов забега.
    ///
    /// <b>«Обычная (не скрытая, не опасная)» — два независимых исключения:</b>
    /// <list type="bullet">
    /// <item><b>опасная</b> — <see cref="Tile.LethalTrap"/> уже стоит
    /// (активная угроза прямо сейчас, включая проходимую-но-летальную Лаву,
    /// issue #258, если её фикс уже смержен) — щелчок не проигрывается: шаг
    /// на такую плитку не рядовое событие, тонуть в обычном "тук-тук" не
    /// должен.</item>
    /// <item><b>скрытая</b> — плита несёт триггер одной из пяти ловушек
    /// (<see cref="HasHiddenDanger"/>, ТОТ ЖЕ набор полей, что уже
    /// независимо проверяют <c>Movement.TrapRevealSystem.HasHiddenDanger</c>/
    /// <c>TunnelDebugVisual.Tick</c> — структурное свойство плиты, не
    /// зависит от того, раскрыл ли игрок сигнатуру примериванием) — щелчок
    /// не проигрывается, даже пока триггер ещё не раскрыт: он объективно не
    /// "обычная" плита, даже если игрок пока не знает об этом.</item>
    /// </list>
    /// Все остальные плиты (источники валюты, Алтарь, Босс, рычаг, ворота,
    /// обычный пол) — "обычные" для этой задачи, щелчок играет.
    /// </summary>
    public sealed class StepClickController : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(StepClickController));
            host.AddComponent<StepClickController>();
            DontDestroyOnLoad(host);
        }

        private GridTraceInputController _input;
        private GridTraceTrail _wiredTrail;
        private AudioSource _audioSource;
        private AudioClip _clickClip;

        private void Update()
        {
            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
            if (_input == null || _input.Trail == null) return;

            // Трейл пересобирается на каждый забег (RunStarted) — как и
            // PickupFeedback, ре-подписка на смену инстанса.
            if (_input.Trail != _wiredTrail)
            {
                if (_wiredTrail != null) _wiredTrail.PositionChanged -= HandlePositionChanged;
                _wiredTrail = _input.Trail;
                _wiredTrail.PositionChanged += HandlePositionChanged;
            }
        }

        private void OnDisable()
        {
            if (_wiredTrail != null) _wiredTrail.PositionChanged -= HandlePositionChanged;
            _wiredTrail = null;
        }

        private void HandlePositionChanged(GridCoordinate coordinate)
        {
            if (_input.Grid == null || !_input.Grid.TryGetTile(coordinate, out var tile)) return;
            if (tile.LethalTrap.HasValue) return; // опасная
            if (HasHiddenDanger(tile)) return; // скрытая

            PlayClick();
        }

        // Тот же набор полей, что Movement.TrapRevealSystem.HasHiddenDanger/
        // TunnelDebugVisual.Tick (isTrapTrigger) — три независимых места
        // проверяют одно и то же структурное свойство плиты, не
        // централизовано в Core.Tile (см. их же doc-комментарии) —
        // дублирование уже устоявшийся паттерн в этой части проекта, не
        // новый для этой задачи.
        private static bool HasHiddenDanger(Tile tile) =>
            tile.ArrowWaveTargetRow.HasValue
            || tile.IsBombTrigger
            || tile.BladeTactTargetRow.HasValue
            || tile.IsFallingRockTrigger
            || tile.IsLavaTrigger;

        private void PlayClick()
        {
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            if (_clickClip == null) _clickClip = BuildClickClip();

            _audioSource.PlayOneShot(_clickClip);
        }

        // Короткий мягкий "тик" — фильтрованный шум, не чистый тон (ближе к
        // звуку реального шага, чем к сигналу-предупреждению). Заметно
        // короче и тише звуков ловушек/распада (TrapRevealController/
        // DecayPulseController) — играет на КАЖДЫЙ шаг, должен быть
        // ненавязчивым фоном, не акцентом.
        private const float ClipDurationSeconds = 0.045f;
        private const int SampleRate = 44100;
        private const float NoiseSmoothing = 0.35f;

        private static AudioClip BuildClickClip()
        {
            var sampleCount = Mathf.RoundToInt(ClipDurationSeconds * SampleRate);
            var clip = AudioClip.Create("StepClickTick", sampleCount, 1, SampleRate, false);

            var random = new System.Random(2026_09_14);
            var samples = new float[sampleCount];
            var filteredNoise = 0f;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleCount;

                var whiteNoise = (float)(random.NextDouble() * 2.0 - 1.0);
                filteredNoise += (whiteNoise - filteredNoise) * NoiseSmoothing;

                var envelope = Mathf.Pow(1f - t, 4f); // резкий спад — короткий щелчок, не хвост
                samples[i] = filteredNoise * envelope * 0.35f; // тише скрежета/стука — на каждый шаг
            }

            clip.SetData(samples, 0);
            return clip;
        }
    }
}
