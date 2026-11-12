using System;
using System.Collections.Generic;
using Burmalda.Altar;
using Burmalda.Artifacts;
using Burmalda.Bootstrap;
using Burmalda.Camp;
using Burmalda.Core;
using Burmalda.Currencies;
using Burmalda.Decay;
using Burmalda.FateRoll;
using Burmalda.Generation;
using Burmalda.Movement;
using Burmalda.RunLifecycle;
using Burmalda.Sanctuary;

namespace Burmalda.Playtest
{
    /// <summary>Причина смерти (до Броска Судьбы) — для метрик бота.</summary>
    public enum HazardCause
    {
        Rock,
        Wall,
        Arrows,
        Blades,
        Bomb,
        Lava,
        Decay,
        Collapse,
    }

    /// <summary>Чем закончился забег бота.</summary>
    public enum RunOutcome
    {
        Running,
        Evacuated,
        Died,
        /// <summary>Бот не смог ни идти, ни эвакуироваться за отведённое время — ошибка бота, не игры.</summary>
        Stuck,
    }

    /// <summary>Одна смерть (до Броска Судьбы): где, от чего и чем кончился бросок.</summary>
    public sealed class HazardRecord
    {
        public HazardCause Cause;
        public int Yarus;
        public bool InSanctuary;
        public float Time;
        /// <summary>Где стоял игрок в момент смерти (до телепорта Броска).</summary>
        public GridCoordinate Position;
        /// <summary>null — бросков не осталось, смерть окончательна без броска.</summary>
        public FateOutcome? Fate;
    }

    /// <summary>Итог по одному Ярусу забега.</summary>
    public sealed class YarusRecord
    {
        public bool Entered;
        public bool Cleared;
        public float Seconds;
        public int Hazards;
        public int FateRolls;
        public bool SarcophagusTaken;
        public bool SanctuaryEntered;
        public int SanctuaryHazards;
        public float SanctuarySeconds;
        public string SanctuaryLayout;
    }

    /// <summary>
    /// Забег без рендера и без MonoBehaviour: собирает те же системы, что <c>Bootstrap.RunBootstrap</c>
    /// (в том же порядке подписки на события), и тикает их вручную общими часами <see cref="Now"/>.
    /// Правила игры здесь не дублируются — ходы идут через <c>GridTraceTrail.TryAdvanceTo</c>, смерти —
    /// через <c>RunState</c>, эвакуация — через <c>EvacuationSystem</c>. Если состав контроллеров в
    /// <c>RunBootstrap</c> меняется, эту сборку нужно обновить вместе с ним.
    /// Экраны Алтаря и Броска Судьбы останавливают время (PRD §7.2): бросок делается сразу, часы не идут.
    /// </summary>
    public sealed class PlaytestRun : IDisposable, IAltarContext
    {
        public const int Width = 5;

        /// <summary>Шаг часов симуляции. Тайминги ловушек (0.15 / 0.3 с) кратны ему.</summary>
        public const float TickSeconds = 0.05f;

        /// <summary>Длительность шага — как <c>GridTraceInputController._stepDurationSeconds</c> (PRD §7.2).</summary>
        public const float StepSeconds = 0.3f;

        private readonly PlaytestProfile _profile;
        private readonly Random _dice;
        private readonly List<IDisposable> _systems = new List<IDisposable>();
        private readonly TrapRevealSystem _reveal;
        private readonly AltarPickFlow _pickFlow;
        private readonly SanctuaryRunSystem _sanctuary;
        private readonly ArrowWaveTrapSystem _arrowWave;
        private readonly BombTrapSystem _bomb;
        private readonly BladeTactTrapSystem _bladeTact;
        private readonly FallingRockTrapSystem _fallingRock;
        private readonly MovingWallTrap _movingWall;
        private readonly IdolEffects _idolEffects;
        private readonly IdolChargeSystem _idolCharge;
        private readonly IdolRunSystem _idolRun;
        private readonly TotemEffects _totemEffects;
        private readonly RuneActions _runes;
        private readonly YarusRecord[] _yarus = new YarusRecord[GodTable.YarusCount + 1];
        private HazardCause _pendingCause;
        private HazardRecord _openHazard;
        private float _carry;

