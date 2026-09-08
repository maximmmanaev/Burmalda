using System.Collections.Generic;
using System.Linq;
using Burmalda.Core;
using Burmalda.Movement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Burmalda.DebugVisuals.Tests
{
    public sealed class TombEnvironmentAssetTests
    {
        private const string ModelPath = "Assets/Art/Tomb/Models/burmalda-tomb-modular.fbx";
        private const string MaterialPath = "Assets/Art/Tomb/Materials/TombSandstone.mat";
        private const string CatalogPath = "Assets/Resources/Art/TombEnvironmentCatalog.asset";
        private const string TextureFolder = "Assets/Art/Tomb/Textures/";

        private static readonly string[] RequiredMeshNames =
        {
            "Floor_Intact",
            "Floor_Worn",
            "Floor_Cracked",
            "Wall_Straight",
            "Wall_Corner",
        };

        [Test]
        public void RequiredModels_ImportWithValidGeometryAndMobileBudgets()
        {
            var meshes = LoadMeshes();

            CollectionAssert.IsSubsetOf(RequiredMeshNames, meshes.Keys, "FBX должен содержать весь модульный набор гробницы.");
            foreach (var meshName in RequiredMeshNames)
            {
                var mesh = meshes[meshName];
                Assert.Greater(mesh.vertexCount, 0, $"{meshName}: меш пуст.");
                Assert.AreEqual(mesh.vertexCount, mesh.normals.Length, $"{meshName}: отсутствуют валидные нормали.");
                Assert.AreEqual(mesh.vertexCount, mesh.uv.Length, $"{meshName}: отсутствует UV0.");
                Assert.LessOrEqual(mesh.vertexCount, 1500, $"{meshName}: превышен мобильный лимит вершин.");
                Assert.LessOrEqual(mesh.triangles.Length / 3, 2500, $"{meshName}: превышен мобильный лимит треугольников.");
            }

            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            Assert.IsNotNull(importer, "Модульный FBX не импортирован ModelImporter.");
            Assert.AreEqual(1f, importer.globalScale, 0.0001f, "FBX должен импортироваться в метрическом масштабе 1:1.");
            Assert.IsTrue(importer.isReadable, "Read/Write нужен интеграционным проверкам геометрии и bounds.");
            Assert.AreEqual(ModelImporterNormals.Import, importer.importNormals, "Нормали должны приходить из Blender.");
        }

        [Test]
        public void FloorModules_FillOneMeterGridCell_WithTopCenterPivot()
        {
            var meshes = LoadMeshes();
            foreach (var name in new[] { "Floor_Intact", "Floor_Worn", "Floor_Cracked" })
            {
                var bounds = meshes[name].bounds;
                Assert.AreEqual(1f, bounds.size.x, 0.002f, $"{name}: ширина должна равняться TileSize=1.");
                Assert.AreEqual(1f, bounds.size.z, 0.002f, $"{name}: глубина должна равняться TileSize=1.");
                Assert.AreEqual(0f, bounds.max.y, 0.002f, $"{name}: pivot должен лежать в центре верхней поверхности.");
                Assert.AreEqual(-0.12f, bounds.min.y, 0.005f, $"{name}: ожидается умеренная толщина мобильной плиты.");
                Assert.AreEqual(-0.5f, bounds.min.x, 0.002f);
                Assert.AreEqual(0.5f, bounds.max.x, 0.002f);
                Assert.AreEqual(-0.5f, bounds.min.z, 0.002f);
                Assert.AreEqual(0.5f, bounds.max.z, 0.002f);
            }

            var left = meshes["Floor_Intact"].bounds;
            var right = meshes["Floor_Worn"].bounds;
            Assert.AreEqual(left.max.x, right.min.x + 1f, 0.002f, "Соседние плиты на сетке не должны оставлять геометрическую щель.");
        }

        [Test]
        public void WallModules_UseBottomPivot_AndOneMeterSegmentLength()
        {
            var meshes = LoadMeshes();
            var straight = meshes["Wall_Straight"].bounds;
            Assert.AreEqual(1f, straight.size.x, 0.002f);
            Assert.AreEqual(0.18f, straight.size.z, 0.005f);
            Assert.AreEqual(1.1f, straight.size.y, 0.005f);
            Assert.AreEqual(0f, straight.min.y, 0.002f, "Pivot стены должен находиться внизу по центру.");
            Assert.AreEqual(-0.5f, straight.min.x, 0.002f);
            Assert.AreEqual(0.5f, straight.max.x, 0.002f);

            var corner = meshes["Wall_Corner"].bounds;
            Assert.AreEqual(1f, corner.size.x, 0.005f);
            Assert.AreEqual(1f, corner.size.z, 0.005f);
            Assert.AreEqual(1.1f, corner.size.y, 0.005f);
            Assert.AreEqual(0f, corner.min.y, 0.002f);
        }

        [Test]
        public void SharedMaterial_IsUrpCompatible_AndTexturesFitAndroidBudget()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(material, "Общий материал гробницы отсутствует.");
            StringAssert.StartsWith("Universal Render Pipeline/", material.shader.name);
            Assert.IsTrue(material.enableInstancing, "Общий материал должен поддерживать GPU instancing.");
            Assert.IsNotNull(material.GetTexture("_BaseMap"), "Base Color не назначен.");
            Assert.IsNotNull(material.GetTexture("_BumpMap"), "Normal Map не назначен.");
            Assert.IsNotNull(material.GetTexture("_MetallicGlossMap"), "URP Mask Map не назначена.");

            AssertTexture("tomb-sandstone-basecolor.png", TextureImporterType.Default, true);
            AssertTexture("tomb-sandstone-normal.png", TextureImporterType.NormalMap, false);
            AssertTexture("tomb-sandstone-mask.png", TextureImporterType.Default, false);
        }

        [Test]
        public void Catalog_HasNoMissingAssetReferences()
        {
            var catalog = AssetDatabase.LoadMainAssetAtPath(CatalogPath);
            Assert.IsNotNull(catalog, "TombEnvironmentCatalog.asset отсутствует.");

            var serialized = new SerializedObject(catalog);
            foreach (var propertyName in new[] { "_floorIntact", "_floorWorn", "_floorCracked", "_wallStraight", "_wallCorner", "_sharedMaterial" })
            {
                var property = serialized.FindProperty(propertyName);
                Assert.IsNotNull(property, $"В каталоге отсутствует поле {propertyName}.");
                Assert.IsNotNull(property.objectReferenceValue, $"В каталоге потеряна ссылка {propertyName}.");
            }
        }

        [Test]
        public void TunnelVisual_UsesModularFloorAndWall_WithoutWallColliders()
        {
            var parent = new GameObject("TombEnvironmentAssetTests");
            TunnelDebugVisual visual = null;
            try
            {
                var grid = new TunnelGrid(2);
                var trail = new GridTraceTrail(grid, new GridCoordinate(0, 0));
                visual = new TunnelDebugVisual(grid, trail, new WorldGridProjection(1f, 2), parent.transform);
                Assert.IsTrue(visual.IsEnabled, "В EditMode должен быть доступен URP/Lit или резервный шейдер.");

                grid.GetOrCreateTile(new GridCoordinate(0, 1));
                var tiles = Descendants(parent.transform).Where(t => t.name.StartsWith("DebugTile ")).ToArray();
                Assert.AreEqual(2, tiles.Length);
                foreach (var tile in tiles)
                {
                    StringAssert.StartsWith("Floor_", tile.GetComponent<MeshFilter>().sharedMesh.name);
                    var collider = tile.GetComponent<BoxCollider>();
                    Assert.IsNotNull(collider, "Тап по плите требует BoxCollider.");
                    Assert.IsNotNull(tile.GetComponent<TileVisualMarker>(), "Тап по плите требует TileVisualMarker.");
                    Assert.AreEqual(1f, collider.size.x, 0.002f);
                    Assert.AreEqual(1f, collider.size.z, 0.002f);
                }

                var walls = Descendants(parent.transform).Where(t => t.name.StartsWith("DebugWall")).ToArray();
                Assert.AreEqual(2, walls.Length, "На один ряд должно создаваться по одной стене с каждой стороны.");
                foreach (var wall in walls)
                {
                    Assert.AreEqual("Wall_Straight", wall.GetComponent<MeshFilter>().sharedMesh.name);
                    Assert.IsNull(wall.GetComponent<Collider>(), "Декоративные стены не должны участвовать в тапах/физике.");
                    Assert.AreEqual(
                        1.15f,
                        Mathf.Abs(wall.position.x),
                        0.002f,
                        "После merge #247 центр модульной стены должен соблюдать общий внешний отступ 0.15 клетки.");
                }
            }
            finally
            {
                visual?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void BlockedObstruction_RestsOnImportedFloor_AndDisposesInEditMode()
        {
            var parent = new GameObject("TombBlockedObstructionTests");
            TunnelDebugVisual visual = null;
            try
            {
                var grid = new TunnelGrid(2);
                var coordinate = new GridCoordinate(0, 0);
                var trail = new GridTraceTrail(grid, coordinate);
                visual = new TunnelDebugVisual(grid, trail, new WorldGridProjection(1f, 2), parent.transform);

                grid.GetOrCreateTile(coordinate).TransitionToBlocked();
                visual.Tick(0f);

                var obstruction = Descendants(parent.transform).Single(t => t.name.StartsWith("DebugBlockedObstruction "));
                Assert.IsNull(obstruction.GetComponent<Collider>(), "Препятствие не должно перехватывать тап по плите.");
                Assert.AreEqual(
                    0f,
                    obstruction.GetComponent<Renderer>().bounds.min.y,
                    0.002f,
                    "Препятствие должно стоять на верхней поверхности импортированной плиты без зазора.");
            }
            finally
            {
                visual?.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        private static Dictionary<string, Mesh> LoadMeshes()
        {
            var meshes = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>().ToDictionary(mesh => mesh.name);
            Assert.IsNotEmpty(meshes, $"Модульный FBX не найден или не содержит мешей: {ModelPath}");
            return meshes;
        }

        private static void AssertTexture(string fileName, TextureImporterType expectedType, bool expectedSrgb)
        {
            var path = TextureFolder + fileName;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(texture, $"Не найдена текстура {path}");
            Assert.LessOrEqual(texture.width, 512);
            Assert.LessOrEqual(texture.height, 512);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(importer);
            Assert.AreEqual(expectedType, importer.textureType);
            Assert.AreEqual(expectedSrgb, importer.sRGBTexture);
            Assert.LessOrEqual(importer.maxTextureSize, 512);
            var android = importer.GetPlatformTextureSettings("Android");
            Assert.IsTrue(android.overridden, $"{fileName}: нужны явные Android import settings.");
            Assert.LessOrEqual(android.maxTextureSize, 512);
        }

        private static IEnumerable<Transform> Descendants(Transform root)
        {
            foreach (Transform child in root)
            {
                yield return child;
                foreach (var descendant in Descendants(child)) yield return descendant;
            }
        }
    }
}
