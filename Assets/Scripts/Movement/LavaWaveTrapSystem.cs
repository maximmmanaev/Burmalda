using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Ловушка «Лава» (docs/wiki/traps.md, issue #216) — НЕ статичный
    /// <see cref="LethalTrapType.Lava"/> (плита с генерации, не трогается).
    ///
    /// <b>Реальное время, не ходы (владелец, 2026-09-14, issue #254 —
    /// «Волновые ловушки переходят на реальное время», подтверждено явно:
    /// Лава переходит наравне со Стрелой/Лезвиями, несмотря на то что по
    /// замыслу агрессивнее отрезает путь назад — это ожидаемо).</b> Раньше
    /// система была построена на тактовом планировщике ходов (issue #212),
    /// тикаемом ровно на каждый шаг игрока — волна физически
    /// не могла догнать стоящего на месте игрока (тот же класс бага, что у
    /// Стрелы/Лезвий, см. doc-комментарий <see cref="ArrowWaveTrapSystem"/>).
    /// Теперь построена на <see cref="RealTimeThreatScheduler"/> — каждый шаг
    /// волны (и первый, и все последующие) тикается реальными секундами,
    /// один параметр <see cref="RowStepSeconds"/> на весь путь волны.
    ///
    /// Проход трейла через плиту-триггер (<see cref="Tile.IsLavaTrigger"/>,
    /// ещё не подключено ни к одному генератору сегментов — отдельная
    /// задача авторинга, здесь только механика) запускает волну «сразу»
    /// (владелец) — через <see cref="RowStepSeconds"/> секунд ряд самого
    /// триггера становится лавой, затем каждые следующие
    /// <see cref="RowStepSeconds"/> секунд ещё один ряд НАЗАД (в сторону
    /// убывания <see cref="GridCoordinate.Row"/>, откуда пришёл игрок —
    /// владелец: «волна идёт от ряда триггера назад»), пока не наберётся
    /// <see cref="MaxRows"/> рядов (владелец: «6 рядами, затем волна
    /// останавливается») или волна не дойдёт до начала тоннеля
    /// (<c>Row &lt; 0</c>). Весь ряд целиком (все столбцы) становится лавой
    /// одномоментно на своём шаге — не по одному тайлу, как Стрела.
    ///
    /// Лава НЕ возвращается в безопасное состояние (в отличие от Стрелы/
    /// Бомбы/Лезвий) — «отрезая путь назад» и есть весь смысл ловушки,
    /// необратимость намеренна, как у статичной <see cref="LethalTrapType.Lava"/>.
    ///
    /// <b>Инвариант волны (владелец, 2026-09-04, решение по конфликту с
    /// Воротами — docs/wiki/traps.md, раздел «4. Лава»): волна НИКОГДА не
    /// превращает в лаву ряд, на котором ПРЯМО СЕЙЧАС стоит игрок
    /// (<see cref="GridTraceTrail.CurrentPosition"/>), и ни один ряд
    /// ВПЕРЕДИ него — только позади.</b> Перед каждым шагом волны это
    /// проверяется явно (<see cref="OnTileDue"/>): если целевой ряд ещё не
    /// строго позади игрока, шаг ОТКЛАДЫВАЕТСЯ на 1 ход и проверяется снова
    /// (не пропускается навсегда, не режет бюджет <see cref="MaxRows"/>) —
    /// волна «ждёт», пока игрок не продвинется вперёд достаточно, чтобы
    /// целевой ряд оказался позади него. Это НЕ решение конфликта с Воротами
    /// само по себе (сочетание Лавы и Ворот в одном сегменте владелец прямо
    /// разрешил как намеренную цену жадного возврата, PRD v9 §4.3 —
    /// <see cref="Generation.SegmentReachabilityValidator"/> не трогается) —
    /// это отдельный, более фундаментальный инвариант самой волны: без него
    /// волна могла бы перекрыть ОСНОВНОЙ путь вперёд, что было бы настоящим
    /// тупиком без контригры, а не ценой возврата за наградой.
    ///
    /// <b>Второй инвариант (владелец, 2026-09-13, issue #249 — «лава после
    /// Комнаты Босса заливает ряд игрока»): волна никогда не превращает в
    /// лаву плиту Алтаря (<see cref="Tile.IsAltar"/>).</b> Алтарь — постоянный
    /// чекпоинт d20-исхода Knockback (<c>RunLifecycle.RunState.ResolveHazard</c>,
    /// <see cref="GridTraceTrail.TeleportTo"/> — телепорт цель НЕ проверяет,
    /// в отличие от обычного хода). Разбор наблюдения владельца показал, что
    /// первый инвариант (выше) сам по себе не нарушался, но
    /// <see cref="Tile.TransitionToLethalTrap"/> намеренно пишет поверх ЛЮБОЙ
    /// прежней роли (нужно для случаев вроде «взрыв уничтожает Ключ/Ману под
    /// собой») — включая Алтарь, если он однажды попадёт в диапазон волны.
    /// Раз посещённый Алтарь запоминается как «последний безопасный чекпоинт»
    /// на весь остаток забега; если волна его сжигает, а игрок ПОЗЖЕ ловит
    /// Knockback — его телепортирует прямо в лаву без проверки, снаружи
    /// неотличимо от «лава залила ряд игрока». Особенно вероятно рядом с
    /// Комнатой Босса — PRD-капстон (<c>Generation.SegmentRowProvider.EnsureCoveredThrough</c>)
    /// ставит два Алтаря прямо перед каждым входом, а «жадный возврат» внутри
    /// Комнаты заметно чаще задевает уже сгоревшие позади плиты. Пропускается
    /// ТОЛЬКО плита Алтаря — остальные столбцы того же ряда всё ещё честно
    /// становятся лавой, см. <see cref="OnTileDue"/>.
    ///
    /// Внешний <see cref="Tick"/> нужно вызывать явно, из <c>Update()</c>
    /// владеющего MonoBehaviour с <c>Time.deltaTime</c> — НЕ на каждый шаг
    /// игрока (issue #254). Обнаружение триггера (<see cref="OnPositionChanged"/>)
    /// по-прежнему висит на <see cref="GridTraceTrail.PositionChanged"/> —
    /// только продвижение уже активной волны переехало на реальное время.
    /// Одноразовая ловушка на триггер.
    /// </summary>
    public sealed class LavaWaveTrapSystem : IDisposable
    {
        // "6 рядами" — прямое требование владельца. Балансное число,
        // mutable static, не const — дебаг-панель (issue #216, критерий приёмки).
        // Количество рядов, не единица времени — не меняется задачей #254.
        public static int MaxRows = 6;

        // Единственный параметр скорости волны — и задержка до первого
        // ряда, и время между последующими рядами (issue #254, владелец:
        // "Скорость волны — параметр, настраиваемый в дебаг-панели").
        // Дефолт 0.3с — тот же ориентир, что уже используется в
        // docs/wiki/traps.md для перевода ходов в секунды, предположение
        // агента, не решение владельца. Mutable static — дебаг-панель.
        public static float RowStepSeconds = 0.3f;

        private sealed class ActiveWave
        {
            public GridCoordinate Trigger;
            public int TriggerRow;
            public int NextRowOffset; // 0 = ряд триггера, 1 = на 1 ряд назад, и т.д.
            public int RowsConverted;
        }

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly RealTimeThreatScheduler _scheduler;
        private readonly HashSet<GridCoordinate> _firedTriggers = new HashSet<GridCoordinate>();

        // Ключ — координата триггера (тот же приём, что у BombTrapSystem/
        // FallingRockTrapSystem): у волны нет естественного "следующего
        // столбца" на такт, как у ArrowWaveTrapSystem, зато есть один
        // источник — сам триггер.
        private readonly Dictionary<GridCoordinate, ActiveWave> _activeWaves = new Dictionary<GridCoordinate, ActiveWave>();

        private bool _disposed;

        public LavaWaveTrapSystem(TunnelGrid grid, GridTraceTrail trail, RealTimeThreatScheduler scheduler)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _trail.PositionChanged += OnPositionChanged;
            _scheduler.TileDue += OnTileDue;
        }

        /// <summary>Продвигает планировщик на <paramref name="deltaSeconds"/> реального времени — вызывать явно из Update() (см. doc-комментарий класса).</summary>
        public void Tick(float deltaSeconds) => _scheduler.Tick(deltaSeconds);

        /// <summary>Отписывается от трейла и планировщика. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.PositionChanged -= OnPositionChanged;
            _scheduler.TileDue -= OnTileDue;
            _disposed = true;
        }

        private void OnPositionChanged(GridCoordinate coordinate)
        {
            if (!_grid.TryGetTile(coordinate, out var tile)) return;
            if (!tile.IsLavaTrigger) return;
            if (!_firedTriggers.Add(coordinate)) return; // одноразовый триггер

            var wave = new ActiveWave
            {
                Trigger = coordinate,
                TriggerRow = coordinate.Row,
                NextRowOffset = 0,
                RowsConverted = 0
            };
            _activeWaves[coordinate] = wave;
            _scheduler.ScheduleActivation(coordinate, RowStepSeconds);
        }

        private void OnTileDue(GridCoordinate coordinate)
        {
            if (!_activeWaves.TryGetValue(coordinate, out var wave)) return;

            var candidateRow = wave.TriggerRow - wave.NextRowOffset;
            if (candidateRow < 0)
            {
                // Волна дошла до начала тоннеля раньше, чем набрала MaxRows — дальше рядов физически нет.
                _activeWaves.Remove(coordinate);
                return;
            }

            // Инвариант волны (см. doc-комментарий класса): целевой ряд
            // обязан быть строго позади игрока. Пока это не так — не
            // пропускаем шаг навсегда, а откладываем на RowStepSeconds и
            // проверяем снова, тот же NextRowOffset/RowsConverted. На
            // реальном времени (issue #254) это же и чинит старый баг: пока
            // игрок стоит на месте, волна продолжает ПЫТАТЬСЯ шагнуть каждые
            // RowStepSeconds (а не только когда игрок сам решит сделать ход),
            // и как только целевой ряд окажется позади него — конвертирует.
            if (candidateRow >= _trail.CurrentPosition.Row)
            {
                _scheduler.ScheduleActivation(coordinate, RowStepSeconds);
                return;
            }

            for (var column = 0; column < _grid.Width; column++)
            {
                var tile = _grid.GetOrCreateTile(new GridCoordinate(candidateRow, column));

                // issue #249 (владелец, плейтест после Комнаты Босса):
                // Алтарь — постоянный чекпоинт d20-Knockback (RunState.
                // ResolveHazard, GridTraceTrail.TeleportTo — телепорт цель не
                // проверяет). Tile.TransitionToLethalTrap намеренно пишет
                // поверх ЛЮБОЙ прежней роли (см. её doc-комментарий — нужно
                // для случаев вроде "бомба уничтожает Ключ/Ману под собой"),
                // но Алтарь не годится в жертву этого же правила: если волна
                // однажды сожжёт его, следующий же Knockback телепортирует
                // игрока прямо в лаву — цель телепорта не проверяется. Пропуск
                // ТОЛЬКО этой плиты, не всего ряда — соседние столбцы всё
                // ещё честно становятся лавой, инвариант «отрезает путь
                // назад» для остальной ширины ряда не ослаблен.
                if (tile.IsAltar) continue;

                tile.TransitionToLethalTrap(LethalTrapType.LavaWave);
            }

            wave.NextRowOffset++;
            wave.RowsConverted++;

            if (wave.RowsConverted >= MaxRows)
            {
                _activeWaves.Remove(coordinate);
                return;
            }

            _scheduler.ScheduleActivation(coordinate, RowStepSeconds);
        }
    }
}
