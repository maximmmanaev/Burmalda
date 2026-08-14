using System;
using Burmalda.Core;
using Burmalda.Decay;
using Burmalda.Movement;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Применение активной способности Тотема при полном заряде (PRD раздел
    /// 12, issue #28) — единая точка входа для Рывка/Пробоя/Неуязвимости:
    /// читает <see cref="Totem.ActiveAbility"/> и вызывает уже существующий,
    /// отдельно протестированный эффект (<see cref="GridTraceTrail.PrimeBreach"/>,
    /// <see cref="TrailDecaySystem.Suspend"/>, продвижение трейла для Рывка)
    /// вместо дублирования логики движения/распада здесь.
    ///
    /// "Второе дыхание" (4-е значение <see cref="TotemAbilityType"/>) сюда
    /// не входит — активируется не готовностью этого заряда, а отдельным
    /// триггером на провале возврата (issue #23, Спринт 7, см.
    /// Burmalda.RunLifecycle.SecondWindCharge); здесь для него — не-op.
    ///
    /// Вызов самой активации (жест "двойной тап", PRD раздел 12) — вне
    /// скоупа этого класса: в проекте нет UI-кнопки Тотема и нет детектора
    /// двойного тапа поверх экрана ввода трейса (GridTraceInputController
    /// сейчас принимает только непрерывный drag, весь экран занят под него —
    /// добавление жеста поверх него требует отдельного UI/UX-решения, не
    /// чисто логической задачи). <see cref="TryActivate"/> — публичная точка
    /// входа, которую такой ввод сможет вызвать, когда будет реализован.
    ///
    /// По той же причине здесь нет и MonoBehaviour-контроллера, собирающего
    /// эту систему на <c>GridTraceInputController.RunStarted</c>, как для
    /// прочих систем забега: в проекте нет понятия "экипированный на этот
    /// забег Тотем" — в отличие от Идолов (<see cref="IdolLoadout"/>, 2
    /// постоянных слота), у Тотема нет своего слота/лоадаута нигде в коде
    /// или PRD; экземпляр Тотема сейчас передаётся напрямую вызывающей
    /// стороной (см. <c>Camp.TryUpgradeTotem</c>). Изобретать такой слот
    /// самостоятельно значило бы придумывать механику, не описанную в PRD —
    /// оставлено как отдельный, честно задокументированный пробел.
    /// </summary>
    public sealed class TotemAbilityActivationSystem
    {
        // legacy/burmolda_demo.html: invulnUntil=performance.now()+3000
        private const float InvulnerabilityDurationSeconds = 3f;

        // Рывок в прототипе не реализован (кнопка Тотема там всегда делает
        // Неуязвимость независимо от выбранной способности) — числа взять
        // неоткуда. Черновое значение "на глазок" по аналогии с длительностью
        // Неуязвимости; финальное значение задаст плейтест баланса (Спринт 10).
        private const int DashDistanceTiles = 3;

        private readonly GridTraceTrail _trail;
        private readonly TrailDecaySystem _decay;
        private readonly Totem _totem;
        private readonly TotemChargeSystem _charge;

        public TotemAbilityActivationSystem(GridTraceTrail trail, TrailDecaySystem decay, Totem totem, TotemChargeSystem charge)
        {
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _decay = decay ?? throw new ArgumentNullException(nameof(decay));
            _totem = totem ?? throw new ArgumentNullException(nameof(totem));
            _charge = charge ?? throw new ArgumentNullException(nameof(charge));
        }

        /// <summary>
        /// Пытается активировать способность Тотема. Ложь, если заряд ещё не
        /// полон (<see cref="TotemChargeSystem.IsReady"/>) — в этом случае
        /// заряд не тратится и эффект не применяется. При успехе заряд
        /// обнуляется независимо от того, состоялся ли сам эффект (например,
        /// Рывку в начале забега некуда шагнуть) — оплата за попытку, не за
        /// гарантированный результат, как и в прототипе (кнопка Тотема жмётся
        /// один раз и сразу гасит заряд).
        /// </summary>
        public bool TryActivate()
        {
            if (!_charge.TryActivate()) return false;

            switch (_totem.ActiveAbility)
            {
                case TotemAbilityType.Dash:
                    ApplyDash();
                    break;
                case TotemAbilityType.Breach:
                    _trail.PrimeBreach();
                    break;
                case TotemAbilityType.Invulnerability:
                    _decay.Suspend(InvulnerabilityDurationSeconds);
                    break;
                case TotemAbilityType.SecondWind:
                    break; // вне скоупа — см. doc-комментарий класса
            }

            return true;
        }

        private void ApplyDash()
        {
            var path = _trail.Path;
            if (path.Count < 2) return; // направление ещё не определено — Рывку некуда идти

            var from = path[path.Count - 2];
            var to = path[path.Count - 1];
            var rowStep = Math.Sign(to.Row - from.Row);
            var columnStep = Math.Sign(to.Column - from.Column);

            var current = _trail.CurrentPosition;
            for (var i = 0; i < DashDistanceTiles; i++)
            {
                var next = new GridCoordinate(current.Row + rowStep, current.Column + columnStep);
                if (!_trail.TryAdvanceTo(next)) break; // упёрлись в преграду/край/ловушку — останавливаемся
                current = next;
            }
        }
    }
}
