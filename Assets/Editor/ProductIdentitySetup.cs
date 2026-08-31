using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Burmalda.EditorTools
{
    /// <summary>
    /// Задача 1, часть A: выставляет постоянный application identifier
    /// (заменяет дефолт Unity-шаблона <c>com.UnityTechnologies.com.unity.template.urpblank</c>)
    /// для Android/iOS + companyName (тоже был дефолтом Unity —
    /// <c>DefaultCompany</c>), одноразовый Editor-скрипт, запускается через
    /// <c>Unity -batchmode -quit -executeMethod
    /// Burmalda.EditorTools.ProductIdentitySetup.Run</c>. Не трогает .unity/
    /// .prefab (docs/rules/forbidden-actions.md) — только
    /// <c>ProjectSettings/ProjectSettings.asset</c> через официальный
    /// <see cref="PlayerSettings"/> API, не ручную правку YAML.
    ///
    /// <b>Идентификатор необратим после первой публикации</b> (Google
    /// Play/App Store не дают его сменить без потери установок/отзывов/
    /// рейтинга — только новое приложение) — значение согласовано с
    /// владельцем продукта ДО запуска этого скрипта, не выбрано автономно.
    /// </summary>
    public static class ProductIdentitySetup
    {
        // Согласовано с владельцем продукта (издатель "MaxMan" — reverse-DNS,
        // строчными буквами). Не менять без повторного согласования — см.
        // doc-комментарий класса про необратимость после публикации.
        private const string ApplicationIdentifier = "com.maxman.burmalda";
        private const string CompanyName = "MaxMan";

        public static void Run()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationIdentifier);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, ApplicationIdentifier);
            PlayerSettings.companyName = CompanyName;
            // productName уже кастомизирован ("BurmaldaUnity", не дефолт
            // Unity-шаблона) — задача просит трогать только дефолтные поля,
            // этого не трогаем.

            AssetDatabase.SaveAssets();
            Debug.Log($"ProductIdentitySetup: applicationIdentifier={ApplicationIdentifier}, companyName={CompanyName}");
        }
    }
}
