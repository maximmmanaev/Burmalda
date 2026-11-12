using System;
using System.Collections.Generic;
using System.Linq;
using Burmalda.Core;
using Burmalda.Movement;

namespace Burmalda.Artifacts
{
    /// <summary>Вид ловушки для групп Тотема «Маанг» (PRD v10 §8.2, §19). Огненная струя, Капкан и Валун в игре ещё нет — только классификация.</summary>
    public enum TrapKind
    {
        Rock, Crusher, Walls, Arrows,
        Blades, Bomb, FireJet,
        BearTrap, Boulder,
    }

    public static class TrapGroups
    {
        /// <summary>Группа ловушки: 1 — Камень, Давилка, Стены, Стрелы; 2 — Лезвия, Бомба, Огненная струя; 3 — Капкан, Валун.</summary>
        public static int GroupOf(TrapKind kind) => kind switch
        {
            TrapKind.Rock or TrapKind.Crusher or TrapKind.Walls or TrapKind.Arrows => 1,
            TrapKind.Blades or TrapKind.Bomb or TrapKind.FireJet => 2,
            _ => 3,
        };

        /// <summary>Вид ловушки, чей триггер стоит на плите; null — триггера нет.</summary>
        public static TrapKind? KindOf(Tile tile)
        {
            if (tile.IsFallingRockTrigger) return TrapKind.Rock;
            if (tile.MovingWallTargetRow.HasValue) return tile.MovingWallTriggerMode == MovingWallMode.Both ? TrapKind.Crusher : TrapKind.Walls;
            if (tile.ArrowWaveTargetRow.HasValue) return TrapKind.Arrows;
            if (tile.BladeTactTargetRow.HasValue) return TrapKind.Blades;
            if (tile.IsBombTrigger) return TrapKind.Bomb;
            return null;
        }
    }

    /// <summary>
    /// Эффекты Тотема в слоте (PRD v10 §19), 3 уровня; числа — ЧЕРНОВЫЕ [CONFIG]. Слой «totem» в <see cref="RunEffects"/>.
    /// Маанг — раскрытие триггеров и типов группами; Амик — распад, стены, обрушение; Ваагош — Мана источников и Жил, тайник;
    /// Ваабижеши — ход конём; Мигизи — бонус к сумме Броска Судьбы. Ни один Тотем не меняет шансы Саркофагов, частей и Реликвий.
    /// </summary>
    public sealed class TotemEffects : IRunEffects
    {
        public const string Maang = "totem-maang";
        public const string Amik = "totem-amik";
        public const string Waagosh = "totem-waagosh";
        public const string Waabizheshi = "totem-waabizheshi";
        public const string Migizi = "totem-migizi";

        // ---- Числа [CONFIG, черновик] ----
        public static float AmikDecayRate = 0.8f;
        public static float AmikWallSlowdown = 1.5f;
        public static float AmikCollapseSpeed = 0.7f;
        public static float WaagoshSourceMana = 1.5f;
        public static float WaagoshVeinMana = 1.5f;
        public static int KnightMoveEveryStreakTiles = 10;
        public static int MaangLookaheadRows = 8;

        private readonly CollectionProgress _progress;
        private readonly Func<int> _yarus;
        private int _knightUsedAtYarus = -1;
        private int _streakAtLastKnight;
        private int _suppressedSteps;

        public TotemEffects(CollectionProgress progress, Func<int> yarus)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _yarus = yarus ?? throw new ArgumentNullException(nameof(yarus));
        }

        public string ActiveId => _progress.ActiveTotemId;

        public int Level => ActiveId == null ? 0 : Math.Max(1, _progress.LevelOf(ActiveId));

        private bool Is(string id) => ActiveId == id;

        // ---- Амик ----

        public float DecayRateMultiplier => Is(Amik) ? AmikDecayRate : 1f;

        public float WallCloseSlowdown => Is(Amik) && Level >= 2 ? AmikWallSlowdown : 1f;

        public float CollapseSpeedMultiplier => Is(Amik) && Level >= 3 ? AmikCollapseSpeed : 1f;

        // ---- Ваагош ----

        public float SourceManaMultiplier => Is(Waagosh) ? WaagoshSourceMana : 1f;

        public float VeinManaMultiplier => Is(Waagosh) && Level >= 2 ? WaagoshVeinMana : 1f;

        /// <summary>Ваагош, ур. 3: показывает, где за Воротами тайник. Тайник теперь не несёт суммы (v10-3), поэтому это только указатель места.</summary>
        public bool RevealsGateVault => Is(Waagosh) && Level >= 3;

        // ---- Маанг ----

        /// <summary>Какие группы ловушек раскрывает Маанг: ур. 1 — первая, ур. 2 — +вторая, ур. 3 — +третья; 0 — не раскрывает.</summary>
        public int MaangMaxGroup => Is(Maang) ? Level : 0;

