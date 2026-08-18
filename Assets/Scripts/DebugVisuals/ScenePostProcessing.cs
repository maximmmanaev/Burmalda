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

        // Процедурное небо (2026-08-18, "ещё больше процедурного полиша без
        // арта") — встроенный шейдер Unity "Skybox/Procedural", без единой
        // текстуры. Тёмный, приглушённый — под тон "greed survival", не
        // отвлекает от тоннеля. Экспозиция занижена (дефолт — 1.3), чтобы
        // небо не забивало Bloom.
        private static readonly Color SkyTint = new Color(0.35f, 0.32f, 0.4f);
        private static readonly Color SkyGroundColor = new Color(0.08f, 0.07f, 0.08f);
        private const float SkyExposure = 0.6f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(ScenePostProcessing));
            host.AddComponent<ScenePostProcessing>();
            DontDestroyOnLoad(host);

            SetupProceduralSky();
        }

        private static void SetupProceduralSky()
        {
            // Shader.Find может вернуть null при шейдер-стриппинге на
            // устройстве (см. TunnelDebugVisual, тот же класс бага,
            // 2026-08-14) — BuildScript добавляет "Skybox/Procedural" в
            // Always Included Shaders, но защищаемся и здесь на случай
            // сборки в обход BuildScript.
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return;

            var skyMaterial = new Material(shader);
            skyMaterial.SetColor("_SkyTint", SkyTint);
            skyMaterial.SetColor("_GroundColor", SkyGroundColor);
            skyMaterial.SetFloat("_Exposure", SkyExposure);
            RenderSettings.skybox = skyMaterial;
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
