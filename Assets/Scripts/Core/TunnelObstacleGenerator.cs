using System;
using System.Collections.Generic;

namespace Burmalda.Core
{
    /// <summary>
    /// Процедурная расстановка препятствий по сетке (PRD 4.2, issues #9/#10/
    /// #45): заблокированные плиты, статичные смертельные ловушки (яма/
    /// лава), триггеры динамических мгновенных ловушек (взрыв) и триггеры
    /// ловушек с таймингом (стрела/лезвие). Реагирует на
    /// <see cref="TunnelGrid.TileMaterialized"/> — как только плита впервые
    /// появляется в сетке, для неё один раз бросается случайное решение, по
    /// аналогии с genTileType() из legacy/burmolda_demo.html. Материализация
    /// плит заранее (до того как игрок успевает до них дойти) — не забота
    /// этого класса, см. <c>Burmalda.Movement.TunnelGridReveal</c>.
    ///
    /// Стартовый ряд (Row == 0) всегда безопасен — как и в прототипе
    /// (genTileType: "if(r===0)return 'safe'").
    ///
    /// Пороговые значения — изначально взяты буквально из прототипа там, где
    /// есть аналог (genTileType: rnd&lt;0.05 → 'block', rnd&lt;0.08 → 'pit',
    /// rnd&lt;0.11 → 'spike'), либо оценены агентом по аналогии там, где
    /// аналога нет (лава — новая в PRD v5; ловушки с таймингом — тоже новые
    /// в PRD v5, частота взята вдвое меньше взрыва). Все эти числа — предмет
    /// плейтеста баланса (Спринт 10, см. docs/rules/forbidden-actions.md) —
    /// не менять молча.
    ///
    /// <b>Задача «сделать тоннель играбельным», часть 2 (плейтест владельца,
    /// 2026-08-31):</b> прототип вёлся протяжкой пальца (десятки плит в
    /// минуту), продукт — дискретным шагом с примериванием (PRD v9 §4.1) —
    /// плит в минуту в разы меньше при той же вероятности на плиту, отсюда
    /// ощущение пустого тоннеля. Шесть <c>*Share</c>-полей ниже — доли
    /// вероятности на КАЖДЫЙ тип (ширина полосы, не кумулятивный порог) —
    /// раздельные доли вместо прямых порогов специально: иначе слайдер
    /// одного типа на дебаг-панели ломал бы диапазон всех типов после него.
    /// Теперь изменяемые (не <c>const</c>) и выведены на дебаг-панель
    /// <see cref="DebugVisuals.TrapDensityDebugPanel"/> ("GEN" в углу) —
    /// точную плотность подбирает владелец на устройстве, здесь только
    /// подняты стартовые значения (сохранена прежняя пропорция между
    /// типами) с ориентиром "хотя бы одна ситуация на 5-8 шагов" по задаче,
    /// не как окончательный баланс. Кумулятивные <c>*Threshold</c> ниже (те
    /// же имена, что использовали существующий код и тесты до этой задачи)
    /// — computed-свойства поверх долей, не самостоятельные поля: переход
    /// не потребовал переписывать ни <see cref="OnTileMaterialized"/>, ни
    /// один существующий тест.
    ///
    /// <b>УСТАРЕЛО (PRD v7 §21, issue #78)</b> — заменён сегментной
    /// генерацией (<c>Burmalda.Generation</c>). См.
    /// <c>Movement.TunnelObstacleController</c> для причины, по которой
    /// класс не удалён.
    ///
    /// <b>Сосуществование с сегментной генерацией (переходное состояние,
    /// docs/wiki/roadmap.md)</b>: с момента, когда <c>RunBootstrap</c> начал
    /// подключать <c>Generation.SegmentGenerationController</c> на ту же
    /// сцену, оба генератора работают одновременно — этот класс уступает
    /// целиком (см. <see cref="TunnelGrid.ClaimRow"/>) любому ряду, который
    /// уже заявлен сегментной генерацией, и продолжает случайно засеивать
    /// только ряды, до которых сегменты ещё не дошли. По конструкции, не по
    /// договорённости — см. проверку в <see cref="OnTileMaterialized"/>.
    /// </summary>
    public sealed class TunnelObstacleGenerator : IDisposable
    {
        // Доли вероятности каждого типа (ширина полосы, не кумулятивный
        // порог) — редактируются с TrapDensityDebugPanel в рантайме.
        // Прежние значения (до задачи "сделать тоннель играбельным"): 5% /
        // 1.5% / 1.5% / 3% / 1.5% / 1.5% (сумма 14%) — подняты примерно в
        // 1.5 раза с сохранением той же пропорции между типами, не выдуманы
        // заново (см. doc-комментарий класса).
        public static float BlockedShare = 0.08f;
        public static float PitShare = 0.02f;
        public static float LavaShare = 0.02f;
        public static float ExplosiveTriggerShare = 0.045f;
        public static float TimedTrapArrowShare = 0.02f;
        public static float TimedTrapBladeShare = 0.02f;

