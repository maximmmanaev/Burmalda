using System;
using System.Collections.Generic;
using System.Linq;
using Burmalda.Altar;
using Burmalda.Artifacts;
using Burmalda.Camp;
using Burmalda.Core;

namespace Burmalda.Playtest
{
    public enum BotPolicyKind
    {
        Cautious,
        Greedy,
        Skilled,
    }

    /// <summary>
    /// Параметры политики бота. Все числа — ЧЕРНОВЫЕ допущения о поведении игрока, не баланс игры.
    /// Правила политик описаны в docs/wiki/playtest-bot.md.
    /// </summary>
    public sealed class BotPolicyConfig
    {
        public BotPolicyKind Kind;

        /// <summary>Пауза «подумать» перед шагом, секунды (мин, макс): тоннель / Святилище / сразу после ловушки.</summary>
        public float ThinkTunnelMin, ThinkTunnelMax;
        public float ThinkSanctuaryMin, ThinkSanctuaryMax;
        public float ThinkUrgentMin, ThinkUrgentMax;

        /// <summary>Доля шагов, перед которыми бот примеривается к плите (раскрывает сигнатуру опасности).</summary>
        public double ProbeChance;

        /// <summary>Сколько секунд занимает одно примеривание (цена информации — время, PRD §7.4).</summary>
        public float ProbeSeconds;

        /// <summary>Во сколько шагов бот оценивает наступание на раскрытый триггер при выборе маршрута.</summary>
        public float TriggerPenalty;

        /// <summary>Сколько лишних шагов бот готов сделать ради источника Маны в тоннеле; −1 — не подбирает.</summary>
        public int ManaDetourSteps;

        /// <summary>То же для плит множителя Святилища.</summary>
        public int SanctuaryDetourSteps;

        public bool UsesIdolAndRunes;

        /// <summary>Опытный игрок узнаёт шаблон и знает тип раскрытого триггера без подсказок.</summary>
        public bool KnowsTrapTypes;

        public static BotPolicyConfig For(BotPolicyKind kind) => kind switch
        {
            BotPolicyKind.Cautious => new BotPolicyConfig
            {
                Kind = kind,
                ThinkTunnelMin = 0.5f, ThinkTunnelMax = 0.9f,
                ThinkSanctuaryMin = 0.25f, ThinkSanctuaryMax = 0.45f,
                ThinkUrgentMin = 0.15f, ThinkUrgentMax = 0.3f,
                ProbeChance = 1.0, ProbeSeconds = 0.25f, TriggerPenalty = 50f,
                ManaDetourSteps = 0, SanctuaryDetourSteps = 0,
            },
            BotPolicyKind.Greedy => new BotPolicyConfig
            {
                Kind = kind,
                ThinkTunnelMin = 0.1f, ThinkTunnelMax = 0.25f,
                ThinkSanctuaryMin = 0.1f, ThinkSanctuaryMax = 0.2f,
                ThinkUrgentMin = 0.1f, ThinkUrgentMax = 0.2f,
                ProbeChance = 0.0, TriggerPenalty = 3f,
                ManaDetourSteps = 3, SanctuaryDetourSteps = 2,
            },
            _ => new BotPolicyConfig
            {
                Kind = kind,
                ThinkTunnelMin = 0.2f, ThinkTunnelMax = 0.4f,
                ThinkSanctuaryMin = 0.1f, ThinkSanctuaryMax = 0.25f,
                ThinkUrgentMin = 0.05f, ThinkUrgentMax = 0.15f,
                ProbeChance = 0.9, ProbeSeconds = 0.12f, TriggerPenalty = 50f,
                ManaDetourSteps = 1, SanctuaryDetourSteps = 1,
                UsesIdolAndRunes = true, KnowsTrapTypes = true,
            },
        };
    }