        public PlaytestRun(int seed, PlaytestProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Seed = seed;
            _dice = new Random(unchecked(seed * 486187739 + 17));
            for (var i = 0; i < _yarus.Length; i++) _yarus[i] = new YarusRecord();

            Grid = new TunnelGrid(Width);
            Trail = new GridTraceTrail(Grid, new GridCoordinate(0, Width / 2));

            Decay = new TrailDecaySystem(Grid, Trail);
            _systems.Add(Decay);
            // Причину запоминаем ДО RunState — его подписки на те же события идут следом.
            Trail.LethalTrapTriggered += (_, type) => _pendingCause = CauseOf(type);
            Decay.TileDestroyed += c => { if (c == Trail.CurrentPosition) _pendingCause = HazardCause.Decay; };
            RunState = new RunState(Grid, Trail, Decay, () => _dice.Next(1, 7));
            _systems.Add(RunState);
            RunState.FateRollOffered += _ => _openHazard = RecordHazard();
            RunState.FateRolled += OnFateRolled;
            RunState.Died += OnDied;

            Provider = new SegmentRowProvider(Grid, Trail, new SegmentSelector(SegmentTemplateCatalog.All, new RunSeed(seed)),
                _ => 1, YarusProgression.TunnelRows(1), YarusProgression.TunnelRows);
            _systems.Add(Provider);

            Mana = new RunCurrencyAccumulator();
            var multiplier = new TrailMultiplierSystem(Trail);
            _systems.Add(multiplier);
            _systems.Add(new TrailManaIncomeSystem(Trail, multiplier, Mana));
            _systems.Add(new TrailTileCurrencySystem(Grid, Trail, t => t.IsManaSource, CurrencyController.ManaPerSource, Mana));
            Streak = new CleanPassStreakSystem(Grid, Trail, Mana);
            _systems.Add(Streak);

            Build = new RunRelicBuild();
            Relics = new RelicEffectState(() => Build, () => Now, () => Provider.CurrentYarus);
            RunEffects.SetLayer("relics", Relics);
            var vault = new VaultBonusTracker(Grid, Trail);
            _systems.Add(vault);
            _pickFlow = new AltarPickFlow(profile.Pool, Build, vault, this);
            var altarTrigger = new AltarTriggerSystem(Grid, Trail);
            _systems.Add(altarTrigger);
            altarTrigger.AltarReached += c =>
            {
                ActiveSession = _pickFlow.OnAltarReached(c);
                if (ActiveSession != null) ActiveSession.Completed += _ => ActiveSession = null;
            };

            Carried = new RunSarcophagi();
            Evacuation = new EvacuationSystem(Grid, Trail, Mana, profile.Coins, Carried, profile.Camp.AddUnopened);
            Evacuation.Returned += r => { CoinsFromMana = r.TotalCoins; ManaConverted = r.Mana; };

            var progress = profile.Camp.Progress;
            progress.AutoEquipIfEmpty();
            _idolEffects = new IdolEffects(() => Now);
            RunEffects.SetLayer("idol", _idolEffects);
            _idolCharge = new IdolChargeSystem(Streak, () => progress.ActiveIdolId != null);
            _systems.Add(_idolCharge);
            _idolRun = new IdolRunSystem(progress, () => _idolCharge.IsFull, () => _idolCharge.TryConsume(),
                new IdolAbilityContext(Grid, Trail, _idolEffects, () => Now), IdolAbilities.All());
            Trail.PositionChanged += _ => _idolEffects.NoteStep();

            _totemEffects = new TotemEffects(progress, () => Provider.CurrentYarus);
            RunEffects.SetLayer("totem", _totemEffects);
            _systems.Add(new TotemRevealSystem(Grid, Trail, _totemEffects, Provider.EnsureCoveredThrough));
            Trail.PositionChanged += _ => _totemEffects.NoteStep();

            _systems.Add(new LeverActivationSystem(Grid, Trail));

            _arrowWave = new ArrowWaveTrapSystem(Grid, Trail, new RealTimeThreatScheduler());
            _bomb = new BombTrapSystem(Grid, Trail, new RealTimeThreatScheduler(), () => Math.Max(0, Provider.CurrentYarus - 1));
            _bladeTact = new BladeTactTrapSystem(Grid, Trail, new RealTimeThreatScheduler());
            _fallingRock = new FallingRockTrapSystem(Grid, Trail, new RealTimeThreatScheduler());
            _fallingRock.PlayerCrushed += _ =>
            {
                _pendingCause = HazardCause.Rock;
                RunState.ReportFallingRockCrush();
            };
            _movingWall = new MovingWallTrap(Grid, Trail, new RealTimeThreatScheduler());
            _systems.AddRange(new IDisposable[] { _arrowWave, _bomb, _bladeTact, _fallingRock, _movingWall });

            _sanctuary = new SanctuaryRunSystem(Grid, Trail, Mana, () =>
            {
                _pendingCause = HazardCause.Collapse;
                RunState.ReportSanctuaryCollapse();
            });
            _systems.Add(_sanctuary);
            _sanctuary.Exited += s => _yarus[Provider.YarusAtRow(s.EntryRow)].Cleared = true;
            _sanctuary.SarcophagusTaken += () =>
            {
                var yarus = Provider.CurrentYarus;
                _yarus[yarus].SarcophagusTaken = true;
                Carried.Add(SarcophagusRarityRoller.Take(yarus, profile.Rng));
                SarcophagiTaken++;
            };

            _reveal = new TrapRevealSystem(Grid);
            _runes = new RuneActions(Relics, Grid, Trail, Provider.EnsureCoveredThrough);
            _yarus[1].Entered = true;
        }

