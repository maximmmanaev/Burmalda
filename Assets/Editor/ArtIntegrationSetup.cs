using System.Linq;
using Burmalda.DebugVisuals;
using UnityEditor;
using UnityEngine;

namespace Burmalda.EditorTools
{
    /// <summary>
    /// Одноразовая (переиспользуемая при повторном запуске — идемпотентна)
    /// настройка импорта первой партии Scenario-арта (issue "Завести арт в
    /// игру"): Texture Import Settings, каталог текстур плит
    /// (<see cref="TileArtCatalog"/>) и иконка приложения. Запускается через
    /// <c>Unity -batchmode -quit -executeMethod
    /// Burmalda.EditorTools.ArtIntegrationSetup.Run</c> — не требует ручных
    /// кликов в инспекторе и не трогает .unity/.prefab
    /// (docs/rules/forbidden-actions.md): единственный новый ассет,
    /// который создаёт этот скрипт, — <see cref="TileArtCatalog"/>
    /// (.asset, не сцена/префаб).
    /// </summary>
    public static class ArtIntegrationSetup
    {
        private const string TilesFolder = "Assets/Art/Tiles";
        private const string CurrenciesFolder = "Assets/Art/Icons/Currencies";
        private const string ArtifactsFolder = "Assets/Art/Icons/Artifacts";
        private const string OmensFolder = "Assets/Art/Cards/Omens";
        private const string AppIconFolder = "Assets/Art/AppIcon";

        private const string TileArtCatalogPath = "Assets/Resources/Art/TileArtCatalog.asset";

        public static void Run()
        {
            ApplySpriteImportSettings(CurrenciesFolder);
            ApplySpriteImportSettings(ArtifactsFolder);
            ApplySpriteImportSettings(OmensFolder);
            ApplySpriteImportSettings(AppIconFolder);

            BuildTileArtCatalog();
            SetApplicationIcon();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Sprite (2D and UI) + Alpha Is Transparency — шаг 1 задачи, для
        /// всего, что не тайл тоннеля (тайлы остаются Default, см.
        /// doc-комментарий <see cref="BuildTileArtCatalog"/>). Идемпотентна:
        /// повторный вызов на уже настроенных текстурах — не-op
        /// (SaveAndReimport не пересобирает без изменений параметров).
        /// </summary>
        private static void ApplySpriteImportSettings(string folder)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// Тайлы (<c>Assets/Art/Tiles</c>) сознательно оставлены Default
        /// (Texture Type), не Sprite — <see cref="DebugVisuals.TunnelDebugVisual"/>
        /// накладывает их через <c>Renderer.material.mainTexture</c> на
        /// 3D-примитив (Cube), не через <c>SpriteRenderer</c>; Default —
        /// более прямой путь для этого случая (шаг 1 задачи явно оставляет
        /// выбор на усмотрение исполнителя).
        ///
        /// Заполняет <see cref="TileArtCatalog"/> под <c>Resources/</c> (см.
        /// её doc-комментарий, почему именно Resources) ссылками на
        /// текстуры по фиксированному маппингу <see cref="TileArtKind"/> →
        /// имя файла.
        ///
        /// <b>Задача «тёплый набор плит»:</b> каждой категории, у которой в
        /// новом тёплом наборе появился собственный файл, — своя текстура
        /// (источники, рычаг, ворота открыты/закрыты, Алтарь, позиция
        /// игрока, обе сигнатуры). Яма и активированный взрыв (issue #163)
        /// по-прежнему делят ОДНУ текстуру — теперь выделенный
        /// <c>tile-hidden-trap-signature</c> (раньше сюда подключался
        /// <c>tile-pit</c> за неимением специального файла); то же для
        /// триггеров — выделенный <c>tile-trigger-signature</c> вместо
        /// прежнего <c>tile-explosive-trigger</c>. Комнаты Босса в Unity ещё
        /// нет (PRD v8) — под неё текстуры по-прежнему нет, остаётся на
        /// цветном фолбэке. <c>tile-start</c> не вошла в тёплый лист —
        /// владелец: использовать <c>tile-fresh</c> как временную замену.
        /// </summary>
        private static void BuildTileArtCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<TileArtCatalog>(TileArtCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<TileArtCatalog>();
                EnsureFolder("Assets/Resources");
                EnsureFolder("Assets/Resources/Art");
                AssetDatabase.CreateAsset(catalog, TileArtCatalogPath);
            }

            catalog.EditorAssign(
                fresh: LoadTile("tile-fresh"),
                freshVariantB: LoadTile("tile-fresh-b"),
                freshVariantC: LoadTile("tile-fresh-c"),
                halfDecayed: LoadTile("tile-half-decayed"),
                aboutToDecay: LoadTile("tile-about-to-decay"),
                destroyed: LoadTile("tile-destroyed"),
                start: LoadTile("tile-fresh"),
                blocked: LoadTile("tile-blocked"),
                lava: LoadTile("tile-lava"),
                hiddenTrapSignature: LoadTile("tile-hidden-trap-signature"),
                timedTrapActive: LoadTile("tile-timed-trap-active"),
                triggerSignature: LoadTile("tile-trigger-signature"),
                currentPosition: LoadTile("tile-current-position"),
                manaSource: LoadTile("tile-mana-source"),
                keySource: LoadTile("tile-key-source"),
                lever: LoadTile("tile-lever"),
                gateClosed: LoadTile("tile-gate-closed"),
                gateOpen: LoadTile("tile-gate-open"),
                altar: LoadTile("tile-altar"));

            EditorUtility.SetDirty(catalog);
        }

        private static Texture2D LoadTile(string fileName) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{TilesFolder}/{fileName}.png");

        /// <summary>
        /// Шаг 5 задачи: Player Settings → Icon. Ставит один квадратный PNG
        /// как default application icon (BuildTargetGroup.Unknown) — Unity
        /// сам генерирует нужные размеры для платформ без переопределения
        /// (тот же слот, что в задаче описан как "Player Settings → Icon").
        /// app-icon.png импортирован как Sprite — сама текстура лежит
        /// суб-ассетом на этом пути, поэтому LoadAllAssetsAtPath, а не
        /// LoadAssetAtPath&lt;Texture2D&gt; (тот вернул бы null для главного
        /// объекта типа Sprite).
        /// </summary>
        private static void SetApplicationIcon()
        {
            const string path = AppIconFolder + "/app-icon.png";
            var texture = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>().FirstOrDefault();
            if (texture == null)
            {
                Debug.LogWarning($"ArtIntegrationSetup: не найден Texture2D по пути {path} — иконка приложения не выставлена.");
                return;
            }

            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { texture });
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0, path.LastIndexOf('/'));
            var newFolderName = path.Substring(path.LastIndexOf('/') + 1);
            AssetDatabase.CreateFolder(parent, newFolderName);
        }
    }
}
