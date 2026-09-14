using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Планировщик отложенных угроз, тикаемый РЕАЛЬНЫМ ВРЕМЕНЕМ, не ходами
    /// (issue #254, docs/wiki/traps.md, раздел «Разделение тактов/реального
    /// времени») — единственный планировщик отложенных угроз в проекте,
    /// используют все пять систем ловушек (<see cref="ArrowWaveTrapSystem"/>/
    /// <see cref="BombTrapSystem"/>/<see cref="BladeTactTrapSystem"/>/
    /// <see cref="FallingRockTrapSystem"/>/<see cref="LavaWaveTrapSystem"/>).
    /// Планировщик ничего не знает о <see cref="GridTraceTrail"/> — вызывающая
    /// сторона сама решает, что значит «плита созрела» (через
    /// <see cref="TileDue"/>); единица времени — <c>deltaSeconds</c>, а не
    /// «шаг игрока».
    ///
    /// <b>Была сестра-близнец TurnBasedThreatScheduler, тикаемая ходами —
    /// удалена целиком (владелец, 2026-09-14, issue #254, второй раунд):
    /// «ловушки в такт шагам это ошибка в ТЗ, никаких ловушек в такт быть не
    /// должно, только тайминги».</b> Первый раунд этой задачи разделял ДВЕ
    /// вещи между двумя классами планировщиков: задержку до активации
    /// (Бомба/Падающий камень, оставалась на тактах ходов) и движение уже
    /// активной волны (Стрела/Лезвия/Лава, реальное время). Причина
    /// разделения была в том, что первые два казались одномоментным
    /// событием, не "волной, которую нужно догонять" — но живой плейтест
    /// показал тот же класс бага в другой форме: если игрок наводится на
    /// раскрытый триггер, отпускает и просто стоит на месте, разглядывая —
    /// тактовый планировщик вообще не продвигался, потому что тикался
    /// только на ходы игрока. Разделения на два класса больше нет — все
    /// пять типов ловушек одинаково не должны зависеть от того, шагает ли
    /// игрок.
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
