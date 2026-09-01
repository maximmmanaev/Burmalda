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
    /// независимо от того, пройдена она трейлом или нет. Плита-цель ловушки
    /// с таймингом (<see cref="Tile.IsTimedTrapActive"/>, PRD v5 4.2, #45)
    /// непроходима, только пока активна — в отличие от прочих препятствий
    /// это временное состояние. Плита-ворота рычага (<see cref="Tile.IsGated"/>,
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
        /// Срабатывает, когда игрок пытается шагнуть на смертельную ловушку
        /// (<see cref="Tile.LethalTrap"/>, PRD 4.2) — сам ход при этом НЕ
        /// засчитывается (см. <see cref="TryAdvanceTo"/>), плита-ловушка не
        /// становится текущей позицией. Потребитель этого события — система,
        /// отвечающая за смерть/рестарт (Burmalda.RunLifecycle); сама
        /// GridTraceTrail ничего не знает о смерти.
        /// </summary>
        public event Action<GridCoordinate, LethalTrapType> LethalTrapTriggered;

        /// <summary>
        /// Срабатывает, когда игрок пытается шагнуть на плиту-цель ловушки с
        /// таймингом, пока она активна (<see cref="Tile.IsTimedTrapActive"/>,
        /// PRD v5 4.2, #45) — по аналогии с <see cref="LethalTrapTriggered"/>,
        /// сам ход при этом НЕ засчитывается.
        /// </summary>
        public event Action<GridCoordinate, TimedTrapType> TimedTrapTriggered;

        /// <summary>
        /// Ход на <paramref name="target"/> валиден, если плита в пределах
        /// сетки, соседняя текущей позиции, не является препятствием (#9), не
        /// смертельной ловушкой (PRD 4.2) и не активной прямо сейчас плитой-
        /// целью ловушки с таймингом (#45), и при этом либо ещё не пройдена
        /// трейлом, либо пройдена, но не разрушена распадом (#61).
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
                if (tile.LethalTrap.HasValue) return false;
                if (tile.IsTimedTrapActive) return false;
                if (_visited.Contains(target)) return !tile.IsDestroyed;
            }

            return true;
        }

        /// <summary>
        /// Продвигает трейл на <paramref name="target"/>, если ход валиден.
        /// Если цель — смертельная ловушка или активная прямо сейчас плита-
        /// цель ловушки с таймингом, ход не засчитывается (как и для любой
        /// другой невалидной цели), но дополнительно поднимается
        /// <see cref="LethalTrapTriggered"/>/<see cref="TimedTrapTriggered"/> —
        /// попытка шагнуть на ловушку сама по себе является игровым событием,
        /// даже если позиция не меняется (см. legacy/burmolda_demo.html,
        /// tryAct: attemptDeath вызывается и делается return без продвижения).
        /// </summary>
        public bool TryAdvanceTo(GridCoordinate target)
        {
            if (_grid.Contains(target) && CurrentPosition.IsAdjacentTo(target) && _grid.TryGetTile(target, out var targetTile))
            {
                if (targetTile.LethalTrap.HasValue)
                {
                    LethalTrapTriggered?.Invoke(target, targetTile.LethalTrap.Value);
                    return false;
                }

                if (targetTile.IsTimedTrapActive)
                {
                    TimedTrapTriggered?.Invoke(target, targetTile.TimedTrapKind.Value);
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
            return true;
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