    /// <summary>
    /// Черновые числа правила эвакуации Skilled («риск следующего Яруса × ценность несомого»).
    /// Риск — доля окончательных смертей на Ярусе; стартовые значения — из PRD §24 (93/87/78%).
    /// </summary>
    public static class BotEvacuationModel
    {
        public static double[] DeathRiskByYarus = { 0.07, 0.07, 0.13, 0.22, 0.25, 0.3, 0.35, 0.4, 0.45, 0.5, 0.5 };

        /// <summary>Во сколько раз растёт риск с каждым уже потраченным Броском Судьбы.</summary>
        public static double[] RiskFactorByFateRollsUsed = { 1.0, 1.5, 2.5, 5.0 };

        /// <summary>Ценность Саркофага в Монетах по рангу бога (младший, средний, старший): Монеты внутри плюс шанс части/Реликвии.</summary>
        public static int[] SarcophagusValueByRank = { 300, 500, 900 };

        /// <summary>Сколько Монет в среднем даёт Мана одного пройденного Яруса.</summary>
        public static int ManaCoinsPerYarus = 80;
    }

    /// <summary>
    /// Бот-игрок: эвристика, не идеальная игра. Видит то же, что игрок (PRD §7.4): стены, Лаву, награды,
    /// распад, предупреждения ловушек; триггеры — только после примеривания или наступания.
    /// Ходит только через <see cref="PlaytestRun.Step"/>, то есть по правилам игры.
    /// </summary>
    public sealed class PlaytestBot
    {
        /// <summary>Сколько рядов вперёд видит игрок (PRD §7.3: 6–8).</summary>
        public const int VisibleRows = 8;

        private const int RowsBack = 3;
        private const float MaxWaitSeconds = 30f;

        private static readonly (int dr, int dc)[] Directions =
        {
            (1, 0), (1, -1), (1, 1), (0, -1), (0, 1), (-1, -1), (-1, 1), (-1, 0),
        };

        private readonly PlaytestRun _run;
        private readonly BotPolicyConfig _config;
        private readonly PlaytestProfile _profile;
        private readonly Random _rng;
        private readonly HashSet<GridCoordinate> _firedTriggers = new HashSet<GridCoordinate>();
        private GridCoordinate _lastPosition;
        private GridCoordinate? _decidedAltar;
        private int _urgentSteps;
        private float _waited;

        public PlaytestBot(PlaytestRun run, BotPolicyConfig config, PlaytestProfile profile, Random rng)
        {
            _run = run;
            _config = config;
            _profile = profile;
            _rng = rng;
            _lastPosition = run.Trail.CurrentPosition;
        }

        private int _seenReturns;

        public void Play(float maxSeconds = 1800f)
        {
            while (!_run.IsOver)
            {
                if (_run.Now > maxSeconds || _waited > MaxWaitSeconds)
                {
                    _run.MarkStuck();
                    return;
                }

                NoteMovement();
                if (_run.ActiveSession != null) PickRelic(_run.ActiveSession);
                // Бросок вернул к Алтарю: маршрут впереди разрушен собственным следом (а Святилище — обрушением),
                // поэтому любая политика уносит то, что есть (PRD §16.2).
                if (_run.ReturnsToAltar != _seenReturns)
                {
                    _seenReturns = _run.ReturnsToAltar;
                    if (_run.Evacuate()) return;
                }

                if (_run.CanEvacuate && _decidedAltar != Position)
                {
                    _decidedAltar = Position;
                    if (ShouldEvacuate() && _run.Evacuate()) return;
                }

                if (_config.UsesIdolAndRunes)
                {
                    UseAbilities();
                    NoteMovement();
                    if (_run.IsOver) return;
                }

                var target = ChooseStep();
                if (!target.HasValue)
                {
                    if (_run.CanEvacuate && _run.Evacuate()) return; // идти некуда — уносим, что есть
                    _waited += 0.2f;
                    _run.Advance(0.2f);
                    continue;
                }

                // Примеривание: стоит времени; если показало триггер — маршрут пересчитывается.
                // Когда плита под ногами вот-вот распадётся, бот не примеривается, а идёт.
                if (!FloorIsFailing() && !IsRevealed(target.Value) && _rng.NextDouble() < _config.ProbeChance)
                {
                    var foundTrigger = _run.Probe(target.Value);
                    if (!_run.Advance(_config.ProbeSeconds) || foundTrigger) continue;
                }

                if (!_run.Advance(Think())) continue; // за время раздумья что-то убило — план устарел
                if (EnterCost(Position, target.Value) < 0f) continue; // плита закрылась, пока думали
                if (_run.Step(target.Value))
                {
                    _waited = 0f;
                    if (_urgentSteps > 0) _urgentSteps--;
                }
            }
        }

