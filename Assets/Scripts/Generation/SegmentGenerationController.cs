using System;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.Generation
{
    /// <summary>
    /// Пересобирает <see cref="SegmentRowProvider"/> (PRD v7 §21, issue #78)
    /// на каждый забег — по паттерну <c>Movement.TunnelObstacleController</c>,
    /// который эта система заменяет (см. docs/wiki/csharp-glossary.md,
    /// раздел «Ловушки и рычаги»: TunnelObstacleController по-прежнему
    /// существует в проекте и НЕ удалён — привязка компонента на GameObject
    /// в сцене меняется вручную пользователем, чтобы не трогать .unity
    /// (docs/rules/forbidden-actions.md); использовать оба контроллера
    /// одновременно на одном забеге не нужно — они конкурируют за одни и те
    /// же плиты).
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

        private void Update()
        {
            if (_provider == null)
            {
                if (_input == null || _input.Grid == null || _input.Trail == null) return;
                RebuildProvider();
            }
        }

        private void HandleRunStarted() => RebuildProvider();

        private void RebuildProvider()
        {
            DisposeProvider();
            if (_input == null || _input.Grid == null || _input.Trail == null) return;

            var seed = new RunSeed(new Random().Next());
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