        public int Seed { get; }
        public TunnelGrid Grid { get; }
        public GridTraceTrail Trail { get; }
        public TrailDecaySystem Decay { get; }
        public RunState RunState { get; }
        public SegmentRowProvider Provider { get; }
        public RunCurrencyAccumulator Mana { get; }
        public CleanPassStreakSystem Streak { get; }
        public RunRelicBuild Build { get; }
        public RelicEffectState Relics { get; }
        public RunSarcophagi Carried { get; }
        public EvacuationSystem Evacuation { get; }

        /// <summary>Часы забега, секунды. Стоят на экранах Алтаря и Броска Судьбы.</summary>
        public float Now { get; private set; }

        public RunOutcome Outcome { get; private set; } = RunOutcome.Running;
        public bool IsOver => Outcome != RunOutcome.Running;

        /// <summary>Открытый выбор Реликвии на Алтаре; бот обязан закрыть его до следующего хода.</summary>
        public AltarPickSession ActiveSession { get; private set; }

        /// <summary>Активное (ещё не пройденное) Святилище или null.</summary>
        public Sanctuary.Sanctuary ActiveSanctuary => _sanctuary.ActiveSanctuary != null && _sanctuary.ActiveSanctuary.IsActive ? _sanctuary.ActiveSanctuary : null;

        public bool IdolCharged => _idolCharge.IsFull;
        public string ActiveIdolId => _profile.Camp.Progress.ActiveIdolId;

        public IReadOnlyList<HazardRecord> Hazards => _hazards;
        private readonly List<HazardRecord> _hazards = new List<HazardRecord>();

        public YarusRecord Yarus(int yarus) => _yarus[yarus];
        public int Steps { get; private set; }
        public int FateRollsUsed => RunState.FateRollsUsed;

