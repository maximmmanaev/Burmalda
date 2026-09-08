using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Тонкий driver debug-визуала тоннеля (issue #58) — создаёт/обновляет
    /// примитивы плит по мере продвижения трейла и хода распада того же
    /// забега, что и <see cref="GridTraceInputController"/>. Debug-инфраструктура
    /// для ручного тестирования, не финальный арт и не система из PRD.
    ///
    /// <b>Самобутстрап (владелец, 2026-09-08, «на сценах не остаётся
    /// ничего, кроме камеры и света»):</b> раньше жил компонентом рядом с
    /// <see cref="GridTraceInputController"/> на сцене (все поля были
    /// дефолтными/null — переносить было нечего) и находил её через
    /// <c>GetComponent</c>, требующий совместного размещения. Теперь создаёт
    /// себя сам на СВОЁМ отдельном GameObject, тот же паттерн, что
    /// <see cref="RestartButton"/>/<see cref="TilePreviewController"/> — не
    /// через <c>Bootstrap.RunBootstrap</c> (который добавляет
    /// <c>Decay.TrailDecayController</c>/<c>RunLifecycle.RunController</c>
    /// именно на GameObject <see cref="GridTraceInputController"/>): та
    /// сборка уже ссылается на <c>Burmalda.DebugVisuals</c>, обратная
    /// ссылка отсюда была бы циклической зависимостью сборок. Ищет
    /// <see cref="GridTraceInputController"/> глобально (см. её
    /// самобутстрап) — совместное размещение с ней этому классу не нужно,
    /// в отличие от <c>GetComponent</c>-цепочки Decay→Run: ничто не ищет
    /// <see cref="TunnelDebugVisualController"/> через <c>GetComponent</c>.
    /// Тайлы визуала переиспользуют <c>transform</c> этого объекта как
    /// родителя — их собственная мировая позиция выставляется явно (см.
    /// <c>TunnelDebugVisual.OnTileMaterialized</c>), позиция родителя на
    /// это не влияет.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TunnelDebugVisualController : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // См. doc-комментарий GridTraceInputController.Bootstrap() —
            // тот же баг с устройства, тот же принцип защиты: дубликат
            // этого класса создал бы ВТОРОЙ набор тайлов поверх первого
            // (не так фатально, как дубликат GridTraceInputController, но
            // всё равно мусор, раз уж защита нужна ГЛАВНОМУ якорю рядом).
            if (FindFirstObjectByType<TunnelDebugVisualController>() != null) return;

            var host = new GameObject(nameof(TunnelDebugVisualController));
            host.AddComponent<TunnelDebugVisualController>();
            DontDestroyOnLoad(host);
        }

        private GridTraceInputController _input;
        private TunnelDebugVisual _visual;

        private void OnDisable()
        {
            if (_input != null) _input.RunStarted -= HandleRunStarted;
            DisposeVisual();
        }

        private void Update()
        {
            // Ленивый поиск — тот же паттерн, что у остальных
            // самобутстрапящихся driver'ов проекта (RestartButton и т.п.):
            // порядок AfterSceneLoad между разными классами не гарантирован,
            // GridTraceInputController может ещё не существовать в момент,
            // когда этот компонент запускается.
            if (_input == null)
            {
                _input = FindFirstObjectByType<GridTraceInputController>();
                if (_input != null) _input.RunStarted += HandleRunStarted;
            }

            // Ленивая инициализация визуала — как раньше в Awake/Update:
            // Grid/Trail/Projection появляются только в Awake()
            // GridTraceInputController, порядок между разными
            // самобутстрапящимися компонентами не гарантирован — покрывает
            // первый запуск; рестарты (мир пересоздаётся заново, старые
            // примитивы больше не актуальны) приходят через HandleRunStarted.
            if (_visual == null)
            {
                if (_input == null || _input.Grid == null || _input.Trail == null) return;
                RebuildVisual();
            }

            // Задача «разрушение плиты»: Tick() берёт deltaSeconds — нужен
            // для анимации обвала и фазы визуальной пульсации (см.
            // TunnelDebugVisual.Tick).
            _visual.Tick(Time.deltaTime);
        }

        private void HandleRunStarted() => RebuildVisual();

        private void RebuildVisual()
        {
            DisposeVisual();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;
            _visual = new TunnelDebugVisual(_input.Grid, _input.Trail, _input.Projection, transform);
        }

        private void DisposeVisual()
        {
            _visual?.Dispose();
            _visual = null;
        }
    }
}
