using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Поднимает уровень отладочного визуала без единого арт-ассета (прямой
    /// запрос владельца продукта, 2026-08-18) — только встроенные
    /// инструменты URP: Bloom + Vignette через рантайм-созданный
    /// <see cref="Volume"/>/<see cref="VolumeProfile"/>. Никаких .asset-файлов
    /// не создаётся — профиль существует только в памяти сессии, как и
    /// материалы <see cref="TunnelDebugVisual"/>.
    ///
    /// Самостоятельно создаёт себя через <see cref="RuntimeInitializeOnLoadMethodAttribute"/>
    /// — без правки .unity/.prefab (docs/rules/forbidden-actions.md), тот же
    /// паттерн, что <see cref="RestartButton"/>. Свет в сцене уже настроен
    /// (мягкие тени, intensity 2) — не трогается, здесь только пост-обработка.
    /// </summary>
    public sealed class ScenePostProcessing : MonoBehaviour
    {
        // Черновые значения "на глазок" — не PRD, не баланс, чисто
        // визуальный тюнинг debug-уровня. Порог и интенсивность подобраны,
        // чтобы яркие плоские цвета TileDebugColor чуть "светились", не
        // засвечивая экран целиком.
        private const float BloomThreshold = 0.9f;
        private const float BloomIntensity = 0.6f;
        private const float VignetteIntensity = 0.25f;
        private const float VignetteSmoothness = 0.6f;
        private const float SaturationBoost = 15f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(ScenePostProcessing));
            host.AddComponent<ScenePostProcessing>();
            DontDestroyOnLoad(host);
        }

        private Camera _camera;
        private bool _volumeCreated;

        private void Update()
        {
            // Ленивый поиск камеры — тот же паттерн, что RestartButton
            // ищет GridTraceInputController: порядок Awake между разными
            // компонентами сцены не гарантирован.
            if (_camera == null) _camera = FindFirstObjectByType<Camera>();
            if (_camera == null || _volumeCreated) return;

            EnablePostProcessingOnCamera(_camera);
            CreateGlobalVolume();
            _volumeCreated = true;
        }

        private static void EnablePostProcessingOnCamera(Camera camera)
        {
            var cameraData = camera.GetUniversalAdditionalCameraData();
            if (cameraData == null) return; // камера не под URP (не должно случаться в этом проекте) — тихо не делаем ничего
            cameraData.renderPostProcessing = true;
        }

        private static void CreateGlobalVolume()
        {
            var host = new GameObject(nameof(ScenePostProcessing) + "_Volume");
            DontDestroyOnLoad(host);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = BloomThreshold;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = BloomIntensity;

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = VignetteIntensity;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = VignetteSmoothness;

            var colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.saturation.value = SaturationBoost;

            var volume = host.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.weight = 1f;
            volume.profile = profile;
        }
    }
}
