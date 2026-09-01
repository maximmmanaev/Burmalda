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
    ///
    /// <para><b>Задача «двойные флаги на плитах»:</b> раньше этот класс сам
    /// подписывался на <c>GridTraceInputController.RunStarted</c> — из-за
    /// того, что он уже размещён на сцене (его <c>OnEnable</c> срабатывает
    /// при загрузке сцены), а <c>Generation.SegmentGenerationController</c>
    /// добавляется динамически позже <c>Bootstrap.RunBootstrap</c>, подписка
    /// ЭТОГО класса всегда оказывалась РАНЬШЕ в списке подписчиков —
    /// <see cref="TunnelGridReveal"/> успевал материализовать ряды раньше,
    /// чем <c>Generation.SegmentRowProvider</c> успевал их заявить
    /// (<c>TunnelGrid.ClaimRow</c>), давая плитам одновременно роль от обоих
    /// генераторов. Теперь этот класс НЕ подписывается на RunStarted сам —
    /// <c>RunBootstrap</c> единственный, кто вызывает <see cref="EnsureBuilt"/>/
    /// <see cref="EnsureRebuilt"/>, и делает это СТРОГО после того, как
    /// сегментный провайдер уже заявил свои ряды на этот момент.</para>
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

        private void OnDisable()
        {
            DisposeAll();
        }

        private void Update() => EnsureBuilt();

        /// <summary>
        /// Строит генератор/reveal, если ещё не собраны — не-op, если уже
        /// собраны или Grid/Trail ещё не готовы. Идемпотентный аналог
        /// <c>Generation.SegmentGenerationController.EnsureBuilt</c> — тот же
        /// приём: <c>Bootstrap.RunBootstrap</c> вызывает его синхронно, СРАЗУ
        /// ПОСЛЕ того, как вызвал аналогичный метод у сегментного провайдера
        /// (см. класс-докстрингу), поэтому к моменту, когда <see cref="TunnelGridReveal"/>
        /// реально начинает материализовывать ряды, они уже заявлены.
        /// </summary>
        public void EnsureBuilt()
        {
            if (_generator != null && _reveal != null) return;
            if (_input == null || _input.Grid == null || _input.Trail == null) return;
            Rebuild();
        }

        /// <summary>
        /// Безусловная пересборка (Dispose старого + новый Grid/Trail) — для
        /// повторных забегов (<c>RunStarted</c> на рестарте). Вызывается
        /// ТОЛЬКО извне (<c>Bootstrap.RunBootstrap.HandleRunStarted</c>),
        /// строго после того, как сегментный провайдер уже пересобрался сам
        /// (его собственная подписка на RunStarted идёт раньше в списке —
        /// см. <c>RunBootstrap.EnsureControllersWired</c>).
        /// </summary>
        public void EnsureRebuilt() => Rebuild();

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
