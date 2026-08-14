using Burmalda.Core;
using UnityEngine;

namespace Burmalda.Movement
{
    /// <summary>
    /// Пересобирает процедурную расстановку статичных препятствий (issue #9)
    /// на каждый забег: <see cref="TunnelGridReveal"/> материализует плиты
    /// заранее, <see cref="TunnelObstacleGenerator"/> (Core) решает тип
    /// каждой новой плиты по мере материализации. Оба используют Grid/Trail
    /// того же забега, что и <see cref="GridTraceInputController"/> — по
    /// паттерну <c>Burmalda.Decay.TrailDecayController</c>. Привязка к тому
    /// же GameObject, что и <see cref="GridTraceInputController"/> — вручную
    /// пользователем, здесь только логика пересборки.
    ///
    /// <para><b>УСТАРЕЛО (PRD v7 §21, issue #78)</b>: заменён
    /// <c>Burmalda.Generation.SegmentGenerationController</c> (авторские
    /// шаблоны-сегменты вместо броска на каждую отдельную плиту, seeded RNG
    /// вместо <c>UnityEngine.Random</c> ниже). Класс НЕ удалён и продолжает
    /// работать — на сцене уже есть GameObject с этим компонентом
    /// (`Assets/Scenes/SampleScene.unity`), а трогать .unity-файлы
    /// автономно запрещено (docs/rules/forbidden-actions.md). Замену
    /// компонента на новый в сцене нужно сделать вручную в Editor; не
    /// использовать оба одновременно на одном забеге — конкурируют за одни и
    /// те же плиты.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TunnelObstacleController : MonoBehaviour
    {
        [SerializeField] private GridTraceInputController _input;

        private TunnelObstacleGenerator _generator;
        private TunnelGridReveal _reveal;

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
            DisposeAll();
        }

        private void Update()
        {
            // Ленивая инициализация вместо OnEnable — по тому же паттерну,
            // что TrailDecayController/TunnelCameraController: порядок
            // Awake/OnEnable между разными компонентами не гарантирован, а
            // Grid/Trail появляются только в Awake() GridTraceInputController.
            if (_generator == null || _reveal == null)
            {
                if (_input == null || _input.Grid == null || _input.Trail == null) return;
                Rebuild();
            }
        }

        private void HandleRunStarted() => Rebuild();

        private void Rebuild()
        {
            DisposeAll();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;

            // Генератор подписывается на TileMaterialized ДО того, как reveal
            // начинает материализовывать плиты в своём конструкторе — иначе
            // самые первые открытые ряды остались бы без типа препятствия.
            _generator = new TunnelObstacleGenerator(_input.Grid, () => UnityEngine.Random.value);
            _reveal = new TunnelGridReveal(_input.Grid, _input.Trail);
        }

        private void DisposeAll()
        {
            _reveal?.Dispose();
            _reveal = null;
            _generator?.Dispose();
            _generator = null;
        }
    }
}
