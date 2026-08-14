using System;
using Burmalda.Core;
using Burmalda.Movement;

namespace Burmalda.Currencies
{
    /// <summary>
    /// Начисление Монет за собранные плиты (PRD раздел 5, issue #13):
    /// "источник — обычные плиты". Порт формулы из
    /// legacy/burmolda_demo.html (act()): <c>base=1+applyTalismanBonus();
    /// ...; multi=getM(effLen); runCoins+=base*multi*deeperRiskMult;</c> —
    /// каждая по-настоящему новая плита (<see cref="GridTraceTrail.Advanced"/>,
    /// #61) даёт <see cref="BaseCoinsPerTile"/>, умноженный на ТЕКУЩИЙ (уже
    /// пересчитанный для этого хода) множитель добычи. Бонус Талисмана
    /// (<c>applyTalismanBonus()</c>) и множитель риска Знамения
    /// (<c>deeperRiskMult</c>) не перенесены — соответствующих систем в
    /// проекте ещё нет (Спринт 5, Спринт 9).
    ///
    /// <b>Критично для порядка подписки</b>: <paramref name="multiplier"/>
    /// должен быть сконструирован ДО этого класса (см. вызывающую сторону,
    /// <c>CurrencyController</c>) — оба независимо подписаны на
    /// <see cref="GridTraceTrail.Advanced"/>, и обработчик множителя должен
    /// успеть пересчитать <see cref="TrailMultiplierSystem.CurrentMultiplier"/>
    /// до того, как этот класс его прочитает для того же хода (C#-делегаты
    /// вызывают подписчиков в порядке подписки — тот же принцип, что и в
    /// <c>TunnelObstacleController</c>: "генератор подписывается... ДО того,
    /// как reveal начинает материализовывать плиты").
    /// </summary>
    public sealed class TrailCoinSystem : IDisposable
    {
        /// <summary>
        /// Базовая ценность обычной плиты в Монетах до умножения на
        /// множитель (порт <c>base=1</c> из прототипа). Редкие/эпические/
        /// легендарные плиты с бонусом к базе (PRD v4 §3: "Rare/epic/
        /// legendary тайлы") не перенесены — вне явного текста issue #13,
        /// известный пробел, не молчаливое решение.
        /// </summary>
        public const int BaseCoinsPerTile = 1;

        private readonly GridTraceTrail _trail;
        private readonly TrailMultiplierSystem _multiplier;
        private readonly RunCurrencyAccumulator _runCoins;
        private bool _disposed;

        public TrailCoinSystem(GridTraceTrail trail, TrailMultiplierSystem multiplier, RunCurrencyAccumulator runCoins)
        {
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _multiplier = multiplier ?? throw new ArgumentNullException(nameof(multiplier));
            _runCoins = runCoins ?? throw new ArgumentNullException(nameof(runCoins));

            _trail.Advanced += OnAdvanced;
        }

        /// <summary>Отписывается от трейла. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.Advanced -= OnAdvanced;
            _disposed = true;
        }

        private void OnAdvanced(GridCoordinate coordinate)
        {
            _runCoins.Add(BaseCoinsPerTile * _multiplier.CurrentMultiplier);
        }
    }
}
