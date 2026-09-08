using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Пересобирает <see cref="TrailMultiplierSystem"/> (PRD 4.3, issue #11)
    /// на каждый забег (реагирует на события трейла, не нужен per-frame
    /// Tick). Привязка к тому же GameObject,
    /// что и <see cref="GridTraceInputController"/> — вручную пользователем.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrailMultiplierController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;

        private TrailMultiplierSystem _multiplier;

        /// <summary>Текущий множитель добычи текущего забега — для чтения смежными системами (HUD, будущие Currencies). 1, пока забег не начат.</summary>
        public int CurrentMultiplier => _multiplier?.CurrentMultiplier ?? 1;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
        }

        private void OnEnable()
        {
            if (_input != null) _input.RunStarted += HandleRunStarted;
        }

        private void OnDisable()
        {
            if (_input != null) _input.RunStarted -= HandleRunStarted;
            DisposeMultiplier();
        }

        private void Update()
        {
            // Ленивая инициализация — тот же паттерн, что и в соседних контроллерах.
            if (_multiplier == null)
            {
                if (_input == null || _input.Trail == null) return;
                RebuildMultiplier();
            }
        }

        private void HandleRunStarted() => RebuildMultiplier();

        private void RebuildMultiplier()
        {
            DisposeMultiplier();
            if (_input == null || _input.Trail == null) return;
            _multiplier = new TrailMultiplierSystem(_input.Trail);
        }

        private void DisposeMultiplier()
        {
            _multiplier?.Dispose();
            _multiplier = null;
        }
    }
}