        /// <summary>Сколько раз Бросок Судьбы вернул игрока к последнему Алтарю.</summary>
        public int ReturnsToAltar { get; private set; }
        public HazardCause? DeathCause { get; private set; }
        public int DeathYarus { get; private set; }
        public int SarcophagiTaken { get; private set; }
        public int SarcophagiEvacuated { get; private set; }
        public int SarcophagiLost { get; private set; }
        public int ManaConverted { get; private set; }
        public int ManaLost { get; private set; }
        public int CoinsFromMana { get; private set; }

        /// <summary>Успешный шаг бота: откуда и куда. Телепорты Броска Судьбы и прыжки Идола сюда не попадают.</summary>
        public event Action<GridCoordinate, GridCoordinate> Stepped;

        /// <summary>Пропускает время; false — если прервано смертью (Бросок Судьбы уже сделан) или концом забега.</summary>
        public bool Advance(float seconds)
        {
            var hazardsBefore = _hazards.Count;
            _carry += seconds;
            while (_carry >= TickSeconds - 1e-6f && !IsOver)
            {
                _carry -= TickSeconds;
                Tick(TickSeconds);
                ResolveFate();
                if (_hazards.Count != hazardsBefore)
                {
                    _carry = 0f;
                    return false;
                }
            }

            return !IsOver;
        }

        /// <summary>Шаг на соседнюю плиту по правилам игры; после успешного шага ввод заблокирован на <see cref="StepSeconds"/>.</summary>
        public bool Step(GridCoordinate target)
        {
            if (IsOver || ActiveSession != null) return false;
            var from = Trail.CurrentPosition;
            var moved = Trail.TryAdvanceTo(target);
            ResolveFate();
            if (!moved) return false;
            Steps++;
            Stepped?.Invoke(from, target);
            if (!IsOver) Advance(StepSeconds);
            return true;
        }

        /// <summary>Примеривание (PRD §7.4): раскрывает сигнатуру опасности плиты. true — на плите известный триггер.</summary>
        public bool Probe(GridCoordinate target)
        {
            _reveal.Tick(target);
            return Grid.TryGetTile(target, out var tile) && tile.IsTrapTrigger && tile.IsDangerSignatureRevealed;
        }

        public bool CanEvacuate => !IsOver && Evacuation.CanEvacuate;

        public bool Evacuate()
        {
            if (IsOver) return false;
            var carried = Carried.Count;
            if (!Evacuation.Evacuate()) return false;
            SarcophagiEvacuated = carried;
            Outcome = RunOutcome.Evacuated;
            return true;
        }

        /// <summary>Двойной тап — Идол. Для прицельных способностей нужна цель; неверная цель заряд не тратит.</summary>
        public bool UseIdol(GridCoordinate? aim = null)
        {
            if (IsOver || !_idolCharge.IsFull || !_idolRun.OnDoubleTap()) return false;
            if (!_idolRun.IsTargeting) return true;
            if (aim.HasValue && _idolRun.OnTargetReleased(aim.Value))
            {
                ResolveFate();
                return true;
            }

            _idolRun.Reset();
            return false;
        }

        public bool UseRune(string runeId) => !IsOver && _runes.Activate(runeId);

        /// <summary>Бот сдаётся: не может ни идти, ни эвакуироваться.</summary>
        public void MarkStuck()
        {
            if (!IsOver) Outcome = RunOutcome.Stuck;
        }

        public void Dispose()
        {
            foreach (var system in _systems) system.Dispose();
            _systems.Clear();
            if (RunEffects.HasLayer("relics", Relics)) RunEffects.SetLayer("relics", null);
            if (RunEffects.HasLayer("idol", _idolEffects)) RunEffects.SetLayer("idol", null);
            if (RunEffects.HasLayer("totem", _totemEffects)) RunEffects.SetLayer("totem", null);
        }

