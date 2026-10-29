using System;
using Burmalda.Core;
using Burmalda.Decay;
using Burmalda.FateRoll;
using Burmalda.Movement;

namespace Burmalda.RunLifecycle
{
    /// <summary>
    /// Жизнь/смерть текущего забега (PRD 9, issue #24). Смерть от ловушки/
    /// обвала плиты запускает d20-испытание вместо мгновенного проигрыша —
    /// см. <see cref="ResolveHazard"/>. Слушает источники опасности —
    /// попытку шагнуть на смертельную ловушку
    /// (<see cref="GridTraceTrail.LethalTrapTriggered"/>, PRD 4.2) и
    /// обрушение плиты под ногами игрока
    /// (<see cref="TrailDecaySystem.TileDestroyed"/> для текущей позиции) —
    /// по аналогии с legacy/burmolda_demo.html, attemptDeath().
    /// Поражение от Босса — отдельный путь, ReportBossDefeat (удалён):
    /// d20 к нему НЕ применяется (PRD v7 §8.3, issue #82) — детерминировано.
    /// Ничего не знает про рестарт — при новом забеге создаётся новый
    /// экземпляр (мир перегенерируется заново, см. GridTraceInputController.Restart).
    /// </summary>
    public sealed class RunState : IDisposable
    {
        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly TrailDecaySystem _decay;
        private readonly Func<int> _rollDie;
        private string _pendingReason;
        private GridCoordinate _lastAltarCoordinate;
        private bool _disposed;

        public RunState(TunnelGrid grid, GridTraceTrail trail, TrailDecaySystem decay, Func<int> rollDie, int fateRollsUsed = 0)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _decay = decay ?? throw new ArgumentNullException(nameof(decay));
            _rollDie = rollDie ?? throw new ArgumentNullException(nameof(rollDie));
            FateRollsUsed = fateRollsUsed;
            IsAlive = true;
            // Запасной "чекпоинт" для Knockback (#24), пока игрок ещё не
            // прошёл ни одного Алтаря в этом забеге — откат к старту.
            _lastAltarCoordinate = trail.CurrentPosition;

            _trail.LethalTrapTriggered += OnLethalTrapTriggered;
            _decay.TileDestroyed += OnTileDestroyed;
            _trail.PositionChanged += OnPositionChanged;
        }

        /// <summary>Жив ли игрок в этом забеге. Становится false ровно один раз — далее не меняется.</summary>
        public bool IsAlive { get; private set; }

        /// <summary>Срабатывает один раз, когда игрок умирает — с человекочитаемой причиной для будущего UI.</summary>
        public event Action<string> Died;

        /// <summary>Срабатывает при каждом броске d20 (issue #24) — для будущей визуальной обратной связи (PRD §19).</summary>
        /// <summary>
        /// Опасность настигла игрока, а Броски Судьбы ещё есть (PRD v10 §16): ждём
        /// решения «Бросить» (<see cref="RollFate"/>) или «Отказаться»
        /// (<see cref="DeclineFate"/>). Аргумент — номер предлагаемого броска (1..3).
        /// </summary>
        public event Action<int> FateRollOffered;

        /// <summary>Бросок брошен: кубики, сумма, исход.</summary>
        public event Action<FateRollResult> FateRolled;

        /// <summary>Сколько Бросков Судьбы потрачено в этой экспедиции (максимум <see cref="FateRollRules.MaxRolls"/>).</summary>
        public int FateRollsUsed { get; private set; }

        /// <summary>Ждёт решения игрока по Броску Судьбы.</summary>
        public bool IsAwaitingFate { get; private set; }

        /// <summary>Модификатор суммы 2D6 — единственная точка расширения под Тотем Мигизи (§16.1); пока 0.</summary>
        public int FateModifier { get; set; }

