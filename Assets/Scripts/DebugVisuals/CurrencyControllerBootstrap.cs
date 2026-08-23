using Burmalda.Currencies;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// <see cref="CurrencyController"/> ни разу не был размещён в
    /// <c>SampleScene.unity</c> вручную (обнаружено на устройстве при
    /// проверке задачи 2 — HUD показывал пустое место вместо чисел; сверено
    /// по GUID компонента напрямую в YAML сцены, не по текстовому имени) —
    /// валюты нигде в реальной игре не считались. Разместить компонент
    /// через .unity нельзя автономно (docs/rules/forbidden-actions.md),
    /// поэтому — самобутстрап тем же паттерном ленивого поиска в
    /// <see cref="Update"/>, что <see cref="TilePreviewController"/>/
    /// <see cref="PickupFeedback"/>.
    ///
    /// <b>Не создаёт отдельный DontDestroyOnLoad-хост для самого
    /// <see cref="CurrencyController"/></b> (в отличие от большинства
    /// классов в этом файле) — добавляет его КОМПОНЕНТОМ на ТОТ ЖЕ
    /// GameObject, где уже есть <see cref="GridTraceInputController"/>:
    /// <see cref="CurrencyController.Awake"/> сам находит соседа через
    /// <c>GetComponent&lt;GridTraceInputController&gt;()</c>, если поле не
    /// заполнено в инспекторе — точно так же, как если бы его перетащили на
    /// тот же объект руками. Дальше <see cref="CurrencyController"/> живёт
    /// полностью по своей уже написанной и покрытой тестами логике
    /// (<c>RunStarted</c>/<c>RebuildRunSystems</c>) — этот класс её не
    /// трогает, только один раз создаёт компонент и уничтожает себя.
    /// </summary>
    public sealed class CurrencyControllerBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(CurrencyControllerBootstrap));
            host.AddComponent<CurrencyControllerBootstrap>();
            DontDestroyOnLoad(host);
        }

        private void Update()
        {
            // Ленивый поиск — порядок Awake между объектами сцены не
            // гарантирован, тот же принцип, что у остальных debug-driver'ов.
            var input = FindFirstObjectByType<GridTraceInputController>();
            if (input == null) return;

            if (input.GetComponent<CurrencyController>() == null)
                input.gameObject.AddComponent<CurrencyController>();

            Destroy(gameObject); // сделано — дальше CurrencyController живёт сам по своему жизненному циклу
        }
    }
}
