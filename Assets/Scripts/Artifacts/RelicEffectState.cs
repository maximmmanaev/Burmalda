using System;
using System.Collections.Generic;
using System.Linq;
using Burmalda.Core;

namespace Burmalda.Artifacts
{
    /// <summary>
    /// Эффекты Реликвий славянских богов (Леший, Мокошь, Перун, Чернобог; PRD v10 §20.2), вычисляемые по билду забега.
    /// Числа — ЧЕРНОВЫЕ [CONFIG] (статические поля). Руны активируются тапом по иконке в HUD (§20.4): здесь хранятся
    /// «использована ли» и таймеры Мороков/Ночи, а действие на мир (стена, раскрытие рядов) выполняет композиционный корень.
    /// Ни один эффект не даёт иммунитета к Лаве и не меняет Бросок Судьбы.
    /// </summary>
    public sealed class RelicEffectState : IRunEffects
    {
        public const string BereskovyAmulet = "relic-leshy-amulet";
        public const string LeshyTalisman = "relic-leshy-talisman";
        public const string MorokRune = "relic-leshy-rune";
        public const string MokoshAmulet = "relic-mokosh-amulet";
        public const string SpindleTalisman = "relic-mokosh-talisman";
        public const string RasputeRune = "relic-mokosh-rune";
        public const string PerunAmulet = "relic-perun-amulet";
        public const string ThunderTalisman = "relic-perun-talisman";
        public const string PerunStrikeRune = "relic-perun-rune";
        public const string ChernobogAmulet = "relic-chernobog-amulet";
        public const string BlackGoldTalisman = "relic-chernobog-talisman";
        public const string NightRune = "relic-chernobog-rune";

        // ---- Числа [CONFIG, черновик] ----
        public static float WallSlowdownValue = 1.5f;
        public static float StreakBonusMultiplierValue = 2f;
        public static float ManaMultiplierValue = 1.5f;
        public static float DecayRateValue = 1.25f;
        public static float ThunderResonanceBonus = 0.8f;
        public static float BearTrapEscapeValue = 2f;
        public static int SpindleEveryTiles = 5;
        public static int SpindleBonusMana = 50;
        public static float MorokSeconds = 5f;
        public static float NightSeconds = 3f;
        public static int PerunStrikeWalls = 1;
        public static int RasputeRows = 5;

        private readonly Func<RunRelicBuild> _build;
        private readonly Func<float> _now;
        private readonly Func<int> _yarus;
        private readonly Dictionary<string, int> _absorbedAtYarus = new Dictionary<string, int>();
        private readonly HashSet<string> _usedRunes = new HashSet<string>();
        private float _morokUntil = float.NegativeInfinity;
        private float _nightUntil = float.NegativeInfinity;

        public RelicEffectState(Func<RunRelicBuild> build, Func<float> now, Func<int> yarus)
        {
            _build = build ?? throw new ArgumentNullException(nameof(build));
            _now = now ?? throw new ArgumentNullException(nameof(now));
            _yarus = yarus ?? throw new ArgumentNullException(nameof(yarus));
        }

        public bool Has(string relicId) => _build().Contains(relicId);

        public float WallCloseSlowdown => Has(BereskovyAmulet) ? WallSlowdownValue : 1f;

        public float ThreatTimeScale => 1f;

        public float CollapseSpeedMultiplier => 1f;

        public float SourceManaMultiplier => 1f;

        public float VeinManaMultiplier => 1f;

        public float ManaMultiplier => Has(BlackGoldTalisman) ? ManaMultiplierValue : 1f;

        public float StreakBonusMultiplier => Has(LeshyTalisman) ? StreakBonusMultiplierValue : 1f;

        public float DecayRateMultiplier => Has(BlackGoldTalisman) ? DecayRateValue : 1f;

        public float ResonanceBonus => Has(ThunderTalisman) ? ThunderResonanceBonus : NeutralRunEffects.BaseResonanceBonus;

        public float BearTrapEscapeMultiplier => Has(ChernobogAmulet) ? BearTrapEscapeValue : 1f;

        public bool StepsActivateTraps => !IsMorokActive;

        public bool CollapseHeld => IsNightActive;

        public int BonusManaForStep(int stepNumber) =>
            Has(SpindleTalisman) && SpindleEveryTiles > 0 && stepNumber > 0 && stepNumber % SpindleEveryTiles == 0 ? SpindleBonusMana : 0;

        /// <summary>Плат Мокоши поглощает любое попадание ловушки, Перунов знак — Огненную струю; каждый — раз за Ярус.</summary>
        public bool TryAbsorbHit(ThreatType threat)
        {
            var yarus = _yarus();
            foreach (var amulet in new[] { MokoshAmulet, PerunAmulet })
            {
                if (!Has(amulet)) continue;
                if (amulet == PerunAmulet && threat != ThreatType.Fire) continue;
                if (_absorbedAtYarus.TryGetValue(amulet, out var used) && used == yarus) continue; // заряд этого Яруса уже потрачен
                _absorbedAtYarus[amulet] = yarus;
                return true;
            }

            return false;
        }

        // ---- Руны: тап по иконке в HUD ----

        public bool IsMorokActive => _now() < _morokUntil;

        public bool IsNightActive => _now() < _nightUntil;

        public float MorokRemaining => Math.Max(0f, _morokUntil - _now());

        /// <summary>Руна есть в билде и ещё не использована (разовая способность, §20.4).</summary>
        public bool CanUseRune(string runeId) => Has(runeId) && !_usedRunes.Contains(runeId);

        /// <summary>Тратит Руну. Для Мороков и Ночи Чернобога запускает таймер. False, если Руны нет или она использована.</summary>
        public bool TryUseRune(string runeId)
        {
            if (!CanUseRune(runeId)) return false;
            _usedRunes.Add(runeId);
            if (runeId == MorokRune) _morokUntil = _now() + MorokSeconds;
            else if (runeId == NightRune) _nightUntil = _now() + NightSeconds;
            return true;
        }

        public IEnumerable<string> UsedRunes => _usedRunes;
    }
}