        // Кумулятивные пороги для роллов в OnTileMaterialized — вычисляются
        // из долей выше при каждом обращении (значения меняются на лету с
        // дебаг-панели, следующего забега ждать не нужно). Имена и порядок
        // — те же, что были у прежних const-полей.
        public static float BlockedThreshold => BlockedShare;
        public static float PitThreshold => BlockedThreshold + PitShare;
        public static float LavaThreshold => PitThreshold + LavaShare;
        public static float ExplosiveTriggerThreshold => LavaThreshold + ExplosiveTriggerShare;
        public static float TimedTrapArrowThreshold => ExplosiveTriggerThreshold + TimedTrapArrowShare;
        public static float TimedTrapBladeThreshold => TimedTrapArrowThreshold + TimedTrapBladeShare;

        private readonly TunnelGrid _grid;
        private readonly Func<float> _random01;
        // Координаты плит, зарезервированных как цель уже сгенерированного
        // триггера (взрыв #10 или тайминг #45) — им нельзя роллить
        // собственный тип, иначе цель могла бы оказаться заранее видимым
        // препятствием (block/pit/lava), что противоречит идее "не видна
        // заранее, пока не активирована". Одноразовая резервация — снимается
        // при первой материализации этой координаты (Remove ниже).
        private readonly HashSet<GridCoordinate> _reservedTrapTargets = new HashSet<GridCoordinate>();
        private bool _disposed;

        /// <param name="random01">
        /// Источник случайности — должен возвращать значение в [0, 1) при
        /// каждом вызове (как <c>UnityEngine.Random.value</c>/<c>System.Random.NextDouble()</c>).
        /// Внедряется явно вместо прямой зависимости от конкретного RNG —
        /// тесты подставляют детерминированную последовательность.
        /// </param>
        public TunnelObstacleGenerator(TunnelGrid grid, Func<float> random01)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _random01 = random01 ?? throw new ArgumentNullException(nameof(random01));
            _grid.TileMaterialized += OnTileMaterialized;
        }

