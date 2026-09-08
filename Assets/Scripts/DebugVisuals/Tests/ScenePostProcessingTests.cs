using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burmalda.DebugVisuals.Tests
{
    /// <summary>
    /// Баг с устройства (владелец, 2026-09-05, «эту виньетку я вижу до сих
    /// пор») — см. doc-комментарий <see cref="ScenePostProcessing"/>. Второй,
    /// независимый <see cref="Volume"/> в <c>SampleScene.unity</c> (стоковый
    /// профиль Unity-шаблона) действовал наравне с созданным кодом — этот
    /// тест не может прочитать саму сцену (EditMode, без загрузки .unity),
    /// но воспроизводит инвариант напрямую: любой <see cref="Volume"/>,
    /// уже существующий в сцене на момент инициализации, обязан быть
    /// выключен, и после инициализации в сцене остаётся ровно один
    /// включённый <see cref="Volume"/> — созданный этим классом.
    /// </summary>
    public class ScenePostProcessingTests
    {
        private GameObject _cameraObject;
        private GameObject _foreignVolumeObject;
        private GameObject _postProcessingObject;

        [TearDown]
        public void TearDown()
        {
            if (_cameraObject != null) Object.DestroyImmediate(_cameraObject);
            if (_foreignVolumeObject != null) Object.DestroyImmediate(_foreignVolumeObject);
            if (_postProcessingObject != null) Object.DestroyImmediate(_postProcessingObject);

            // CreateGlobalVolume создаёт СВОЙ host GameObject
            // ("ScenePostProcessing_Volume") — на него нет локальной ссылки
            // (создаётся внутри приватного статического метода), подчищаем
            // по фактическому типу компонента, чтобы не оставлять Volume в
            // сцене между тестами.
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                Object.DestroyImmediate(volume.gameObject);
        }

        [Test]
        public void Update_ForeignVolumeAlreadyInScene_DisablesItAndEndsWithExactlyOneEnabledVolume()
        {
            _cameraObject = new GameObject("ScenePostProcessingTests_Camera");
            _cameraObject.AddComponent<Camera>();

            // Симулирует "Global Volume"/SampleSceneProfile.asset, найденный
            // в реальной SampleScene.unity — заводской мусор, не заведённый
            // этим классом, но isGlobal/enabled так же, как и он.
            _foreignVolumeObject = new GameObject("ForeignVolume (stock template)");
            var foreignVolume = _foreignVolumeObject.AddComponent<Volume>();
            foreignVolume.isGlobal = true;
            foreignVolume.weight = 1f;
            Assert.IsTrue(foreignVolume.enabled, "Проверка условия теста — Volume создаётся включённым по умолчанию.");

            _postProcessingObject = new GameObject(nameof(ScenePostProcessing));
            var postProcessing = _postProcessingObject.AddComponent<ScenePostProcessing>();

            // Update() — тот же путь, что реальный кадр после того, как
            // ленивый поиск камеры (см. её doc-комментарий) её находит:
            // включает пост-обработку на камере и создаёт собственный Volume.
            InvokePrivate(postProcessing, "Update");

            Assert.IsFalse(foreignVolume.enabled, "Сторонний Volume, уже бывший в сцене, должен быть выключен.");

            var enabledVolumes = 0;
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                if (volume.enabled) enabledVolumes++;

            Assert.AreEqual(1, enabledVolumes, "После инициализации активен ровно один Volume — созданный кодом.");
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} не найден рефлексией — сигнатура/имя изменились?");
            method.Invoke(target, args);
        }
    }
}
