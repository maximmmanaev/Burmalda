using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.Decay
{
    /// <summary>
    /// Тикает распад трейла каждый кадр (PRD 4.1/16), используя сетку и
    /// трейл того же забега, что и <see cref="GridTraceInputController"/>.
    /// Визуализация распадающихся/разрушенных плит под камерой от третьего
    /// лица — отдельная работа с материалами/префабами, здесь не входит.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrailDecayController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;

        private TrailDecaySystem _decay;

        /// <summary>Система распада текущего забега — для чтения смежными системами (визуал, d20/смерть).</summary>
        public TrailDecaySystem Decay => _decay;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<GridTraceInputController>();
        }

        private void OnDisable()
        {
            _decay?.Dispose();
            _decay = null;
        }

        private void Update()
        {
            // Ленивая инициализация вместо OnEnable: порядок Awake/OnEnable
            // между разными компонентами одного GameObject не гарантирован,
            // а Grid/Trail появляются только в Awake() GridTraceInputController.
            if (_decay == null)
            {
                if (_input == null || _input.Grid == null || _input.Trail == null) return;
                _decay = new TrailDecaySystem(_input.Grid, _input.Trail);
            }

            _decay.Tick(Time.deltaTime);
        }
    }
}
