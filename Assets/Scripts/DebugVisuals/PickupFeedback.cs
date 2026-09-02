using Burmalda.Core;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Дофаминовый визуальный отклик на сбор ресурса (PRD §19: «каждый
    /// собираемый ресурс сопровождается выраженной визуальной обратной
    /// связью — сверкание, партиклы при подборе»; issue #75, часть
    /// «Визуальный фидбек сбора»). Уровень исполнения — процедурный, без
    /// единого арт-ассета (2026-08-18, прямой запрос владельца продукта):
    /// встроенный <see cref="ParticleSystem"/>, без текстур/спрайтов.
    ///
    /// Не трогает <c>Currencies.TrailTileCurrencySystem</c> (у него нет
    /// события сбора, добавлять его — отдельная, более рискованная задача,
    /// затрагивающая уже протестированный производственный код) — вместо
    /// этого независимо слушает то же <see cref="GridTraceTrail.Advanced"/>
    /// и сам проверяет <see cref="Tile.IsManaSource"/>/<see cref="Tile.IsKeySource"/>,
    /// тот же паттерн, что <see cref="TileDebugColor"/> у обычных плит.
    /// Монеты (обычные плиты, PRD раздел 5) сознательно не покрыты —
    /// начисляются на КАЖДОЙ новой плите (основной цикл ходьбы), партикл на
    /// каждый шаг был бы визуальным шумом, а не акцентом.
    ///
    /// Самостоятельно создаёт себя (<see cref="RuntimeInitializeOnLoadMethodAttribute"/>),
    /// без правки .unity/.prefab — тот же паттерн, что <see cref="RestartButton"/>/
    /// <see cref="ScenePostProcessing"/>.
    /// </summary>
    public sealed class PickupFeedback : MonoBehaviour
    {
        // Цвета — те же, что уже участвуют в палитре TileDebugColor не
        // используются напрямую (там про состояние плитки, не про валюту),
        // но подобраны в том же духе: насыщенный, не пересекается с
        // остальными тревожными/декоративными цветами.
        private static readonly Color ManaParticleColor = new Color(80f / 255f, 200f / 255f, 255f / 255f); // голубой — Кристаллы Маны
        private static readonly Color KeyParticleColor = new Color(255f / 255f, 210f / 255f, 60f / 255f); // золотой — Ключи

        private const float BurstLifetimeSeconds = 0.6f;

        // Issue #218 (найдено на реальном Android-билде, задача «разрушение
        // плиты», 2026-09-01, зафиксировано отдельно — не чинилось там):
        // дефолтный материал ParticleSystemRenderer оказался закрашен
        // ярко-розовым (классический Unity-фолбэк "шейдер не найден" —
        // ничего в проекте не ссылается на этот дефолтный шейдер напрямую,
        // агрессивный шейдер-стриппинг вырезает его из билда). Тот же приём
        // фикса, что уже проверен на устройстве на осколках обвала
        // (TunnelDebugVisual.SpawnCollapseDebris) — общий материал на
        // "Sprites/Default" (уже в Always Included Shaders), кэшированный
        // один раз, не на партикл.
        private static Material _burstMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(PickupFeedback));
            host.AddComponent<PickupFeedback>();
            DontDestroyOnLoad(host);
        }

        private GridTraceInputController _input;
        private GridTraceTrail _wiredTrail;

        private void Update()
        {
            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
            if (_input == null || _input.Trail == null) return;

            // Трейл пересобирается на каждый забег (RunStarted) — как и
            // остальные Controller'ы в проекте, ре-подписка на смену
            // инстанса, не только на первую находку.
            if (_input.Trail != _wiredTrail)
            {
                if (_wiredTrail != null) _wiredTrail.Advanced -= HandleAdvanced;
                _wiredTrail = _input.Trail;
                _wiredTrail.Advanced += HandleAdvanced;
            }
        }

        private void OnDisable()
        {
            if (_wiredTrail != null) _wiredTrail.Advanced -= HandleAdvanced;
            _wiredTrail = null;
        }

        private void HandleAdvanced(GridCoordinate coordinate)
        {
            if (_input.Grid == null || !_input.Grid.TryGetTile(coordinate, out var tile)) return;

            if (tile.IsManaSource) SpawnBurst(coordinate, ManaParticleColor);
            else if (tile.IsKeySource) SpawnBurst(coordinate, KeyParticleColor);
        }

        private void SpawnBurst(GridCoordinate coordinate, Color color)
        {
            var worldPosition = _input.Projection.ToWorldPosition(coordinate) + Vector3.up * 0.3f;

            var host = new GameObject("PickupBurst");
            host.transform.position = worldPosition;

            var particles = host.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.duration = BurstLifetimeSeconds;
            main.loop = false;
            main.startLifetime = BurstLifetimeSeconds;
            main.startSpeed = 2.5f;
            main.startSize = 0.18f;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            // Issue #218: материал теперь назначается явно (см. doc-комментарий
            // _burstMaterial) — дефолтный материал ParticleSystemRenderer на
            // устройстве рендерился ярко-розовым из-за шейдер-стриппинга.
            if (_burstMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) _burstMaterial = new Material(shader);
            }

            if (_burstMaterial != null)
            {
                var particleRenderer = host.GetComponent<ParticleSystemRenderer>();
                if (particleRenderer != null) particleRenderer.sharedMaterial = _burstMaterial;
            }

            Destroy(host, BurstLifetimeSeconds + 0.2f);
        }
    }
}