        private GridCoordinate Position => _run.Trail.CurrentPosition;

        // Уход с триггера (шагом, прыжком Идола или телепортом Броска) запускает ловушку — дальше надо спешить.
        private void NoteMovement()
        {
            if (Position == _lastPosition) return;
            _decidedAltar = null; // вернувшись на Алтарь (Бросок Судьбы), бот решает заново
            if (IsLiveTrigger(_lastPosition))
            {
                _firedTriggers.Add(_lastPosition);
                _urgentSteps = 2;
            }

            _lastPosition = Position;
        }

        private float Think()
        {
            float min, max;
            if (_urgentSteps > 0 || IsLiveTrigger(Position) || FloorIsFailing()) (min, max) = (_config.ThinkUrgentMin, _config.ThinkUrgentMax);
            else if (_run.ActiveSanctuary != null) (min, max) = (_config.ThinkSanctuaryMin, _config.ThinkSanctuaryMax);
            else (min, max) = (_config.ThinkTunnelMin, _config.ThinkTunnelMax);
            return min + (float)_rng.NextDouble() * (max - min);
        }

        private bool FloorIsFailing() => _run.Grid.TryGetTile(Position, out var tile) && tile.DecayProgress01 > 0.6f;

        private bool IsRevealed(GridCoordinate c) => _run.Grid.TryGetTile(c, out var tile) && tile.IsDangerSignatureRevealed;

        /// <summary>Известный боту и ещё не сработавший триггер.</summary>
        private bool IsLiveTrigger(GridCoordinate c) =>
            _run.Grid.TryGetTile(c, out var tile) && tile.IsTrapTrigger && tile.IsDangerSignatureRevealed && !_firedTriggers.Contains(c);

        /// <summary>Цена шага на плиту; отрицательная — шаг невозможен или заведомо смертелен.</summary>
        private float EnterCost(GridCoordinate from, GridCoordinate to)
        {
            if (!_run.Grid.Contains(to) || !_run.Grid.TryGetTile(to, out var tile)) return -1f;
            if (to.Row > Position.Row + VisibleRows) return -1f;
            if (tile.IsBlocked || (tile.IsGated && !tile.IsLeverGateOpen)) return -1f;
            if (tile.LethalTrap.HasValue || tile.IsFallingRockWarningActive || tile.IsBombWarningActive) return -1f;
            var cost = 1f;
            if (IsLiveTrigger(to)) cost += _config.TriggerPenalty;
            if (_run.Trail.HasVisited(to))
            {
                if (tile.DecayProgress01 > 0.5f) return -1f; // вот-вот станет Лавой
                cost += 3f;
            }

            if (to.Row < from.Row) cost += 0.5f;
            return cost;
        }

