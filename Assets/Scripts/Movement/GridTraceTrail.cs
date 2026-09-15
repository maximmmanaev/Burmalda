using System;
using System.Collections.Generic;
using Burmalda.Core;

namespace Burmalda.Movement
{
    /// <summary>
    /// Трейл grid-trace движения (PRD 4.1): игрок тянет палец по соседним
    /// плитам, продвигаясь вперёд по тоннелю. Ход валиден на плиту, соседнюю
    /// текущей позиции, если она ещё не пройдена трейлом, либо уже пройдена,
    /// но не разрушена распадом (<see cref="Tile.IsDestroyed"/>) — явный
    /// запрос владельца продукта, #61, отменяет прежний полный запрет повтора.
    /// Плита-препятствие (<see cref="Tile.IsBlocked"/>, PRD 4.2, #9) непроходима
    /// независимо от того, пройдена она трейлом или нет. Плита-ворота рычага (<see cref="Tile.IsGated"/>,
    /// PRD 4.2/21, #51) непроходима, пока не открыта связанным рычагом
    /// (<see cref="Tile.IsLeverGateOpen"/>) — по механике похожа на
    /// <see cref="Tile.IsBlocked"/>, но открывается навсегда, а не постоянна.
    /// </summary>
    public sealed class GridTraceTrail
    {
        private readonly TunnelGrid _grid;
        private readonly List<GridCoordinate> _path = new List<GridCoordinate>();
        private readonly HashSet<GridCoordinate> _visited = new HashSet<GridCoordinate>();
        private GridCoordinate _currentPosition;
        private bool _breachAvailable;

        public GridTraceTrail(TunnelGrid grid, GridCoordinate startCoordinate)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (!grid.Contains(startCoordinate))
                throw new ArgumentOutOfRangeException(nameof(startCoordinate), startCoordinate, "Стартовая координата вне сетки тоннеля.");

            grid.GetOrCreateTile(startCoordinate);
            _path.Add(startCoordinate);
            _visited.Add(startCoordinate);
            _currentPosition = startCoordinate;
        }

        /// <summary>
        /// Уникальные плиты трейла в порядке первого посещения — без дублей,
        /// даже если игрок повторно проходит уже пройденную плиту (#61).
        /// Для текущей позиции игрока см. <see cref="CurrentPosition"/> —
        /// она не обязательно совпадает с последним элементом этого списка.
        /// </summary>
        public IReadOnlyList<GridCoordinate> Path => _path;

        /// <summary>Текущая позиция игрока — двигается при любом успешном ходе, включая повтор (#61).</summary>
        public GridCoordinate CurrentPosition => _currentPosition;

        /// <summary>
        /// Была ли плита уже пройдена трейлом хотя бы раз — O(1)-версия
        /// проверки, для которой иначе пришлось бы линейно сканировать
        /// <see cref="Path"/>. Задача «тёплый набор плит»: нужна визуальному
        /// слою (<c>DebugVisuals.TunnelDebugVisual</c>), чтобы скрывать
        /// иконку источника Маны/Ключей после сбора — момент, когда плита
        /// становится "пройдена", у <see cref="TryAdvanceTo"/> синхронен с
        /// моментом начисления валюты (<c>Currencies.TrailTileCurrencySystem</c>
        /// реагирует на то же самое первое посещение через <see cref="Advanced"/>).
        /// </summary>
        public bool HasVisited(GridCoordinate coordinate) => _visited.Contains(coordinate);

        /// <summary>
        /// Срабатывает после продвижения трейла на плиту, которая ранее не
        /// была пройдена (см. <see cref="TryAdvanceTo"/>). Не срабатывает
        /// повторно при возврате на уже пройденную плиту (#61) — Decay и
        /// прочие подписчики реагируют только на по-настоящему новые плиты.
        /// </summary>
        public event Action<GridCoordinate> Advanced;

        /// <summary>
        /// Срабатывает при ЛЮБОМ успешном ходе — включая повторный шаг на
        /// уже пройденную плиту (#61), в отличие от <see cref="Advanced"/>.
        /// Нужен системам, которым важна именно текущая позиция игрока, а не
        /// факт первого посещения плиты (например, камере — она должна
        /// отступать назад при возврате по трейлу, а не только двигаться
        /// вперёд на новых плитках).
        /// </summary>
        public event Action<GridCoordinate> PositionChanged;

