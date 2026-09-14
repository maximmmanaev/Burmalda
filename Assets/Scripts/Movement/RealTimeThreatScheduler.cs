using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Планировщик отложенных угроз, тикаемый РЕАЛЬНЫМ ВРЕМЕНЕМ, не ходами
    /// (issue #254, docs/wiki/traps.md, раздел «Разделение тактов/реального
    /// времени»). Сестра-близнец <see cref="TurnBasedThreatScheduler"/> —
    /// тот же интерфейс, тот же принцип «планировщик ничего не знает о
    /// <see cref="GridTraceTrail"/>, вызывающая сторона сама решает, что
    /// значит «плита созрела»» (см. её doc-комментарий, причины те же и
    /// здесь дословно повторяться не будут), но единица времени —
    /// <c>deltaSeconds</c>, а не «шаг игрока».
    ///
    /// <b>Зачем нужен отдельный класс, а не параметр у существующего</b>
    /// (владелец, 2026-09-14, issue #254 — «Волновые ловушки переходят на
    /// реальное время»): причина бага, из-за которого понадобилось это
    /// разделение — <see cref="Movement.TurnBasedTrapSystemsController"/>
    /// тикал ВСЕ пять систем ловушек ровно на каждый шаг игрока, поэтому
    /// волна (Стрела/Лезвия/Лава) физически не могла догнать игрока: она
    /// продвигалась ровно тогда же, когда шагал игрок, то есть была
    /// безопасна по построению, а не по игровому замыслу. Решение владельца
    /// — разделить ДВЕ РАЗНЫЕ вещи, которые раньше делил один класс: задержку
    /// до активации (остаётся на тактах ходов — <see cref="BombTrapSystem"/>/
    /// <see cref="FallingRockTrapSystem"/> продолжают использовать
    /// <see cref="TurnBasedThreatScheduler"/> без изменений) и движение уже
    /// активной волны (переходит на реальное время — <see cref="ArrowWaveTrapSystem"/>/
    /// <see cref="BladeTactTrapSystem"/>/<see cref="LavaWaveTrapSystem"/>
    /// используют этот класс). Один параметр "тикать секундами вместо ходов"
    /// у общего класса означал бы, что оба режима одновременно доступны
    /// каждой системе — ложный выбор, которого в игре не существует: тип
    /// ловушки раз и навсегда решает, какой класс планировщика ему нужен.
    /// </summary>
    public sealed class RealTimeThreatScheduler
    {
        private readonly struct PendingActivation
        {
            public PendingActivation(GridCoordinate coordinate, float secondsRemaining)
            {
                Coordinate = coordinate;
                SecondsRemaining = secondsRemaining;
            }

            public GridCoordinate Coordinate { get; }
            public float SecondsRemaining { get; }
        }

        private readonly List<PendingActivation> _pending = new List<PendingActivation>();

        /// <summary>
        /// Срабатывает, когда отложенная активация плиты достигает своего
        /// момента (см. <see cref="ScheduleActivation"/>/<see cref="Tick"/>).
        /// Планировщик не решает, что это означает для плиты — вызывающая
        /// сторона сама помечает её смертельной/непроходимой/чем угодно ещё.
        /// </summary>
        public event Action<GridCoordinate> TileDue;

        /// <summary>Число ещё не сработавших отложенных активаций — для тестов/дебаг-панели.</summary>
        public int PendingCount => _pending.Count;

        /// <summary>
        /// Регистрирует отложенную активацию плиты <paramref name="coordinate"/>
        /// через <paramref name="secondsFromNow"/> реальных секунд (см.
        /// <see cref="Tick"/>). Несколько активаций на одну и ту же плиту
        /// регистрируются отдельными вызовами — планировщик не схлопывает и
        /// не дедуплицирует их (тот же принцип, что <see cref="TurnBasedThreatScheduler.ScheduleActivation"/>).
        /// </summary>
        public void ScheduleActivation(GridCoordinate coordinate, float secondsFromNow)
        {
            if (secondsFromNow <= 0f)
                throw new ArgumentOutOfRangeException(nameof(secondsFromNow), secondsFromNow, "Отложенная активация должна наступать хотя бы через какое-то положительное время — активация без задержки не через этот планировщик (см. его doc-комментарий).");

            _pending.Add(new PendingActivation(coordinate, secondsFromNow));
        }

        /// <summary>
        /// Продвигает все отложенные активации на <paramref name="deltaSeconds"/>
        /// реальных секунд — вызывать явно из <c>Update()</c> владеющего
        /// MonoBehaviour (<c>Time.deltaTime</c>), НЕ из
        /// <see cref="GridTraceTrail.PositionChanged"/> (иначе это был бы
        /// снова тактовый планировщик под другим именем — см. doc-комментарий
        /// класса). Активации, чей срок наступил, поднимают
        /// <see cref="TileDue"/> и удаляются из очереди.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                var pending = _pending[i];
                var remaining = pending.SecondsRemaining - deltaSeconds;
                if (remaining <= 0f)
                {
                    _pending.RemoveAt(i);
                    TileDue?.Invoke(pending.Coordinate);
                }
                else
                {
                    _pending[i] = new PendingActivation(pending.Coordinate, remaining);
                }
            }
        }
    }
}