        private GridCoordinate? ChooseStep()
        {
            var position = Position;
            if (IsLiveTrigger(position)) return ChooseTriggerExit(position);

            var lowRow = Math.Max(0, position.Row - RowsBack);
            var rows = position.Row + VisibleRows - lowRow + 1;
            var count = rows * PlaytestRun.Width;
            var dist = new float[count];
            var prev = new int[count];
            var done = new bool[count];
            for (var i = 0; i < count; i++)
            {
                dist[i] = float.MaxValue;
                prev[i] = -1;
            }

            int Index(GridCoordinate c) => (c.Row - lowRow) * PlaytestRun.Width + c.Column;
            GridCoordinate At(int index) => new GridCoordinate(lowRow + index / PlaytestRun.Width, index % PlaytestRun.Width);

            var start = Index(position);
            dist[start] = 0f;
            while (true)
            {
                var current = -1;
                for (var i = 0; i < count; i++)
                    if (!done[i] && dist[i] < float.MaxValue && (current < 0 || dist[i] < dist[current])) current = i;
                if (current < 0) break;
                done[current] = true;
                var from = At(current);
                foreach (var (dr, dc) in Directions)
                {
                    var to = new GridCoordinate(from.Row + dr, from.Column + dc);
                    if (to.Row < lowRow || to.Row >= lowRow + rows || to.Column < 0 || to.Column >= PlaytestRun.Width) continue;
                    var cost = EnterCost(from, to);
                    if (cost < 0f) continue;
                    var next = Index(to);
                    if (dist[current] + cost < dist[next])
                    {
                        dist[next] = dist[current] + cost;
                        prev[next] = current;
                    }
                }
            }

            var goal = ChooseGoal(position, dist, prev, At, count);
            if (goal < 0) return null;
            while (prev[goal] != start) goal = prev[goal];
            return At(goal);
        }

        private int ChooseGoal(GridCoordinate position, float[] dist, int[] prev, Func<int, GridCoordinate> at, int count)
        {
            var sanctuary = _run.ActiveSanctuary;
            var detour = sanctuary != null ? _config.SanctuaryDetourSteps : _config.ManaDetourSteps;
            // Обрушение наступает на пятки — не до наград.
            if (_urgentSteps > 0 || (sanctuary != null && position.Row - sanctuary.LastCollapsedRow < 3)) detour = -1;

            int mandatory = -1, frontier = -1, lever = -1;
            var pickups = new List<int>();
            for (var i = 0; i < count; i++)
            {
                if (dist[i] == float.MaxValue || dist[i] == 0f) continue;
                var c = at(i);
                _run.Grid.TryGetTile(c, out var tile);
                var unvisited = !_run.Trail.HasVisited(c);

                // Алтарь и Саркофаг обязательны; в Святилище за пропущенным Саркофагом назад не ходят.
                var must = (tile.IsAltar && unvisited) || (tile.HasSarcophagus && (sanctuary == null || c.Row >= position.Row));
                if (must && (mandatory < 0 || c.Row < at(mandatory).Row || (c.Row == at(mandatory).Row && dist[i] < dist[mandatory]))) mandatory = i;

                var reward = unvisited && (tile.IsManaSource || tile.IsGateVault || tile.SanctuaryTile.HasValue);
                if (reward && detour >= 0 && c.Row >= position.Row && dist[i] <= c.Row - position.Row + detour) pickups.Add(i);

                if (tile.IsLever && unvisited && (lever < 0 || dist[i] < dist[lever])) lever = i;

                if (frontier < 0 || Better(c, dist[i], at(frontier), dist[frontier])) frontier = i;
            }

            foreach (var pickup in pickups.OrderBy(i => dist[i]))
            {
                if (mandatory >= 0 && at(pickup).Row >= at(mandatory).Row) continue; // сначала обязательное
                if (LeadsOnward(pickup, prev, at)) return pickup;
            }

            if (mandatory >= 0) return mandatory;
            if (frontier >= 0 && at(frontier).Row > position.Row) return frontier;
            return lever; // вперёд пути нет — может, Ворота открывает рычаг
        }

