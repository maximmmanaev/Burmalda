using System;
using Burmalda.Core;
using Burmalda.Movement;

namespace Burmalda.Currencies
{
    /// <summary>
    /// Начисление Кристаллов Маны за собранные плиты (PRD v9 раздел 5,
    /// issue #13 → переезд задачи по экономике "Мана как доход забега"):
    /// в v8 обычная плита давала базовые Монеты, в v9 — базовую Ману.
    /// Класс — переименованный <c>TrailCoinSystem</c>, формула НЕ менялась:
    /// порт из legacy/burmolda_demo.html (act()): <c>base=1+applyTalismanBonus();
    /// ...; multi=getM(effLen); runCoins+=base*multi*deeperRiskMult;</c> —
    /// каждая по-настоящему новая плита (<see cref="GridTraceTrail.Advanced"/>,
    /// #61) даёт <see cref="BaseManaPerTile"/>, умноженный на ТЕКУЩИЙ (уже
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
    public sealed class TrailManaIncomeSystem : IDisposable
    {
        /// <summary>
        /// Базовая ценность обычной плиты в Кристаллах Маны до умножения на
        /// множитель (порт <c>base=1</c> из прототипа, значение поднято с 1
        /// до 10 задачей по экономике v9 — обычная плита стала рабочей
        /// валютой забега, а не Монетами). Редкие/эпические/легендарные
        /// плиты с бонусом к базе (PRD v4 §3: "Rare/epic/legendary тайлы")
        /// не перенесены — вне явного текста issue #13, известный пробел,
        /// не молчаливое решение.
        ///
        /// Стартовое значение, уточняется на плейтесте — намеренно НЕ
        /// <c>const</c>, а изменяемое статическое поле: живой рычаг для
        /// debug-панели (<c>DebugVisuals.EconomyDebugPanel</c>), тот же
        /// принцип, что <c>Movement.TunnelCameraController.AnchorViewportY</c>.
        /// Отношение к <see cref="CurrencyController.ManaPerSource"/> задано
        /// намеренно: источник должен давать на порядок больше обычной
        /// плиты, иначе крюк к нему не окупается (PRD v9 §4/§5).
        /// </summary>
        public static int BaseManaPerTile = 10;

        private readonly GridTraceTrail _trail;
        private readonly TrailMultiplierSystem _multiplier;
        private readonly RunCurrencyAccumulator _runMana;
        private bool _disposed;

        public TrailManaIncomeSystem(GridTraceTrail trail, TrailMultiplierSystem multiplier, RunCurrencyAccumulator runMana)
        {
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _multiplier = multiplier ?? throw new ArgumentNullException(nameof(multiplier));
            _runMana = runMana ?? throw new ArgumentNullException(nameof(runMana));

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
            _runMana.Add(BaseManaPerTile * _multiplier.CurrentMultiplier);
        }
    }
}