        // Порядок — как порядок Update контроллеров в RunBootstrap: Decay, связки Camp/Yarus/Totem, ловушки, Святилище.
        private void Tick(float dt)
        {
            Now += dt;
            var yarus = Provider.CurrentYarus;
            var record = _yarus[yarus];
            record.Entered = true;
            record.Seconds += dt;
            var sanctuary = ActiveSanctuary;
            if (sanctuary != null)
            {
                record.SanctuaryEntered = true;
                record.SanctuarySeconds += dt;
            }

            Decay.YarusLifetimeMultiplier = YarusProgression.DecayLifetimeMultiplier(yarus);
            Decay.CarriedSarcophagi = Carried.Count;
            RunState.FateModifier = _totemEffects.FateBonus;

            var threatDt = dt * RunEffects.Current.ThreatTimeScale;
            Decay.Tick(threatDt);
            _arrowWave.Tick(threatDt);
            _bomb.Tick(threatDt);
            _bladeTact.Tick(threatDt);
            _fallingRock.Tick(threatDt);
            _movingWall.Tick(threatDt);
            _sanctuary.Tick(dt);
            _idolEffects.CommitStep();
            _totemEffects.CommitStep();
        }

        // Все политики бросают, пока броски есть: оплата — заглушка (FateRollRules.FateRollPaymentStub).
        private void ResolveFate()
        {
            while (RunState.IsAlive && RunState.IsAwaitingFate)
                if (!RunState.RollFate())
                {
                    RunState.DeclineFate();
                    break;
                }
        }

        private HazardRecord RecordHazard()
        {
            var yarus = Provider.CurrentYarus;
            var inSanctuary = ActiveSanctuary != null;
            var hazard = new HazardRecord { Cause = _pendingCause, Yarus = yarus, InSanctuary = inSanctuary, Time = Now, Position = Trail.CurrentPosition };
            _hazards.Add(hazard);
            _yarus[yarus].Hazards++;
            if (inSanctuary) _yarus[yarus].SanctuaryHazards++;
            return hazard;
        }

        private void OnFateRolled(FateRollResult result)
        {
            if (_openHazard == null) return;
            _openHazard.Fate = result.Outcome;
            if (result.Outcome == FateOutcome.ToAltar) ReturnsToAltar++;
            _yarus[_openHazard.Yarus].FateRolls++;
            if (result.Outcome != FateOutcome.Death) _openHazard = null;
        }

        private void OnDied(string reason)
        {
            var hazard = _openHazard ?? RecordHazard(); // бросков не осталось — смерть без предложения
            _openHazard = null;
            DeathCause = hazard.Cause;
            DeathYarus = hazard.Yarus;
            SarcophagiLost = Carried.Count;
            ManaLost = Mana.Total;
            Evacuation.LoseAll();
            Outcome = RunOutcome.Died;
        }

        private static HazardCause CauseOf(LethalTrapType type) => type switch
        {
            LethalTrapType.ArrowWave => HazardCause.Arrows,
            LethalTrapType.BombBlast => HazardCause.Bomb,
            LethalTrapType.BladeTact => HazardCause.Blades,
            LethalTrapType.MovingWallCrush => HazardCause.Wall,
            _ => HazardCause.Lava,
        };

        // IAltarContext — то же, что Bootstrap.AltarContextAdapter, но поверх SegmentRowProvider напрямую.
        bool IAltarContext.TryGetSpot(GridCoordinate coordinate, out int yarus, out int ordinal) =>
            Provider.TryGetAltarSpot(coordinate.Row, out yarus, out ordinal);

        IReadOnlyCollection<ThreatType> IAltarContext.AfishaThreats(int shrineYarus) =>
            AltarContextAdapter.ThreatsOf(SegmentTemplateCatalog.SanctuaryLeshyTemplate);

        int IAltarContext.RunSeed => Seed;

        int IAltarContext.CurrentYarus => Provider.CurrentYarus;
    }
}