        /// <summary>
        /// Срабатывает, когда игрок наступает на смертельную ловушку
        /// (<see cref="Tile.LethalTrap"/>, PRD 4.2). Потребитель этого
        /// события — система, отвечающая за смерть/рестарт
        /// (Burmalda.RunLifecycle); сама GridTraceTrail ничего не знает о
        /// смерти/d20, только поднимает событие.
        ///
        /// <b>Две разные семантики по типу ловушки (issue #258 — «Лава
        /// должна быть проходимой, но летальной, а не физической стеной»):</b>
        /// для статичной <see cref="LethalTrapType.Lava"/> ход физически
        /// ЗАСЧИТЫВАЕТСЯ (см. <see cref="TryAdvanceTo"/>) — плита становится
        /// текущей позицией, событие поднимается ПОСЛЕ продвижения. Для
        /// остальных четырёх рантайм-типов (ArrowWave/BombBlast/BladeTact/
        /// LavaWave) сохранено прежнее поведение — ход НЕ засчитывается,
        /// плита-ловушка не становится текущей позицией, событие поднимается
        /// ВМЕСТО продвижения. Разница обоснована в doc-комментарии
        /// <see cref="CanAdvanceTo"/>.
        /// </summary>
        public event Action<GridCoordinate, LethalTrapType> LethalTrapTriggered;

        /// <summary>
        /// Ход на <paramref name="target"/> валиден, если плита в пределах
        /// сетки, соседняя текущей позиции, не является препятствием (#9), и
        /// при этом либо ещё не пройдена трейлом, либо пройдена, но не
        /// разрушена распадом (#61).
        ///
        /// <b>Смертельная ловушка (PRD 4.2) валидной целью НЕ считается — за
        /// одним исключением.</b> Статичная <see cref="LethalTrapType.Lava"/>
        /// (issue #258) физически проходима: она — риск, которым можно
        /// рискнуть (см. <see cref="RunLifecycle.RunState.ResolveHazard"/>,
        /// d20-испытание «Испытание Шахты», PRD v9 §9), не стена. Остальные
        /// четыре рантайм-типа ловушек (ArrowWave/BombBlast/BladeTact/
        /// LavaWave, Спринт 13a) остаются жёсткой стеной для этой проверки —
        /// владелец описывал их как «наступил на триггер — задача с
        /// таймером», не как «наступил на уже активную угрозу», для которой
        /// решение не задано; открытый вопрос, если владелец захочет
        /// перевести и их на ту же модель, что Лава, — отдельная задача.
        /// </summary>
        public bool CanAdvanceTo(GridCoordinate target)
        {
            if (!_grid.Contains(target)) return false;
            if (!CurrentPosition.IsAdjacentTo(target)) return false;

            // TryGetTile, а не GetOrCreateTile — плита, до которой ещё никто
            // не дотрагивался, не материализована и не может быть ни
            // препятствием (#9), ни ловушкой, ни разрушена распадом;
            // материализовывать её здесь как побочный эффект проверки хода
            // не нужно.
            if (_grid.TryGetTile(target, out var tile))
            {
                if (tile.IsBlocked && !_breachAvailable) return false;
                if (tile.IsGated && !tile.IsLeverGateOpen) return false;
                if (tile.LethalTrap.HasValue && tile.LethalTrap.Value != LethalTrapType.Lava) return false;
                if (_visited.Contains(target)) return !tile.IsDestroyed;
            }

            return true;
        }

        /// <summary>
        /// Продвигает трейл на <paramref name="target"/>, если ход валиден
        /// (см. <see cref="CanAdvanceTo"/>). Если цель — один из четырёх
        /// рантайм-типов смертельной ловушки, ход не засчитывается (как и
        /// для любой другой невалидной цели), но дополнительно поднимается
        /// <see cref="LethalTrapTriggered"/> — попытка шагнуть на ловушку
        /// сама по себе является игровым событием, даже если позиция не
        /// меняется (см. legacy/burmolda_demo.html, tryAct: attemptDeath
        /// вызывается и делается return без продвижения).
        ///
        /// Если цель — статичная <see cref="LethalTrapType.Lava"/> (issue
        /// #258), ход засчитывается как обычный (позиция/Path/<see cref="Advanced"/>/
        /// <see cref="PositionChanged"/> — всё как у безопасной плиты), а
        /// <see cref="LethalTrapTriggered"/> поднимается ПОСЛЕ продвижения,
        /// уже с координатой, ставшей текущей позицией — потребитель
        /// (<see cref="RunLifecycle.RunState"/>) не различает эти два случая
        /// сам, для него это всё то же событие.
        /// </summary>
        public bool TryAdvanceTo(GridCoordinate target)
        {
            if (_grid.Contains(target) && CurrentPosition.IsAdjacentTo(target) && _grid.TryGetTile(target, out var targetTile))
            {
                if (targetTile.LethalTrap.HasValue && targetTile.LethalTrap.Value != LethalTrapType.Lava)
                {
                    LethalTrapTriggered?.Invoke(target, targetTile.LethalTrap.Value);
                    return false;
                }
            }

            if (!CanAdvanceTo(target)) return false;

            // Отдельный (не переиспользующий targetTile выше — тот не
            // гарантированно присвоен на всех путях из-за короткого
            // замыкания &&) поиск: тратим Пробой, только если им реально
            // воспользовались (цель была заблокирована).
            if (_breachAvailable && _grid.TryGetTile(target, out var blockedTile) && blockedTile.IsBlocked)
                _breachAvailable = false;

            var isNewTile = _visited.Add(target);
            if (isNewTile)
            {
                _grid.GetOrCreateTile(target);
                _path.Add(target);
            }

            _currentPosition = target;
            if (isNewTile) Advanced?.Invoke(target);
            PositionChanged?.Invoke(target);

            // issue #258: Лава засчитывается как ход, но остаётся летальной
            // — событие поднимается ПОСЛЕ продвижения, не вместо него (в
            // отличие от четырёх рантайм-типов выше).
            if (_grid.TryGetTile(target, out var arrivedTile) && arrivedTile.LethalTrap == LethalTrapType.Lava)
                LethalTrapTriggered?.Invoke(target, LethalTrapType.Lava);

            return true;
        }