        /// <summary>Отписывается от сетки. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _grid.TileMaterialized -= OnTileMaterialized;
            _disposed = true;
        }

        private void OnTileMaterialized(Tile tile)
        {
            if (tile.Coordinate.Row == 0) return; // стартовый ряд всегда безопасен

            // Ряд заявлен внешним генератором контента (см. doc-комментарий
            // TunnelGrid.ClaimRow — сейчас это Generation.SegmentRowProvider,
            // переходное состояние сосуществования двух генераторов на одной
            // сетке, docs/wiki/roadmap.md) — этот генератор его не трогает.
            // Проверка ПО КОНСТРУКЦИИ исключает двойную запись в одну плиту:
            // пока ряд не заявлен, только этот генератор пишет в его плиты;
            // как только заявлен — этот генератор не пишет уже никогда,
            // третьего состояния нет.
            if (_grid.IsRowClaimed(tile.Coordinate.Row)) return;

            // Плита зарезервирована как цель другого триггера — остаётся
            // обычной (безопасной на вид) до срабатывания триггера,
            // собственного броска не получает.
            if (_reservedTrapTargets.Remove(tile.Coordinate)) return;

            var roll = _random01();
            if (roll < BlockedThreshold) tile.MarkBlocked();
            else if (roll < PitThreshold) tile.MarkLethalTrap(LethalTrapType.Pit);
            else if (roll < LavaThreshold) tile.MarkLethalTrap(LethalTrapType.Lava);
            else if (roll < ExplosiveTriggerThreshold)
            {
                var target = NextRowTarget(tile.Coordinate);
                if (CanReserveTarget(target))
                {
                    tile.MarkExplosiveTrapTrigger(target);
                    _reservedTrapTargets.Add(target);
                }
            }
            else if (roll < TimedTrapArrowThreshold)
            {
                var target = NextRowTarget(tile.Coordinate);
                if (CanReserveTarget(target))
                {
                    tile.MarkTimedTrapTrigger(target, TimedTrapType.Arrow);
                    _reservedTrapTargets.Add(target);
                }
            }
            else if (roll < TimedTrapBladeThreshold)
            {
                var target = NextRowTarget(tile.Coordinate);
                if (CanReserveTarget(target))
                {
                    tile.MarkTimedTrapTrigger(target, TimedTrapType.Blade);
                    _reservedTrapTargets.Add(target);
                }
            }
            // Иначе — ни один порог не пройден, плита остаётся обычной
            // (тот же фолбэк, что и ниже у CanReserveTarget=false): не
            // выдуманное состояние, роллы уже сейчас могут не набрать до
            // TimedTrapBladeThreshold.
        }

        /// <summary>
        /// Найдено на реальном билде (2026-09-01, не гипотеза — воспроизвелось
        /// на Ярусе 3 живого забега, повторяющийся необработанный
        /// <c>InvalidOperationException</c> в логе устройства): цель триггера
        /// — ВСЕГДА <c>Row+1</c> (см. <see cref="NextRowTarget"/>). Если этот
        /// ряд уже заявлен сегментной генерацией
        /// (<c>Generation.SegmentRowProvider.ClaimRow</c> вызывается ДО
        /// материализации её плит, см. её doc-комментарий), содержимое этой
        /// плиты решает сегмент, не этот генератор — и оно может оказаться
        /// источником Маны/Ключа. Раньше это не проверялось: резервация
        /// (<see cref="_reservedTrapTargets"/>) защищает только от
        /// СОБСТВЕННОГО ролла этого генератора на той же плите, а не от
        /// содержимого, которое туда положит сегмент. Конфликт всплывал не
        /// на этапе генерации (тихо), а в рантайме — когда игрок реально
        /// доходил до триггера, <see cref="Movement.ExplosiveTrapArmingSystem"/>/
        /// <c>TimedTrapSystem</c> вызывали <c>Tile.MarkLethalTrap</c> на уже
        /// занятую источником плиту, и <c>Tile.GuardAgainstConflictingRole</c>
        /// (задача «двойные флаги на плитах») бросал исключение прямо во
        /// время хода игрока. Безопасный фолбэк — не ставить триггер вовсе
        /// (плита остаётся обычной), тот же принцип, что уже применяется к
        /// зарезервированным целям (см. класс-докстринг про "плита
        /// зарезервирована... остаётся обычной").
        /// </summary>
        private bool CanReserveTarget(GridCoordinate target) => !_grid.IsRowClaimed(target.Row);

        // Цель — плита сразу впереди по глубине тоннеля, тот же столбец
        // (issues #10/#45: "запускается с плиты-триггера" — ближайшая по
        // ходу движения плита, PRD не уточняет радиус/направление
        // детальнее). Координата известна сразу — сама плита-цель
        // материализуется позже (TunnelGridReveal идёт по рядам
        // последовательно), к этому моменту резервация уже ждёт её.
        private static GridCoordinate NextRowTarget(GridCoordinate trigger) =>
            new GridCoordinate(trigger.Row + 1, trigger.Column);
    }
}