        /// <summary>
        /// Награда не в тупике: от неё можно уйти на два ряда вперёд, не возвращаясь по собственному
        /// следу (он распадается в Лаву).
        /// </summary>
        private bool LeadsOnward(int pickup, int[] prev, Func<int, GridCoordinate> at)
        {
            var closed = new HashSet<GridCoordinate> { Position };
            for (var i = pickup; i >= 0; i = prev[i]) closed.Add(at(i));
            var from = at(pickup);
            var queue = new Queue<GridCoordinate>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.Row >= from.Row + 2 || current.Row >= Position.Row + VisibleRows) return true;
                foreach (var (dr, dc) in Directions)
                {
                    var next = new GridCoordinate(current.Row + dr, current.Column + dc);
                    if (closed.Contains(next) || EnterCost(current, next) < 0f) continue;
                    closed.Add(next);
                    queue.Enqueue(next);
                }
            }

            return false;
        }

        private static bool Better(GridCoordinate a, float distA, GridCoordinate b, float distB)
        {
            if (a.Row != b.Row) return a.Row > b.Row;
            if (Math.Abs(distA - distB) > 0.001f) return distA < distB;
            return Math.Abs(a.Column - PlaytestRun.Width / 2) < Math.Abs(b.Column - PlaytestRun.Width / 2);
        }

        /// <summary>
        /// Куда сойти с триггера: ход с наименьшей вероятностью смерти. Сигнатура не сообщает тип (§7.4),
        /// поэтому при неизвестном типе берётся частота типов в шаблонах игры (<see cref="TrapPrior"/>); при известном
        /// (Тотем Маанг, Руна «Распутье», Идол «Время» или опыт политики) — учитывается только он.
        /// Камень бьёт в плиту прямо впереди; Стрела летит по ряду самого триггера; Давилка и Стены
        /// закрывают ряд впереди — в нём нельзя стоять в колонке, что закрывается первой, и из него
        /// нужен выход вперёд, а не пройти его сразу — значит остаться за стеной.
        /// </summary>
        private GridCoordinate? ChooseTriggerExit(GridCoordinate position)
        {
            _run.Grid.TryGetTile(position, out var trigger);
            var known = trigger.IsTrapTypeRevealed || _config.KnowsTrapTypes;
            var isRock = trigger.IsFallingRockTrigger;
            var isArrows = trigger.ArrowWaveTargetRow.HasValue;
            var mode = trigger.MovingWallTriggerMode;
            var prior = _run.ActiveSanctuary != null ? TrapPrior.Sanctuary : TrapPrior.Tunnel;
            double pRock = prior.Rock, pArrows = prior.Arrows, pBoth = prior.Crusher, pLeft = prior.WallLeft, pRight = prior.WallRight;
            if (known)
            {
                pRock = isRock ? 1 : 0;
                pArrows = isArrows ? 1 : 0;
                pBoth = mode == MovingWallMode.Both ? 1 : 0;
                pLeft = mode == MovingWallMode.FromLeft ? 1 : 0;
                pRight = mode == MovingWallMode.FromRight ? 1 : 0;
            }

            const int last = PlaytestRun.Width - 1;
            GridCoordinate? best = null;
            var bestRisk = double.MaxValue;
            foreach (var (dr, dc) in Directions)
            {
                var to = new GridCoordinate(position.Row + dr, position.Column + dc);
                var cost = EnterCost(position, to);
                if (cost < 0f) continue;

                double risk;
                if (dr > 0)
                {
                    var trapped = !HasForwardExit(to) ? 0.8 : 0.0;
                    risk = (dc == 0 ? pRock : 0.0)
                           + pBoth * (to.Column == 0 || to.Column == last ? 1.0 : trapped)
                           + pLeft * (to.Column == 0 ? 1.0 : trapped)
                           + pRight * (to.Column == last ? 1.0 : trapped);
                }
                else
                {
                    risk = (dr == 0 ? pArrows * 0.5 : 0.0) + (pBoth + pLeft + pRight) * 0.9; // ряд впереди закроется раньше, чем его пройти
                    risk += dr == 0 ? 0.01 : 0.02;
                }

                if (cost > 1f) risk += 0.3; // другой триггер или распадающаяся плита
                if (risk < bestRisk)
                {
                    bestRisk = risk;
                    best = to;
                }
            }

            return best;
        }

        private bool HasForwardExit(GridCoordinate from)
        {
            for (var dc = -1; dc <= 1; dc++)
                if (EnterCost(from, new GridCoordinate(from.Row + 1, from.Column + dc)) >= 0f) return true;
            return false;
        }

        private bool ShouldEvacuate()
        {
            _run.Provider.TryGetAltarSpot(Position.Row, out var yarus, out var ordinal);
            if (ordinal == 3 && yarus >= _profile.LastOpenYarus) return true; // дверь закрыта (§6.2) — только эвакуация
            var carried = _run.Carried.Count;
            switch (_config.Kind)
            {
                case BotPolicyKind.Cautious:
                    return carried >= 1;
                case BotPolicyKind.Greedy:
                    return carried >= 3;
                default:
                    return RiskOutweighsGain(ordinal == 3 ? yarus + 1 : Math.Max(1, yarus), ordinal);
            }
        }

        // Skilled: уходит, когда ожидаемая потеря несомого больше ожидаемой добычи следующего Святилища.
        private bool RiskOutweighsGain(int nextShrineYarus, int ordinal)
        {
            var yarus = Math.Min(nextShrineYarus, GodTable.YarusCount);
            var remainingShare = ordinal == 3 ? 1.0 : ordinal == 2 ? 0.6 : 0.8; // часть Яруса уже пройдена
            var risk = Math.Min(0.95, BotEvacuationModel.DeathRiskByYarus[yarus] * remainingShare *
                                      BotEvacuationModel.RiskFactorByFateRollsUsed[Math.Min(3, _run.FateRollsUsed)]);
            var carried = ManaConversion.CoinsFor(_run.Mana.Total, EvacuationSystem.ManaToCoinsRate) +
                          _run.Carried.Items.Sum(s => BotEvacuationModel.SarcophagusValueByRank[(int)s.Rank]);
            var gain = BotEvacuationModel.SarcophagusValueByRank[(int)GodTable.ForYarus(yarus).Rank] + BotEvacuationModel.ManaCoinsPerYarus;
            return risk * carried > (1.0 - risk) * gain;
        }

        // Берёт первую карточку, под которую есть свободный слот; если слотов нет — заменяет первую Реликвию.
        private static void PickRelic(AltarPickSession session)
        {
            foreach (var option in session.Options)
            {
                session.Pick(option.Id);
                if (session.State == AltarPickState.Done) return;
                session.BackToCards();
            }

            session.Pick(session.Options[0].Id);
            session.Replace(session.ReplaceCandidates[0].Id);
        }

        private void UseAbilities()
        {
            var position = Position;
            var sanctuary = _run.ActiveSanctuary;
            var forward = new GridCoordinate(position.Row + 1, position.Column);
            var wallAhead = _run.Grid.TryGetTile(forward, out var forwardTile) && forwardTile.IsBlocked;
            var onTrigger = IsLiveTrigger(position);

            if (sanctuary != null)
            {
                _run.UseRune(RelicEffectState.RasputeRune);
                _run.UseRune(RelicEffectState.MorokRune);
                if (position.Row - sanctuary.LastCollapsedRow <= 1) _run.UseRune(RelicEffectState.NightRune);
                if (wallAhead) _run.UseRune(RelicEffectState.PerunStrikeRune);
            }

            if (!_run.IdolCharged) return;
            var twoAhead = new GridCoordinate(position.Row + 2, position.Column);
            switch (_run.ActiveIdolId)
            {
                case IdolAbilities.Shield:
                    _run.UseIdol();
                    break;
                case IdolAbilities.Time:
                    if (sanctuary != null && onTrigger) _run.UseIdol();
                    break;
                case IdolAbilities.Breach:
                    if (wallAhead) _run.UseIdol(forward);
                    break;
                case IdolAbilities.Rush:
                case IdolAbilities.Passage:
                    if (sanctuary != null && onTrigger) _run.UseIdol(twoAhead); // перепрыгнуть ряд удара
                    break;
            }
        }
    }
}
