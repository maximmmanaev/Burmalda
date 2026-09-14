using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Ловушка «Бомба» (docs/wiki/traps.md, issue #214) — раньше сосуществовала
    /// с ближайшим по духу, но не совпадающим ни по одному из трёх
    /// параметров прецедентом (<c>Movement.ExplosiveTrapArmingSystem</c>/
    /// <c>Core.LethalTrapType.Explosion</c>: активация мгновенная, не через
    /// 2 хода; одна плита, не площадь 3×3; результат постоянный, не возврат
    /// в обычное состояние) — та старая механика удалена целиком владельцем
    /// 2026-09-05 («оставить только пять новых ловушек»). C#-идентификатор
    /// этой ловушки — <see cref="LethalTrapType.BombBlast"/>.
    ///
    /// <b>Третий раунд — реальный баг с устройства (issue #260, 2026-09-14):
    /// «Бомба взрывается мгновенно и показывает текстуру Стрелы».</b>
    /// Разбор показал ДВЕ отдельные, независимые причины одного и того же
    /// симптома:
    /// <list type="bullet">
    /// <item>«Мгновенно» — <see cref="DelaySeconds"/> (второй раунд, issue
    /// #254) уже существовал и уже был на <see cref="RealTimeThreatScheduler"/>,
    /// но было ровно одно фиксированное число (0.6с) без какой-либо связи с
    /// прогрессом забега — на глаз воспринималось как «слишком быстро,
    /// почти без задержки». Эта задача заменяет фиксированное число кривой,
    /// зависящей от <see cref="Progression.RunDepthTier"/> — см.
    /// <see cref="ComputeDelaySeconds"/>.</item>
    /// <item>«Текстура Стрелы» — НЕ баг именно Бомбы: <c>DebugVisuals.
    /// TileArtKindResolver</c>/<c>TileDebugColor</c> объединяли ВСЕ ЧЕТЫРЕ
    /// рантайм-типа ловушек (ArrowWave/BombBlast/BladeTact/LavaWave) в ОДНУ
    /// общую категорию <c>TileArtKind.TimedTrapActive</c> — единственная
    /// текстура этой категории (<c>tile-timed-trap-active.png</c>) визуально
    /// изображает наконечник стрелы (тематически подобрана под Стрелу,
    /// первую из пяти реализованных ловушек). Бомба никогда не имела
    /// СОБСТВЕННОЙ текстуры — не «показывает чужую по ошибке», а «никогда и
    /// не должна была делить эту с остальными тремя». Общий корень
    /// подтверждён — ArrowWave/BladeTact/LavaWave по-прежнему используют
    /// <c>TimedTrapActive</c> заслуженно (для них это не баг, задача про
    /// Бомбу), а отдельная задача про текстуру статичной Лавы (issue #258 —
    /// другая Лава, <see cref="LethalTrapType.Lava"/>, не
    /// <see cref="LethalTrapType.LavaWave"/>) ЭТОГО корня не касается вовсе
    /// (Лава туда никогда не попадала — <c>TileArtKindResolver</c> отдаёт ей
    /// отдельную ветку <c>TileArtKind.Lava</c> ещё до общей проверки). Если
    /// будущая задача даст волновой Лаве (<see cref="LethalTrapType.LavaWave"/>)
    /// собственную текстуру — правильное место править то же самое, что
    /// правит эта задача для Бомбы (<c>TileArtKindResolver</c>/<c>TileDebugColor</c>),
    /// а не заново искать причину.</item>
    /// </list>
    ///
    /// <b>Реальное время, не ходы (владелец, 2026-09-14, issue #254 —
    /// исправлено после первого раунда: «ловушки в такт шагам это ошибка,
    /// никаких ловушек в такт быть не должно, только тайминги»).</b> Раньше
    /// (и в первом варианте issue #254) эта система оставалась на тактах
    /// ходов, потому что задержка до одномоментного взрыва казалась
    /// принципиально другой сущностью, чем движение волны — но живой
    /// плейтест показал тот же класс проблемы, что и у волн: если игрок
    /// наводится на раскрытый триггер, отпускает — и просто стоит,
    /// разглядывая — отсчёт до взрыва не идёт вообще, потому что тикался
    /// только на ЕГО ходы. Построена на <see cref="RealTimeThreatScheduler"/> —
    /// отсчёт идёт реальными секундами независимо от того, движется игрок
    /// или нет.
    ///
    /// <b>Задержка зависит от Яруса Глубины (issue #260, требование
    /// владельца): «чем дальше прошёл игрок, тем короче задержка».</b>
    /// <see cref="ComputeDelaySeconds"/> — простая монотонно убывающая
    /// кривая (линейная, с нижним порогом): <see cref="BaseDelaySeconds"/>
    /// на Ярусе 0, минус <see cref="DelayReductionPerTier"/> за каждый
    /// следующий Ярус, не ниже <see cref="MinDelaySeconds"/> — задержка
    /// никогда не становится тривиально короткой или отрицательной,
    /// сколько бы Ярусов ни прошло. Владелец прямо попросил не заниматься
    /// балансом конкретных цифр (issues #31-34/#94) — форма кривой
    /// (убывает, не растёт и не скачет) это требование, сами три числа —
    /// предположение агента, mutable static, три новых слайдера в
    /// <see cref="DebugVisuals.TrapDensityDebugPanel"/>. Источник номера
    /// Яруса передаётся конструктору как <see cref="Func{TResult}"/> —
    /// <c>Movement</c> не ссылается на сборку <c>Progression</c>
    /// (см. <c>Movement.TrapSystemsController.CurrentTierProvider</c>,
    /// куда его подключает композиционный корень); null по умолчанию — Ярус
    /// 0, безопасно для тестов и для любого вызывающего кода, которому
    /// прогресс безразличен.
    ///
    /// Проход трейла через плиту-триггер (<see cref="Tile.IsBombTrigger"/>)
    /// запускает отсчёт и сразу поднимает предупреждение на всю площадь
    /// (<see cref="Tile.BeginBombWarning"/>, issue #260 — «плитки квадрата
    /// 3×3 должны мигать, пока идёт отсчёт») — визуал мигания
    /// (пульсация тона поверх текстуры) — забота рендер-слоя
    /// (<c>DebugVisuals.TunnelDebugVisual</c>), эта система только выставляет
    /// булев флаг, который рендер-слой читает каждый кадр.
    ///
    /// <b>Момент взрыва (issue #260, полностью переработан):</b> через
    /// <see cref="ComputeDelaySeconds"/> секунд площадь <see cref="RadiusTiles"/>
    /// вокруг триггера (по умолчанию радиус 1 — квадрат 3×3, восемь соседей
    /// плюс сама плита-триггер, обрезанный по границе сетки) схлопывается
    /// ОДНОМОМЕНТНО — одним циклом внутри одного вызова <see cref="Tick"/>,
    /// не по очереди, как у Стрелы (<see cref="ArrowWaveTrapSystem"/>). Все
    /// девять (или меньше, у края) плит получают <see cref="Tile.MarkBombCollapsed"/>
    /// (владелец: «текстура дыры + анимация проваливания» — визуал
    /// одинаков для всех плит площади, независимо от исхода ниже) и
    /// снимают предупреждение (<see cref="Tile.EndBombWarning"/>). Дальше
    /// РОВНО ОДНА из двух веток на плиту, по тому же принципу, что уже
    /// действует у <see cref="FallingRockTrapSystem"/> («камень падает на
    /// плиту-триггер: игрок на ней — гибнет, ушёл — плита блокируется
    /// навсегда»), только применённая к площади 3×3, а не к одной плите:
    /// <list type="bullet">
    /// <item>плита == <see cref="GridTraceTrail.CurrentPosition"/> (игрок
    /// стоит на ней прямо сейчас, не совершая новый ход) — становится
    /// <see cref="LethalTrapType.BombBlast"/> (<see cref="Tile.TransitionToLethalTrap"/>),
    /// затем немедленно <see cref="GridTraceTrail.CheckCurrentPositionForLethalTrap"/>
    /// поднимает то же <see cref="GridTraceTrail.LethalTrapTriggered"/>, что и
    /// обычный шаг на ловушку — <see cref="RunLifecycle.RunState.ResolveHazard"/>
    /// (d20, «Испытание Шахты», PRD v9 §9) решает исход СТАНДАРТНЫМ путём,
    /// не отдельной логикой (владелец, критерий приёмки issue #260). Плита
    /// НЕ становится <see cref="Tile.IsBlocked"/> в этой ветке — при исходе
    /// Fortune игрок продолжает стоять там же (симметрично тому, как Лава,
    /// issue #258, остаётся проходимой после успешного исхода), при
    /// Knockback телепорт уводит его прежде, чем плита успевает получить
    /// какую-либо дальнейшую роль.</item>
    /// <item>любая другая плита площади — становится непроходимой навсегда
    /// (<see cref="Tile.TransitionToBlocked"/>, тот же путь, что и
    /// «Падающий камень») — <b>меняет более раннее решение issue #214</b>
    /// («плиты не разрушаются, дыры — отдельное решение, не реализовывать
    /// без явного запроса») — владелец запросил это явно этой задачей,
    /// значит «отдельное решение» наступило. <see cref="Tile.ClearLethalTrap"/>
    /// вызывается ПЕРЕД <see cref="Tile.TransitionToBlocked"/> — площадь не
    /// должна нести одновременно <see cref="LethalTrapType.BombBlast"/> и
    /// <see cref="Tile.IsBlocked"/>: без явной очистки будущий шаг игрока на
    /// такую плиту попал бы на более раннюю ветку <see cref="GridTraceTrail.TryAdvanceTo"/>
    /// (проверяет <see cref="Tile.LethalTrap"/> раньше <see cref="Tile.IsBlocked"/>)
    /// и заново бросал бы d20 на давно остывшую воронку — постоянная дыра
    /// обязана вести себя как обычная стена (без риска), не как ловушка,
    /// которая к тому же ещё и блокирует.</item>
    /// </list>
    ///
    /// Одноразовая ловушка на триггер — повторный проход не запускает
    /// вторую параллельную бомбу (тот же приём, что <see cref="ArrowWaveTrapSystem"/>).
    /// Несколько одновременных бомб (разные триггеры) поддерживаются
    /// независимо друг от друга.
    /// </summary>
    public sealed class BombTrapSystem : IDisposable
    {
        // Ярус 0 — "несколько секунд", прямое требование владельца (issue
        // #260: "не мгновенно"). Балансное число, mutable static, не const —
        // дебаг-панель (критерий приёмки), предположение агента, не решение
        // владельца (issues #31-34/#94 про баланс конкретно не про это).
        public static float BaseDelaySeconds = 3f;

        // Снижение задержки за каждый пройденный Ярус — форма кривой
        // (убывает линейно) отвечает требованию "монотонно уменьшается, не
        // растёт и не скачет", само число — предположение агента.
        public static float DelayReductionPerTier = 0.3f;

        // Нижний порог — задержка никогда не становится тривиально короткой
        // или отрицательной на очень глубоких Ярусах. "Несколько секунд"
        // (критерий приёмки) обязано оставаться верным на любом Ярусе, не
        // только на нулевом.
        public static float MinDelaySeconds = 1f;

        // "Радиус 1 (квадрат 3×3)" — прямое требование владельца, issue
        // #214. Не единица времени — не меняется этой задачей.
        public static int RadiusTiles = 1;

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly RealTimeThreatScheduler _scheduler;
        private readonly Func<int> _currentTierProvider;
        private readonly HashSet<GridCoordinate> _firedTriggers = new HashSet<GridCoordinate>();
        private bool _disposed;

        public BombTrapSystem(TunnelGrid grid, GridTraceTrail trail, RealTimeThreatScheduler scheduler, Func<int> currentTierProvider = null)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _currentTierProvider = currentTierProvider ?? (() => 0);
            _trail.PositionChanged += OnPositionChanged;
            _scheduler.TileDue += OnTileDue;
        }

        /// <summary>
        /// Задержка до взрыва на заданном Ярусе Глубины (issue #260) — чистая
        /// функция, тестируется без планировщика/сетки. Линейная, с нижним
        /// порогом — см. doc-комментарий класса и <see cref="MinDelaySeconds"/>.
        /// Отрицательный/нулевой <paramref name="tier"/> трактуется как Ярус 0.
        /// </summary>
        public static float ComputeDelaySeconds(int tier)
        {
            var effectiveTier = tier > 0 ? tier : 0;
            var reduced = BaseDelaySeconds - effectiveTier * DelayReductionPerTier;
            return reduced > MinDelaySeconds ? reduced : MinDelaySeconds;
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
            if (!tile.IsBombTrigger) return;
            if (!_firedTriggers.Add(coordinate)) return; // одноразовый триггер

            // "Плитки квадрата 3×3 должны мигать, пока идёт отсчёт" —
            // вся площадь предупреждает сразу с момента активации, не
            // только сама плита-триггер (issue #260).
            foreach (var target in ComputeBlastArea(coordinate))
                _grid.GetOrCreateTile(target).BeginBombWarning();

            _scheduler.ScheduleActivation(coordinate, ComputeDelaySeconds(_currentTierProvider()));
        }

        private void OnTileDue(GridCoordinate coordinate)
        {
            // "Одномоментно, не по очереди" (критерий приёмки issue #214,
            // не отменено #260) — визуал дыры/снятие предупреждения на ВСЮ
            // площадь в рамках одного вызова, ни одна плита не ждёт
            // следующего Tick.
            foreach (var target in ComputeBlastArea(coordinate))
            {
                var tile = _grid.GetOrCreateTile(target);
                tile.EndBombWarning();
                tile.MarkBombCollapsed();
            }

            var playerCoordinate = _trail.CurrentPosition;
            var playerCaughtInBlast = false;
            foreach (var target in ComputeBlastArea(coordinate))
            {
                if (target != playerCoordinate) continue;
                _grid.GetOrCreateTile(target).TransitionToLethalTrap(LethalTrapType.BombBlast);
                playerCaughtInBlast = true;
            }
            // Игрок мог уже стоять на плите площади, не совершая новый ход —
            // TryAdvanceTo здесь ни при чём, см. doc-комментарий
            // GridTraceTrail.CheckCurrentPositionForLethalTrap.
            if (playerCaughtInBlast) _trail.CheckCurrentPositionForLethalTrap();

            foreach (var target in ComputeBlastArea(coordinate))
            {
                if (target == playerCoordinate) continue; // разобрано выше — d20 вместо постоянной дыры
                var tile = _grid.GetOrCreateTile(target);
                tile.ClearLethalTrap(); // см. doc-комментарий класса — дыра не должна ОСТАВАТЬСЯ ещё и ловушкой
                tile.TransitionToBlocked();
            }
        }

        /// <summary>
        /// Все координаты в пределах <see cref="RadiusTiles"/> вокруг
        /// <paramref name="center"/> (включая саму <paramref name="center"/>),
        /// обрезанные по границе сетки (<see cref="TunnelGrid.Contains"/>) —
        /// у края тоннеля площадь взрыва меньше 9 тайлов (критерий приёмки).
        /// </summary>
        private IEnumerable<GridCoordinate> ComputeBlastArea(GridCoordinate center)
        {
            for (var rowOffset = -RadiusTiles; rowOffset <= RadiusTiles; rowOffset++)
            {
                for (var columnOffset = -RadiusTiles; columnOffset <= RadiusTiles; columnOffset++)
                {
                    var candidate = new GridCoordinate(center.Row + rowOffset, center.Column + columnOffset);
                    if (_grid.Contains(candidate)) yield return candidate;
                }
            }
        }
    }
}
