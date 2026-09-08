using System;
using System.Linq;
using Burmalda.DebugVisuals;
using UnityEditor;
using UnityEngine;

namespace Burmalda.EditorTools
{
    /// <summary>Imports the Blender-generated tomb kit without touching scenes or prefabs.</summary>
    public static class TombEnvironmentIntegrationSetup
    {
        private const string ModelPath = "Assets/Art/Tomb/Models/burmalda-tomb-modular.fbx";
        private const string TextureFolder = "Assets/Art/Tomb/Textures/";
        private const string MaterialPath = "Assets/Art/Tomb/Materials/TombSandstone.mat";
        private const string CatalogPath = "Assets/Resources/Art/TombEnvironmentCatalog.asset";

        [MenuItem("Burmalda/Art/Build Tomb Environment Assets")]
        public static void Run()
        {
            ConfigureModelImporter();
            var baseColor = ConfigureTexture("tomb-sandstone-basecolor.png", TextureImporterType.Default, true);
            var normal = ConfigureTexture("tomb-sandstone-normal.png", TextureImporterType.NormalMap, false);
            var mask = ConfigureTexture("tomb-sandstone-mask.png", TextureImporterType.Default, false);
            var material = BuildMaterial(baseColor, normal, mask);
            BuildCatalog(material);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Burmalda/Art/Create Tomb Module Preview (Unsaved)")]
        public static void CreatePreview()
        {
            Run();
            var catalog = AssetDatabase.LoadAssetAtPath<TombEnvironmentCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidOperationException("TombEnvironmentCatalog could not be loaded.");

            var root = new GameObject("Tomb Module Preview (unsaved)");
            var floors = new[]
            {
                catalog.FloorIntact, catalog.FloorWorn, catalog.FloorCracked,
                catalog.FloorWorn, catalog.FloorIntact, catalog.FloorCracked,
            };
            for (var index = 0; index < floors.Length; index++)
            {
                CreateMeshObject(
                    $"Preview {floors[index].name} {index}",
                    floors[index],
                    catalog.SharedMaterial,
                    root.transform,
                    new Vector3(index % 3 - 1f, 0f, index / 3f));
            }

            for (var index = 0; index < 3; index++)
            {
                var wall = CreateMeshObject(
                    $"Preview Wall Straight {index}",
                    catalog.WallStraight,
                    catalog.SharedMaterial,
                    root.transform,
                    new Vector3(index - 1f, 0f, 2f));
                wall.transform.rotation = Quaternion.identity;
            }

            CreateMeshObject(
                "Preview Wall Corner",
                catalog.WallCorner,
                catalog.SharedMaterial,
                root.transform,
                new Vector3(2f, 0f, 1f));
            Selection.activeGameObject = root;
            Undo.RegisterCreatedObjectUndo(root, "Create tomb module preview");
        }

        private static void ConfigureModelImporter()
        {
            if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter importer))
                throw new InvalidOperationException($"Model not found: {ModelPath}");

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.SaveAndReimport();
        }

        private static Texture2D ConfigureTexture(string fileName, TextureImporterType type, bool srgb)
        {
            var path = TextureFolder + fileName;
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                throw new InvalidOperationException($"Texture not found: {path}");

            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = 512,
                format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = 50,
            });
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material BuildMaterial(Texture2D baseColor, Texture2D normal, Texture2D mask)
        {
            EnsureFolder("Assets/Art/Tomb/Materials");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP/Lit shader is unavailable.");
                material = new Material(shader) { name = "TombSandstone" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.SetTexture("_BaseMap", baseColor);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 0.35f);
            material.SetTexture("_MetallicGlossMap", mask);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.18f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildCatalog(Material material)
        {
            var meshes = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>().ToDictionary(mesh => mesh.name);
            Mesh Require(string name) => meshes.TryGetValue(name, out var mesh)
                ? mesh
                : throw new InvalidOperationException($"Mesh '{name}' not found in {ModelPath}.");

            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Art");
            var catalog = AssetDatabase.LoadAssetAtPath<TombEnvironmentCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<TombEnvironmentCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.EditorAssign(
                Require("Floor_Intact"),
                Require("Floor_Worn"),
                Require("Floor_Cracked"),
                Require("Wall_Straight"),
                Require("Wall_Corner"),
                material);
            EditorUtility.SetDirty(catalog);
        }

        private static GameObject CreateMeshObject(
            string name,
            Mesh mesh,
            Material material,
            Transform parent,
            Vector3 position)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return gameObject;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var separator = path.LastIndexOf('/');
            var parent = path.Substring(0, separator);
            var folder = path.Substring(separator + 1);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
