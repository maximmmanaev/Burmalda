using System;
using System.Collections.Generic;
using Burmalda.Core;
using Burmalda.Movement;

namespace Burmalda.Artifacts
{
    /// <summary>Как Идол выбирает цель (PRD v10 §18.1): Рывок, Пробой, Переход — после двойного тапа; Щит и Время — сразу.</summary>
    public enum IdolTargeting
    {
        Immediate,
        Aim,
    }

    /// <summary>Что видит способность Идола: сетка, след, время и слой эффектов Идола.</summary>
    public sealed class IdolAbilityContext
    {
        public IdolAbilityContext(TunnelGrid grid, GridTraceTrail trail, IdolEffects effects, Func<float> now)
        {
            Grid = grid;
            Trail = trail;
            Effects = effects;
            Now = now;
        }

        public TunnelGrid Grid { get; }

        public GridTraceTrail Trail { get; }

        public IdolEffects Effects { get; }

        public Func<float> Now { get; }
    }

    /// <summary>Способность Идола (PRD v10 §18.2). Никакая не восстанавливает разрушенный маршрут, не даёт иммунитета к Лаве и не заменяет Бросок Судьбы (§32).</summary>
    public interface IIdolAbility
    {
        string IdolId { get; }

        IdolTargeting Targeting { get; }

        /// <summary>Выполняет способность на уровне <paramref name="level"/>. <paramref name="aim"/> — цель для <see cref="IdolTargeting.Aim"/>. False — не получилось (заряд не тратится).</summary>
        bool Execute(IdolAbilityContext context, int level, GridCoordinate? aim);
    }

    /// <summary>
    /// Применение Идола в забеге (PRD v10 §18.1): двойной тап по своей плите; не заряжен — ничего. Рывок/Пробой/Переход входят
    /// в режим выбора цели, следующее прицеливание и отпускание выполняют способность вместо шага; Щит/Время — сразу.
    /// Двойной тап в режиме выбора цели отменяет его без траты заряда.
    /// </summary>
    public sealed class IdolRunSystem
    {
        private readonly CollectionProgress _progress;
        private readonly Func<bool> _isFull;
        private readonly Func<bool> _consume;
        private readonly IdolAbilityContext _context;
        private readonly Dictionary<string, IIdolAbility> _abilities = new Dictionary<string, IIdolAbility>();

        public IdolRunSystem(CollectionProgress progress, Func<bool> isCharged, Func<bool> consumeCharge, IdolAbilityContext context, IEnumerable<IIdolAbility> abilities)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _isFull = isCharged ?? throw new ArgumentNullException(nameof(isCharged));
            _consume = consumeCharge ?? throw new ArgumentNullException(nameof(consumeCharge));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            foreach (var ability in abilities ?? throw new ArgumentNullException(nameof(abilities))) _abilities[ability.IdolId] = ability;
        }

        /// <summary>Идёт выбор цели способности.</summary>
        public bool IsTargeting { get; private set; }

        /// <summary>Идол, чья способность ждёт цель.</summary>
        public string TargetingIdolId { get; private set; }

        public event Action StateChanged;

        /// <summary>Двойной тап по своей плите. True — тап обработан (применили, вошли в выбор цели или отменили его).</summary>
        public bool OnDoubleTap()
        {
            if (IsTargeting)
            {
                SetTargeting(false, null);
                return true;
            }

            var id = _progress.ActiveIdolId;
            if (id == null || !_abilities.TryGetValue(id, out var ability)) return false; // «голый» старт или способности нет
            if (!_isFull()) return false; // не заряжен — ничего не происходит

            if (ability.Targeting == IdolTargeting.Immediate)
            {
                if (!ability.Execute(_context, Level(id), null)) return false;
                _consume();
                return true;
            }

            SetTargeting(true, id);
            return true;
        }

        /// <summary>Игрок отпустил палец на плите в режиме выбора цели. True — способность выполнена (заряд потрачен, режим закрыт).</summary>
        public bool OnTargetReleased(GridCoordinate aim)
        {
            if (!IsTargeting || !_abilities.TryGetValue(TargetingIdolId, out var ability)) return false;
            if (!ability.Execute(_context, Level(TargetingIdolId), aim)) return false; // неверная цель: режим остаётся, заряд цел
            _consume();
            SetTargeting(false, null);
            return true;
        }

        /// <summary>Сброс при новом забеге.</summary>
        public void Reset() => SetTargeting(false, null);

        private int Level(string idolId) => Math.Max(1, _progress.LevelOf(idolId));

        private void SetTargeting(bool on, string id)
        {
            IsTargeting = on;
            TargetingIdolId = id;
            StateChanged?.Invoke();
        }
    }
}
