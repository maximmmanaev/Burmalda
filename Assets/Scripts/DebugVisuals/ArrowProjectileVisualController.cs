using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Тонкий driver <see cref="ArrowProjectileVisual"/> (задача «симуляция
    /// стрелы») — тот же самобутстрап-паттерн, что
    /// <see cref="TunnelDebugVisualController"/>: создаёт себя на своём
    /// отдельном GameObject через <see cref="RuntimeInitializeOnLoadMethodAttribute"/>,
    /// не живёт компонентом рядом с <see cref="GridTraceInputController"/> —
    /// см. её doc-комментарий про задачу «на сценах не остаётся ничего,
    /// кроме камеры и света».
    ///
    /// В отличие от <see cref="TunnelDebugVisualController"/>, нужен ещё и
    /// <see cref="TrapSystemsController"/> (источник
    /// <see cref="TrapSystemsController.ArrowWaveStarted"/>) — он живёт на
    /// ТОМ ЖЕ GameObject, что <see cref="GridTraceInputController"/>
    /// (<c>Bootstrap.RunBootstrap.EnsureControllersWired</c>: <c>host =
    /// _input.gameObject</c>), поэтому достаточно одного
    /// <c>GetComponent</c> после того, как сам <c>_input</c> найден.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArrowProjectileVisualController : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Тот же приём защиты от дубликата, что TunnelDebugVisualController.Bootstrap —
            // второй экземпляр создал бы вторую параллельную подписку на
            // ArrowWaveStarted, две летящие стрелы на одну волну.
            if (FindFirstObjectByType<ArrowProjectileVisualController>() != null) return;

            var host = new GameObject(nameof(ArrowProjectileVisualController));
            host.AddComponent<ArrowProjectileVisualController>();
            DontDestroyOnLoad(host);
        }

        private GridTraceInputController _input;
        private TrapSystemsController _traps;
        private ArrowProjectileVisual _visual;

        private void OnDisable()
        {
            if (_input != null) _input.RunStarted -= HandleRunStarted;
            DisposeVisual();
        }

        private void Update()
        {
            // Ленивый поиск — тот же паттерн, что TunnelDebugVisualController.Update
            // (порядок AfterSceneLoad между самобутстрапящимися компонентами
            // не гарантирован).
            if (_input == null)
            {
                _input = FindFirstObjectByType<GridTraceInputController>();
                if (_input != null) _input.RunStarted += HandleRunStarted;
            }
            if (_input == null) return;

            // TrapSystemsController появляется на GameObject _input не
            // синхронно с ним самим (RunBootstrap.EnsureControllersWired
            // вызывается из другого компонента) — отдельный ленивый поиск,
            // не предполагаем, что готово сразу же, как нашёлся _input.
            if (_traps == null) _traps = _input.GetComponent<TrapSystemsController>();

            if (_visual == null)
            {
                if (_traps == null || _input.Grid == null) return;
                RebuildVisual();
            }

            _visual.Tick(Time.deltaTime);
        }

        private void HandleRunStarted() => RebuildVisual();

        private void RebuildVisual()
        {
            DisposeVisual();
            if (_traps == null || _input.Grid == null) return;
            _visual = new ArrowProjectileVisual(_traps, _input.Projection, _input.Grid.Width, transform);
        }

        private void DisposeVisual()
        {
            _visual?.Dispose();
            _visual = null;
        }
    }
}
