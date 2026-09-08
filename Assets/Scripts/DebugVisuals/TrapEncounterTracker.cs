using System.Collections.Generic;
using Burmalda.Core;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Задача «измерить, а не оценить» (владелец, 2026-09-08): тонкий driver
    /// (тот же паттерн, что <see cref="TrapRevealController"/>/<see cref="PickupFeedback"/>) —
    /// пишет в <see cref="TrapEncounterStats"/> на каждый шаг трейла.
    /// "Ловушка сработала" здесь — трейл впервые встал на плиту-триггер
    /// одной из пяти ловушек (issues #213-#217, тот же набор флагов, что
    /// <c>Movement.TrapRevealSystem.HasHiddenDanger</c>) — момент, когда
    /// игрок реально СТОЛКНУЛСЯ с механизмом ловушки, а не когда позже
    /// сработавшая волна/взрыв его поймала или нет (то, поймала ли, зависит
    /// от реакции игрока и не годится в знаменатель "насколько часто
    /// расставлены ловушки" — тогда осторожный игрок всегда получал бы
    /// нулевой счётчик, независимо от плотности). Каждая координата
    /// считается не больше одного раза за забег (см. <see cref="_countedTriggers"/>) —
    /// тем же принципом одноразового срабатывания, что уже у самих систем
    /// ловушек.
    /// </summary>
    public sealed class TrapEncounterTracker : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(TrapEncounterTracker));
            host.AddComponent<TrapEncounterTracker>();
            DontDestroyOnLoad(host);
        }

        private GridTraceInputController _input;
        private GridTraceTrail _wiredTrail;
        private readonly HashSet<GridCoordinate> _countedTriggers = new HashSet<GridCoordinate>();

        private void Update()
        {
            // Ленивый поиск/пересборка — тот же принцип, что у остальных
            // debug/game driver'ов проекта (порядок AfterSceneLoad между
            // разными классами не гарантирован, Trail пересоздаётся на
            // каждый забег — сверяем по ссылке, не только на null).
            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
            if (_input == null || _input.Trail == null) return;

            if (!ReferenceEquals(_wiredTrail, _input.Trail))
            {
                if (_wiredTrail != null) _wiredTrail.PositionChanged -= OnPositionChanged;
                _wiredTrail = _input.Trail;
                _wiredTrail.PositionChanged += OnPositionChanged;

                TrapEncounterStats.Reset();
                _countedTriggers.Clear();
                RecordPosition(_wiredTrail.CurrentPosition);
            }
        }

        private void OnPositionChanged(GridCoordinate coordinate) => RecordPosition(coordinate);

        private void RecordPosition(GridCoordinate coordinate)
        {
            TrapEncounterStats.RecordRow(coordinate.Row);

            if (_input.Grid == null || !_input.Grid.TryGetTile(coordinate, out var tile)) return;
            if (!IsTrapTrigger(tile)) return;
            if (!_countedTriggers.Add(coordinate)) return; // уже засчитан раньше в этом же забеге

            TrapEncounterStats.RecordTrapTrigger();
        }

        // Тот же набор флагов, что Movement.TrapRevealSystem.HasHiddenDanger/
        // DebugVisuals.TunnelDebugVisual.Tick — единственное определение
        // "это плита-триггер ловушки" на весь проект переиспользуется в
        // третьем месте, не заводится заново.
        private static bool IsTrapTrigger(Tile tile) =>
            tile.ArrowWaveTargetRow.HasValue || tile.IsBombTrigger || tile.BladeTactTargetRow.HasValue ||
            tile.IsFallingRockTrigger || tile.IsLavaTrigger;
    }
}
