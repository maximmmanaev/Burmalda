using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Поднимает уровень отладочного визуала без единого арт-ассета (прямой
    /// запрос владельца продукта, 2026-08-18) — только встроенные
    /// инструменты URP: Bloom через рантайм-созданный
    /// <see cref="Volume"/>/<see cref="VolumeProfile"/>, процедурное небо и
    /// туман (issue #192, скрывает обрыв геометрии раскрытых рядов, см.
    /// <see cref="SetupFog"/>) через <c>RenderSettings</c>. Никаких
    /// .asset-файлов не создаётся — профиль существует только в памяти
    /// сессии, как и материалы <see cref="TunnelDebugVisual"/>.
    ///
    /// Самостоятельно создаёт себя через <see cref="RuntimeInitializeOnLoadMethodAttribute"/>
    /// — без правки .unity/.prefab (docs/rules/forbidden-actions.md), тот же
    /// паттерн, что <see cref="RestartButton"/>. Свет в сцене уже настроен
    /// (мягкие тени, intensity 2) — не трогается, кроме одной правки
    /// направления (см. <see cref="FixDirectionalLightYaw"/>), здесь в
    /// остальном только пост-обработка.
    ///
    /// <b>Баг с устройства (владелец, 2026-09-05, «тень справа»):</b>
    /// виньетка уже убрана (см. <see cref="CreateGlobalVolume"/>), но
    /// односторонняя тень осталась — виньетка затемняет края СИММЕТРИЧНО,
    /// значит причина другая. Диагностика (не гипотеза — прочитан
    /// <c>Assets/Scenes/SampleScene.unity</c>, единственная сцена в
    /// <c>ProjectSettings/EditorBuildSettings.asset</c>): единственный Light
    /// сцены ("Directional Light") имеет <c>m_LocalEulerAnglesHint: {x: 50,
    /// y: -30, z: 0}</c> — тангаж (50°) совпадает с камерой
    /// (<c>TunnelCameraFollow.DefaultPitchDegrees</c>), НО рыскание -30°
    /// ничем не оправдано и не совпадает ни с одной другой геометрией сцены.
    /// Тоннель и его стены (<see cref="TunnelDebugVisual.CreateWallSegmentsForRow"/>)
    /// строго симметричны относительно оси Z (стены на -halfWidth/+halfWidth,
    /// один и тот же материал) — ненулевое рыскание даёт свету
    /// X-составляющую направления, из-за которой одна стена получает
    /// прямой свет, а другая (и пол рядом с ней) — идёт в тень: не
    /// артефакт пост-обработки и не асимметрия геометрии (геометрия
    /// симметрична по построению), а асимметрия ЕДИНСТВЕННОГО источника
    /// света относительно симметричной сцены. Правка .unity автономно
    /// запрещена (docs/rules/forbidden-actions.md) — рыскание обнуляется в
    /// рантайме (тангаж/крен не трогаются) тем же самобутстрапом, что и
    /// остальной класс.
    /// </summary>
    public sealed class ScenePostProcessing : MonoBehaviour
    {
        // Черновые значения "на глазок" — не PRD, не баланс, чисто
        // визуальный тюнинг debug-уровня. Порог и интенсивность подобраны,
        // чтобы яркие плоские цвета TileDebugColor чуть "светились", не
        // засвечивая экран целиком.
        private const float BloomThreshold = 0.9f;
        private const float BloomIntensity = 0.6f;
        private const float SaturationBoost = 15f;

        // Процедурное небо (2026-08-18, "ещё больше процедурного полиша без
        // арта") — встроенный шейдер Unity "Skybox/Procedural", без единой
        // текстуры. Тёмный, приглушённый — под тон "greed survival", не
        // отвлекает от тоннеля. Экспозиция занижена (дефолт — 1.3), чтобы
        // небо не забивало Bloom.
        private static readonly Color SkyTint = new Color(0.35f, 0.32f, 0.4f);
        private static readonly Color SkyGroundColor = new Color(0.08f, 0.07f, 0.08f);
        private const float SkyExposure = 0.6f;

        // issue #192 (Спринт 12b): туман вместо обрыва геометрии — сейчас
        // видимый горизонт тоннеля обрывается последней материализованной
        // плитой (TunnelGridReveal/SegmentRowProvider.RowsAheadOfPlayer=8),
        // читается как обрыв, не как художественное решение.
        // RenderSettings.fog — тот же путь, что стандартный Fog в
        // Lighting > Environment, URP учитывает его без отдельной настройки
        // Volume-профиля (в отличие от Bloom/Vignette выше). Тёплый тёмный
        // тон — тот же дух, что WallColor у TunnelDebugVisual, не
        // нейтральный серый.
        //
        // Расстояния — геометрия камеры, не подбор на глаз: камера без
        // Z-смещения от текущей позиции игрока (TunnelCameraController.
        // _heightOffset по умолчанию (0,6,0)), тангаж 50°
        // (TunnelCameraFollow.DefaultPitchDegrees). По прямой от камеры до
        // границы гарантированно раскрытых рядов (RowsAheadOfPlayer=8 рядов
        // вперёд, TileSize=1) — sqrt(6² + 8²) = 10 мировых единиц
        // (треугольник 6-8-10).
        //
        // Найдено на реальном Android-билде (2026-09-03): первая попытка
        // (Start=11, End=17 — оба СТРОГО за границей 10) не дала видимого
        // эффекта вообще — за последним материализованным рядом плит
        // физически нет, там пусто (скайбокс), а туман окрашивает только
        // отрисованную геометрию, не пустоту. Граница читалась тем же
        // резким обрывом, что и без тумана. Старт сдвинут чуть БЛИЖЕ самой
        // границы (не за неё) — последний ряд получает лёгкую дымку
        // (~30% на границе), различимость плиты не теряется (PRD 4.2:
        // честная разведка), полная непрозрачность — сразу за границей, где
        // геометрии всё равно уже нет, скрывая сам факт обрыва.
        //
        // Владелец, 2026-09-05 («туман — дальше и плотнее»): подтверждено
        // на устройстве, что 9.5/11 работает (дымка видна, обрыв не
        // читается), но начало попросили отодвинуть примерно на одну плиту
        // дальше и убедиться, что за границей не просвечивает НИЧЕГО. Стены
        // тоннеля (TunnelDebugVisual.CreateWallMaterial) используют тот же
        // CreateTemplateMaterial(), что и обычные плиты пола (тот же
        // шейдер "Universal Render Pipeline/Lit", уже в Always Included
        // Shaders и с фог-вариантами, не вырезаемыми стриппингом —
        // BuildScript.EnsureFogShaderVariantsNotStripped) — стены гаснут в
        // тумане тем же градиентом, что и пол, отдельного шейдера под них
        // нет и заводить не пришлось.
        private const float FogStartDistance = 10.5f;
        private const float FogEndDistance = 12f;
        private static readonly Color FogColor = new Color(30f / 255f, 20f / 255f, 14f / 255f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(ScenePostProcessing));
            host.AddComponent<ScenePostProcessing>();
            DontDestroyOnLoad(host);

            SetupProceduralSky();
            SetupFog();
            FixDirectionalLightYaw();
        }

        /// <summary>
        /// Баг с устройства (владелец, 2026-09-05, «тень справа») — см.
        /// doc-комментарий класса для полной диагностики. Обнуляет ТОЛЬКО
        /// рыскание (компонент Y поворота) единственного направленного
        /// света сцены, оставляя тангаж/крен как задал владелец продукта —
        /// это устраняет именно асимметрию (свет бьёт в обе стены тоннеля
        /// под одним и тем же углом), не убирает свет/тени целиком.
        /// Не-op, если направленного света на сцене нет (не должно
        /// случаться в этом проекте, но тот же оборонительный принцип, что
        /// у отсутствующего шейдера/камеры в этом классе) или его рыскание
        /// уже нулевое.
        /// </summary>
        private static void FixDirectionalLightYaw()
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;

                var euler = light.transform.eulerAngles;
                if (Mathf.Approximately(euler.y, 0f)) continue;

                light.transform.eulerAngles = new Vector3(euler.x, 0f, euler.z);
            }
        }

        private static void SetupFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = FogStartDistance;
            RenderSettings.fogEndDistance = FogEndDistance;
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

            // Виньетка убрана целиком (владелец, 2026-09-05, «убрать
            // виньетку») — тень по краям экрана мешала, Bloom остаётся.
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
