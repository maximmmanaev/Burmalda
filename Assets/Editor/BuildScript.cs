using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burmalda.EditorTools
{
    /// <summary>
    /// Сборка билда из командной строки (<c>Unity -batchmode -quit
    /// -executeMethod ...</c>) — для быстрой тестовой сборки на подключённое
    /// устройство без ручных кликов в Build Settings. Не привязано ни к
    /// одной игровой системе — чистая build-инфраструктура.
    /// </summary>
    public static class BuildScript
    {
        /// <summary>Собирает Android APK (не AAB — устанавливается напрямую через adb install) из сцен, включённых в Build Settings.</summary>
        public static void BuildAndroid()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            EditorUserBuildSettings.buildAppBundle = false;

            // Диагностика серого экрана на устройстве (2026-08-14): в
            // проекте был явно закреплён порядок Vulkan → OpenGLES3, автовыбор
            // выключен (m_Automatic: 0). Оставляем только OpenGLES3 — Vulkan+URP
            // известная причина серого/чёрного экрана на части Android GPU.
            // Не подтвердилось как единственная причина (гонка продолжилась
            // и с OpenGLES3), но само по себе безопасное, совместимое значение.
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);

            FixRenderingConfiguration();
            DisableSplashScreenForTesting();

            // Development build — задача явно требует диагностики через
            // adb logcat -s Unity: в non-development сборке Debug.Log почти
            // не долетает до logcat. Только BuildAndroid() (тестовая сборка
            // на подключённое устройство) — реальный релиз собирается отдельно.
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Builds/Android/Burmalda.apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });

            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        /// <summary>Диагностическая сборка под macOS Standalone — изолировать, проблема в сцене/пайплайне или специфична для Android-устройства.</summary>
        public static void BuildStandaloneOSX()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            FixRenderingConfiguration();
            DisableSplashScreenForTesting();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Builds/StandaloneOSX/Burmalda.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            });

            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        /// <summary>
        /// Сплэш-скрин Unity (2026-08-14): скриншот с устройства пиксель-в-
        /// пиксель совпал с настроенным m_SplashScreenBackgroundColor
        /// (0.137, 0.122, 0.125) — при этом Update() игровых скриптов уже
        /// тикает (см. лог) — сцена загружена и работает ПОД сплэшем, но он
        /// не убирается визуально. ТОЛЬКО для тестовых сборок — для реального
        /// релиза на Unity Personal лого обязательно по лицензии, здесь
        /// временно отключаем ради диагностики.
        /// </summary>
        private static void DisableSplashScreenForTesting()
        {
            PlayerSettings.SplashScreen.show = false;
        }

        /// <summary>
        /// Диагностика гарантированного серого/чёрного экрана на всех платформах
        /// (2026-08-14): единственное, что реально визуально представляет
        /// тоннель в игре — DebugVisuals.TunnelDebugVisual (создаёт материал
        /// через Shader.Find в рантайме) — ни один Material-ассет в проекте
        /// не ссылается на "Universal Render Pipeline/Lit" напрямую, поэтому
        /// build-time шейдер-стриппинг вырезает его целиком: игра не падает
        /// (TunnelDebugVisual теперь при этом самоотключается), но рисовать
        /// НЕЧЕГО. Плюс отдельно у PC_Renderer.asset (Standalone) сломан
        /// Renderer Feature ScreenSpaceAmbientOcclusion — "Couldn't find the
        /// required resources" в логе каждый кадр (подтверждено в
        /// ~/Library/Logs/DefaultCompany/BurmaldaUnity/Player.log).
        /// </summary>
        private static void FixRenderingConfiguration()
        {
            EnsureAlwaysIncludedShader("Universal Render Pipeline/Lit");
            // 2026-08-18: процедурное небо (DebugVisuals.ScenePostProcessing)
            // и партиклы сбора (DebugVisuals.PickupFeedback) — тот же риск
            // шейдер-стриппинга, что уже ловили на "Universal Render
            // Pipeline/Lit". PickupFeedback материал шейдер не запрашивает
            // (дефолт ParticleSystemRenderer) — добавлять нечего.
            EnsureAlwaysIncludedShader("Skybox/Procedural");
            // Задача «разрушение плиты» (2026-09-01): оверлей трещин
            // (TunnelDebugVisual.CreateCrackOverlayMaterial) намеренно взял
            // "Sprites/Default", а не "Universal Render Pipeline/Lit" —
            // но это ТОТ ЖЕ класс риска шейдер-стриппинга, что уже ловили
            // на "Universal Render Pipeline/Lit" 2026-08-14 (ни один
            // Material-ассет в проекте на него не ссылается — раньше и на
            // Sprites/Default тоже, в проекте нет ни одного SpriteRenderer).
            EnsureAlwaysIncludedShader("Sprites/Default");
            DisableBrokenRendererFeature("Assets/Settings/PC_Renderer.asset", "ScreenSpaceAmbientOcclusion");
        }

        private static void EnsureAlwaysIncludedShader(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"BuildScript: шейдер '{shaderName}' не найден даже в Editor — пропускаю добавление в Always Included Shaders.");
                return;
            }

            var graphicsSettings = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            var serializedObject = new SerializedObject(graphicsSettings);
            var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");

            for (var i = 0; i < arrayProp.arraySize; i++)
                if (arrayProp.GetArrayElementAtIndex(i).objectReferenceValue == shader) return; // уже есть — не дублируем

            arrayProp.InsertArrayElementAtIndex(arrayProp.arraySize);
            arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1).objectReferenceValue = shader;
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        private static void DisableBrokenRendererFeature(string rendererDataPath, string featureName)
        {
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(rendererDataPath);
            foreach (var subAsset in subAssets)
            {
                if (subAsset == null || subAsset.name != featureName) continue;

                var serializedFeature = new SerializedObject(subAsset);
                var activeProp = serializedFeature.FindProperty("m_Active");
                if (activeProp == null || !activeProp.boolValue) continue;

                activeProp.boolValue = false;
                serializedFeature.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
        }
    }
}