        /// <summary>Отписывается от трейла и распада. Вызывать при завершении забега/пересборке на рестарте.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.LethalTrapTriggered -= OnLethalTrapTriggered;
            _decay.TileDestroyed -= OnTileDestroyed;
            _trail.PositionChanged -= OnPositionChanged;
            _disposed = true;
        }

        /// <summary>
        /// Пол Святилища обрушился под игроком (PRD v10 §14.2): обычная опасность —
        /// Бросок Судьбы, если он ещё доступен, иначе смерть.
        /// </summary>
        public void ReportSanctuaryCollapse() => ResolveHazard("Пол Святилища обрушился");

        /// <summary>
        /// Падающий камень (issue #217, задача «падающий камень: новая
        /// спецификация» — Спринт «Стены вместо ловушек»): игрок стоял на
        /// целевой плите в момент падения камня
        /// (<c>Movement.FallingRockTrapSystem.PlayerCrushed</c>). Как и
        /// прочие источники опасности (<see cref="OnLethalTrapTriggered"/>,
        /// <see cref="OnTileDestroyed"/>) — идёт через
        /// <see cref="ResolveHazard"/> (d20, PRD раздел 9), не мгновенная
        /// безусловная смерть: владелец не просил для этой ловушки
        /// исключения из стандартного разрешения опасности, как это явно
        /// сделано для ReportBossDefeat (удалён).
        ///
        /// Подключается СНАРУЖИ (<c>Bootstrap.RunBootstrap</c>), не через
        /// конструктор — <c>Movement</c> не ссылается на <c>RunLifecycle</c>
        /// (см. asmdef), а <c>Movement.FallingRockTrapSystem</c>
        /// пересобирается на каждый забег отдельно от <see cref="RunState"/>;
        /// прямая подписка в конструкторе создала бы зависимость в обратную
        /// сторону. См. <c>Movement.TrapSystemsController.FallingRockPlayerCrushed</c>.
        /// </summary>
        public void ReportFallingRockCrush()
        {
            if (TryAbsorb(ThreatType.Blunt)) return;
            ResolveHazard("Раздавлен камнем");
        }

        private void OnPositionChanged(GridCoordinate coordinate)
        {
            if (_grid.TryGetTile(coordinate, out var tile) && tile.IsAltar)
                _lastAltarCoordinate = coordinate;
        }

        private void OnLethalTrapTriggered(GridCoordinate coordinate, LethalTrapType trapType)
        {
            // Лава — не ловушка: защиты нет (§32, инвариант 5). Остальное — попадание угрозы, его может поглотить Амулет.
            if (trapType != LethalTrapType.Lava && TryAbsorb(ThreatOf(trapType))) return;
            ResolveHazard(DescribeLethalTrap(trapType));
        }

        private static ThreatType ThreatOf(LethalTrapType trapType) => trapType switch
        {
            LethalTrapType.BombBlast => ThreatType.Explosion,
            LethalTrapType.ArrowWave => ThreatType.Slashing,
            LethalTrapType.BladeTact => ThreatType.Slashing,
            _ => ThreatType.Blunt,
        };

        // Амулеты Реликвий (Плат Мокоши, Перунов знак): раз за Ярус переживают попадание. Лаву сюда не передаём.
        private static bool TryAbsorb(ThreatType threat) => RunEffects.Current.TryAbsorbHit(threat);

        /// <summary>
        /// Попадание угрозы типа <paramref name="threat"/> — единая точка для ловушек, которых в игре ещё нет (Огненная струя,
        /// Капкан, Валун): Амулет может поглотить попадание раз за Ярус, иначе это обычная опасность (Бросок Судьбы). Лава сюда
        /// не передаётся — от неё защиты нет.
        /// </summary>
        public void ReportThreatHit(ThreatType threat)
        {
            if (TryAbsorb(threat)) return;
            ResolveHazard($"Попадание: {threat}");
        }

        private static string DescribeLethalTrap(LethalTrapType trapType) => trapType switch
        {
            // issue #262: LethalTrapType.LavaWave (отдельный идентификатор
            // волны Лавы) слит с этим значением — Movement.LavaWaveTrapSystem
            // теперь тоже ставит Lava, отдельная ветка здесь больше не нужна.
            LethalTrapType.Lava => "Сгорел в лаве",
            LethalTrapType.ArrowWave => "Пронзён стрелой",
            LethalTrapType.BombBlast => "Подорвался на бомбе",
            LethalTrapType.BladeTact => "Разрублен лезвием",
            _ => "Сработала ловушка"
        };

        private void OnTileDestroyed(GridCoordinate coordinate)
        {
            // legacy/burmolda_demo.html, updateDecay(): проверяется только ПОСЛЕДНЯЯ (текущая) плита трейла.
            if (coordinate == _trail.CurrentPosition)
                ResolveHazard("Плита под ногами обрушилась");
        }

        /// <summary>
        /// Опасность (ловушка, Лава, обрушение): если Броски Судьбы ещё есть —
        /// предлагает бросок и ждёт решения, иначе смерть окончательна (PRD v10 §16, §15.3).
        /// </summary>
        private void ResolveHazard(string reason)
        {
            if (!IsAlive || IsAwaitingFate) return;

            if (FateRollsUsed >= FateRollRules.MaxRolls)
            {
                Die(reason);
                return;
            }

            IsAwaitingFate = true;
            _pendingReason = reason;
            FateRollOffered?.Invoke(FateRollsUsed + 1);
        }

        /// <summary>
        /// «Бросить»: платит (заглушка — бесплатно), бросает 2D6 и применяет исход:
        /// на месте — продолжает; к Алтарю — телепорт на последний пройденный Алтарь
        /// (разрушенный маршрут не восстанавливается); смерть — окончательная.
        /// False, если бросать нечего или платёж не прошёл.
        /// </summary>
        public bool RollFate()
        {
            if (!IsAlive || !IsAwaitingFate) return false;

            var rollIndex = FateRollsUsed + 1;
            if (!FateRollRules.TryPay(rollIndex)) return false;

            FateRollsUsed = rollIndex;
            var die1 = _rollDie();
            var die2 = _rollDie();
            var outcome = FateRollRules.Evaluate(rollIndex, die1, die2, FateModifier);
            var reason = _pendingReason;
            IsAwaitingFate = false;
            _pendingReason = null;
            FateRolled?.Invoke(new FateRollResult(rollIndex, die1, die2, FateModifier, outcome));

            switch (outcome)
            {
                case FateOutcome.ToAltar:
                    _trail.TeleportTo(_lastAltarCoordinate);
                    break;
                case FateOutcome.Death:
                    Die(reason);
                    break;
            }

            return true;
        }

        /// <summary>«Отказаться»: окончательная смерть без броска.</summary>
        public void DeclineFate()
        {
            if (!IsAlive || !IsAwaitingFate) return;
            var reason = _pendingReason;
            IsAwaitingFate = false;
            _pendingReason = null;
            Die(reason);
        }

        private void Die(string reason)
        {
            if (!IsAlive) return;
            IsAlive = false;
            IsAwaitingFate = false;
            Died?.Invoke(reason);
        }
    }
}
