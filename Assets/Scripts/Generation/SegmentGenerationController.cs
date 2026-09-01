using System;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.Generation
{
    /// <summary>
    /// Пересобирает <see cref="SegmentRowProvider"/> (PRD v7 §21, issue #78)
    /// на каждый забег — целевая замена <c>Core.TunnelObstacleGenerator</c>/
    /// <c>Movement.TunnelObstacleController</c> (см. docs/wiki/csharp-glossary.md,
    /// раздел «Ловушки и рычаги»).
    ///
    /// <b>Сосуществование с TunnelObstacleController (переходное состояние,
    /// docs/wiki/roadmap.md)</b>: раньше оба контроллера считались взаимно
    /// исключающими ("конкурируют за одни и те же плиты") и подключение
    /// этого класса через <c>Bootstrap.RunBootstrap</c> сознательно
    /// откладывалось. Разграничение по рядам —
    /// <see cref="SegmentRowProvider"/> заявляет (<c>TunnelGrid.ClaimRow</c>)
    /// все ряды применяемого шаблона ДО их материализации, а
    /// <c>Core.TunnelObstacleGenerator</c> пропускает уже заявленные ряды
    /// (см. doc-комментарии обоих классов). Этого достаточно, ТОЛЬКО если
    /// <see cref="SegmentRowProvider"/> реально успевает заявить свои ряды
    /// раньше, чем <c>Movement.TunnelGridReveal</c> материализует их
    /// напрямую — раньше это зависело от порядка `RunStarted`/`Update()`
    /// между независимо самоуправляемыми контроллерами (не гарантирован) и
    /// на практике ломалось (плейтест владельца, задача «двойные флаги на
    /// плитах» — плиты с ролью от обоих генераторов одновременно). Порядок
    /// теперь задаёт явно <c>Bootstrap.RunBootstrap</c> — см.
    /// <see cref="EnsureBuilt"/> и doc-комментарий <c>RunBootstrap</c>.
    /// <c>TunnelObstacleController</c> удаляется со сцены владельцем продукта
    /// только когда каталог шаблонов (<see cref="SegmentTemplateCatalog"/>)
    /// покроет тоннель целиком без содержательных пробелов — см.
    /// docs/wiki/roadmap.md, до этого момента он продолжает засеивать ряды,
    /// до которых сегменты ещё не дошли.
    ///
    /// Обычный забег — случайный seed (не завязан на <c>UnityEngine.Random</c>,
    /// см. <see cref="RunSeed"/>); seed Испытания Шахты — отдельная забота
    /// вызывающей стороны (<c>RunSeed.ComputeTrialSeed</c>), сюда пока не
    /// подключена (нет источника "текущее Испытание/дата", Спринт 9/13).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SegmentGenerationController : MonoBehaviour
    {
        // Черновая кривая сложности по рядам — заглушка "Яруса Глубины"
        // (Спринт 8), которого в проекте ещё нет: сложность растёт на 1
        // каждые RowsPerDifficultyStep рядов, до MaxDifficultyTier. Предмет
        // плейтеста баланса (Спринт 10, docs/rules/forbidden-actions.md).
        public const int RowsPerDifficultyStep = 40;

        [SerializeField] private GridTraceInputController _input;

        private SegmentRowProvider _provider;

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
            DisposeProvider();
        }

        private void Update() => EnsureBuilt();

        /// <summary>
        /// Строит <see cref="SegmentRowProvider"/>, если он ещё не собран —
        /// не-op, если уже собран или Grid/Trail ещё не готовы. Публичный,
        /// а не только внутренний ленивый вызов из <see cref="Update"/> —
        /// задача «двойные флаги на плитах»: <c>Bootstrap.RunBootstrap</c>
        /// вызывает его СИНХРОННО в своём <c>Awake()</c> (гарантированно до
        /// первого <c>Update()</c> любого компонента в этом кадре — самобутстрап
        /// через <c>RuntimeInitializeLoadType.AfterSceneLoad</c> происходит
        /// уже после Awake/OnEnable всех объектов сцены), чтобы этот
        /// провайдер ГАРАНТИРОВАННО заявил свои первые ряды раньше, чем
        /// уже размещённый на сцене <c>Movement.TunnelObstacleController</c>
        /// успеет материализовать их напрямую в обход <c>TunnelGrid.ClaimRow</c>
        /// — раньше порядок зависел от того, чей <c>Update()</c>/<c>RunStarted</c>
        /// сработает первым (не гарантировано), теперь порядок задан явно.
        /// </summary>
        public void EnsureBuilt()
        {
            if (_provider != null) return;
            if (_input == null || _input.Grid == null || _input.Trail == null) return;
            RebuildProvider();
        }

        private void HandleRunStarted() => RebuildProvider();

        private void RebuildProvider()
        {
            DisposeProvider();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;

            var seed = new RunSeed(new System.Random().Next()); // явно System.Random — не UnityEngine.Random, см. doc-комментарий класса
            var selector = new SegmentSelector(SegmentTemplateCatalog.All, seed);
            _provider = new SegmentRowProvider(_input.Grid, _input.Trail, selector, MaxDifficultyForRow);
        }

        private static int MaxDifficultyForRow(int row)
        {
            var tier = 1 + row / RowsPerDifficultyStep;
            return tier > SegmentTemplate.MaxDifficultyTier ? SegmentTemplate.MaxDifficultyTier : tier;
        }

        private void DisposeProvider()
        {
            _provider?.Dispose();
            _provider = null;
        }
    }
}