        // ---- Мигизи ----

        /// <summary>Бонус к сумме Броска Судьбы: +1 / +2 / +3 (§16, §19).</summary>
        public int FateBonus => Is(Migizi) ? Level : 0;

        // ---- Ваабижеши: ход конём ----

        /// <summary>
        /// Доступен ли ход конём. Ур. 1–2 — раз за Ярус; ур. 3 — каждые <see cref="KnightMoveEveryStreakTiles"/> плит серии
        /// чистого прохода (серия <paramref name="streak"/> после прошлого хода).
        /// </summary>
        public bool KnightMoveAvailable(int streak)
        {
            if (!Is(Waabizheshi)) return false;
            if (Level >= 3) return _knightUsedAtYarus < 0 || streak - _streakAtLastKnight >= KnightMoveEveryStreakTiles;
            return _knightUsedAtYarus != _yarus();
        }

        /// <summary>Ход конём на <paramref name="target"/>: смещение (±1, ±2) или (±2, ±1), перепрыгивая клетки. False — нельзя (нет заряда, не конь, плита непроходима).</summary>
        public bool TryKnightMove(TunnelGrid grid, GridTraceTrail trail, GridCoordinate target, int streak)
        {
            if (!KnightMoveAvailable(streak)) return false;
            var from = trail.CurrentPosition;
            var dr = Math.Abs(target.Row - from.Row);
            var dc = Math.Abs(target.Column - from.Column);
            if (!((dr == 1 && dc == 2) || (dr == 2 && dc == 1))) return false;
            if (!trail.JumpTo(target)) return false;

            _knightUsedAtYarus = _yarus();
            _streakAtLastKnight = streak;
            if (Level >= 2) _suppressedSteps = 1; // ур. 2: приземление не активирует ловушку
            return true;
        }

        private bool _stepNoted;

        /// <summary>Перемещение произошло; подавление расходуется в <see cref="CommitStep"/> (после реакции систем ловушек).</summary>
        public void NoteStep() => _stepNoted = true;

        public void CommitStep()
        {
            if (!_stepNoted) return;
            _stepNoted = false;
            if (_suppressedSteps > 0) _suppressedSteps--;
        }

        /// <summary>Шаг сделан и сразу учтён (для тестов).</summary>
        public void OnStepTaken()
        {
            NoteStep();
            CommitStep();
        }

        public bool StepsActivateTraps => _suppressedSteps <= 0;

        // ---- Остальное нейтрально ----

        public float ThreatTimeScale => 1f;
        public float ManaMultiplier => 1f;
        public float StreakBonusMultiplier => 1f;
        public float ResonanceBonus => NeutralRunEffects.BaseResonanceBonus;
        public float BearTrapEscapeMultiplier => 1f;
        public bool CollapseHeld => false;
        public int BonusManaForStep(int stepNumber) => 0;
        public bool TryAbsorbHit(ThreatType threat) => false;
    }

    /// <summary>Маанг: раскрывает триггеры и типы ловушек доступных групп в ближайших рядах впереди.</summary>
    public sealed class TotemRevealSystem : IDisposable
    {
        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly TotemEffects _totem;
        private readonly Action<int> _ensureCovered;

        public TotemRevealSystem(TunnelGrid grid, GridTraceTrail trail, TotemEffects totem, Action<int> ensureCovered = null)
        {
            _grid = grid;
            _trail = trail;
            _totem = totem;
            _ensureCovered = ensureCovered;
            _trail.Advanced += OnAdvanced;
            Scan();
        }

        public void Dispose() => _trail.Advanced -= OnAdvanced;

        private void OnAdvanced(GridCoordinate coordinate) => Scan();

        /// <summary>Раскрывает типы триггеров доступных групп в ближайших рядах. Возвращает число раскрытых.</summary>
        public int Scan()
        {
            var max = _totem.MaangMaxGroup;
            if (max <= 0) return 0;
            var from = _trail.CurrentPosition.Row;
            _ensureCovered?.Invoke(from + TotemEffects.MaangLookaheadRows);
            var revealed = 0;
            for (var row = from; row <= from + TotemEffects.MaangLookaheadRows; row++)
                for (var column = 0; column < _grid.Width; column++)
                    if (_grid.TryGetTile(new GridCoordinate(row, column), out var tile))
                    {
                        var kind = TrapGroups.KindOf(tile);
                        if (kind.HasValue && TrapGroups.GroupOf(kind.Value) <= max && !tile.IsTrapTypeRevealed)
                        {
                            tile.RevealTrapType();
                            revealed++;
                        }
                    }

            return revealed;
        }
    }
}