        /// <summary>
        /// Issue #260 («Бомба взрывается мгновенно и показывает текстуру
        /// Стрелы» — заодно закрывает открытый вопрос из doc-комментария
        /// <c>Movement.BombTrapSystem</c>, «что если игрок стоит на плите
        /// в момент, когда она становится смертельной»): проверяет, не
        /// стала ли <see cref="CurrentPosition"/> смертельной ловушкой ПРЯМО
        /// СЕЙЧАС, пока игрок уже на ней стоит, не совершая новый ход — если
        /// стала, поднимает <see cref="LethalTrapTriggered"/> точно так же,
        /// как если бы он попытался на неё шагнуть.
        ///
        /// Симметрично уже действующему <c>Decay.TrailDecaySystem.TileDestroyed</c>
        /// → <c>RunLifecycle.RunState.OnTileDestroyed</c> («обрушение плиты
        /// под ногами — тоже не новый ход, тоже отдельная явная проверка
        /// текущей позиции») — та же идея, применённая к <see cref="Tile.LethalTrap"/>
        /// вместо распада. Не-op, если текущая плита не несёт
        /// <see cref="Tile.LethalTrap"/> — безопасно вызывать "на всякий случай"
        /// после любого рантайм-перехода, который мог (но не обязан был)
        /// затронуть плиту под игроком.
        ///
        /// Общий примитив, не специфичный для Бомбы — issue #268 (живой тест
        /// устройства, владелец: «смерть не появляется после того, как в
        /// меня выстреливает стрела») применил его и к
        /// <c>ArrowWaveTrapSystem</c>/<c>BladeTactTrapSystem</c> — та же
        /// "известная остаточная проблема", предсказанная здесь заранее.
        /// <c>LavaWaveTrapSystem</c> не нуждается — у неё отдельный, более
        /// ранний инвариант: волна НИКОГДА не заливает ряд, на котором стоит
        /// игрок (issue #254/#249), поэтому плита под игроком физически не
        /// может стать смертельной этим путём.
        /// </summary>
        public void CheckCurrentPositionForLethalTrap()
        {
            if (!_grid.TryGetTile(CurrentPosition, out var tile) || !tile.LethalTrap.HasValue) return;
            LethalTrapTriggered?.Invoke(CurrentPosition, tile.LethalTrap.Value);
        }

        /// <summary>
        /// Даёт трейлу одноразовую возможность пройти через ЛЮБУЮ одну
        /// заблокированную плиту (PRD раздел 12, Тотем — "Пробой"). Тратится
        /// на первом же ходе, который фактически ей воспользовался (шаг на
        /// заблокированную плиту) — обычный ход на не заблокированную плиту
        /// её не расходует.
        /// </summary>
        public void PrimeBreach()
        {
            _breachAvailable = true;
        }

        /// <summary>
        /// Принудительно переносит текущую позицию на <paramref name="target"/>
        /// — не ход игрока: не требует соседства с текущей позицией, не
        /// проверяет препятствия/ловушки, не материализует новую плиту и не
        /// поднимает <see cref="Advanced"/> (target уже посещался раньше —
        /// иначе телепортировать туда было бы некуда). Используется
        /// d20-испытанием (PRD раздел 9, issue #24) — откат к последнему
        /// пройденному Алтарю при исходе Knockback. Поднимает
        /// <see cref="PositionChanged"/>, чтобы камера/прочие подписчики на
        /// текущую позицию сориентировались на новом месте.
        /// </summary>
        public void TeleportTo(GridCoordinate target)
        {
            if (!_grid.Contains(target))
                throw new ArgumentOutOfRangeException(nameof(target), target, "Координата телепорта вне сетки тоннеля.");

            _currentPosition = target;
            PositionChanged?.Invoke(target);
        }
    }
}
