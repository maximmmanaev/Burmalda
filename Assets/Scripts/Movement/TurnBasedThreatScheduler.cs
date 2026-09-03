using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Планировщик отложенных угроз, тикаемый ХОДАМИ, не реальным временем
    /// (issue #212, docs/wiki/traps.md). Владелец задал пять типов ловушек
    /// (issues #213–#217), тайминги которых даны в ходах ("через 1 ход",
    /// "через 2 хода") и которые оперируют ПАТТЕРНОМ/ПЛОЩАДЬЮ из нескольких
    /// плит с РАЗНЫМИ моментами активации каждой (стрела по ряду, такт
    /// лезвий), а не одной плитой-целью на фиксированном временном окне —
    /// <see cref="TimedTrapSystem"/> (ближайший прецедент по форме: список
    /// ожидающих угроз, явный внешний <c>Tick</c>) для этого не подходит без
    /// переписывания (тикается <c>deltaSeconds</c>, одна плита-цель), и по
    /// прямому требованию задачи НЕ переписывается — этот класс существует
    /// РЯДОМ с ним, не вместо него.
    ///
    /// <b>Специально НЕ подписывается на <see cref="GridTraceTrail.PositionChanged"/>
    /// сама.</b> Конкретный тип ловушки (issues #213–#217) сам решает, что
    /// считать «ходом» (на практике — <see cref="GridTraceTrail.PositionChanged"/>,
    /// как и у <see cref="TimedTrapSystem"/>/<see cref="ExplosiveTrapArmingSystem"/>),
    /// и вызывает <see cref="Tick"/> явно — тот же приём, что
    /// <c>TimedTrapSystem.Tick(deltaSeconds)</c>, только единица хода, а не
    /// секунды. Если бы планировщик подписывался на трейл сам, а
    /// конкретный тип ловушки регистрировал новую отложенную активацию из
    /// СВОЕГО ОБРАБОТЧИКА того же самого события (внутри того же самого
    /// хода — плита-триггер под ногами прямо сейчас), порядок подписки
    /// решал бы, тикнет ли только что зарегистрированная активация на этом
    /// же ходу или только со следующего — тот же класс гонки, что уже ловили
    /// на двух генераторах тоннеля (задача «двойные флаги на плитах»).
    /// Явный внешний <see cref="Tick"/> убирает эту гонку по конструкции —
    /// планировщик вообще не знает о <see cref="GridTraceTrail"/>.
    ///
    /// Один экземпляр — одна независимая очередь отложенных активаций,
    /// переиспользуемая любым числом одновременно зарегистрированных плит
    /// (см. <see cref="ScheduleActivation"/>) с разными моментами активации
    /// каждой — ровно то, что нужно волне Стрелы или такту Лезвий (см.
    /// docs/wiki/traps.md). Конкретный тип ловушки создаёт свой собственный
    /// экземпляр (тот же принцип, что отдельные экземпляры
    /// <see cref="TimedTrapSystem"/>/<see cref="ExplosiveTrapArmingSystem"/>
    /// на забег) — этот класс не знает и не должен знать, ЧТО означает
    /// «плита созрела» (смертельна? непроходима? и то, и другое сразу?) —
    /// решает вызывающая сторона через <see cref="TileDue"/>.
    /// </summary>
    public sealed class TurnBasedThreatScheduler
    {
        private readonly struct PendingActivation
        {
            public PendingActivation(GridCoordinate coordinate, int ticksRemaining)
            {
                Coordinate = coordinate;
                TicksRemaining = ticksRemaining;
            }

            public GridCoordinate Coordinate { get; }
            public int TicksRemaining { get; }
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
        /// через <paramref name="ticksFromNow"/> ходов (см. <see cref="Tick"/>).
        /// Несколько активаций на одну и ту же плиту (например, «такт» Лезвий,
        /// где столбец опасен несколько раз за цикл) регистрируются отдельными
        /// вызовами — планировщик не схлопывает и не дедуплицирует их.
        /// </summary>
        public void ScheduleActivation(GridCoordinate coordinate, int ticksFromNow)
        {
            if (ticksFromNow <= 0)
                throw new ArgumentOutOfRangeException(nameof(ticksFromNow), ticksFromNow, "Отложенная активация должна наступать хотя бы через 1 ход — активация без задержки не через этот планировщик (см. её doc-комментарий).");

            _pending.Add(new PendingActivation(coordinate, ticksFromNow));
        }

        /// <summary>
        /// Продвигает все отложенные активации на 1 ход. Вызывать явно на
        /// каждый ход игрока (см. doc-комментарий класса — планировщик сам
        /// не подписывается ни на что). Активации, чей срок наступил,
        /// поднимают <see cref="TileDue"/> и удаляются из очереди.
        /// </summary>
        public void Tick()
        {
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                var pending = _pending[i];
                var remaining = pending.TicksRemaining - 1;
                if (remaining <= 0)
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
