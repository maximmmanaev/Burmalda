using Burmalda.Core;
using NUnit.Framework;

namespace Burmalda.Movement.Tests
{
    public class GridTraceTrailTests
    {
        private static GridTraceTrail CreateTrail(GridCoordinate start)
        {
            var grid = new TunnelGrid(5);
            return new GridTraceTrail(grid, start);
        }

        [Test]
        public void Constructor_StartCoordinate_IsCurrentPositionAndOnlyPathEntry()
        {
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);

            Assert.AreEqual(start, trail.CurrentPosition);
            Assert.AreEqual(1, trail.Path.Count);
            Assert.AreEqual(start, trail.Path[0]);
        }

        [Test]
        public void HasVisited_StartCoordinate_ReturnsTrue()
        {
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);

            Assert.IsTrue(trail.HasVisited(start));
        }

        [Test]
        public void HasVisited_UnvisitedCoordinate_ReturnsFalse()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));

            Assert.IsFalse(trail.HasVisited(new GridCoordinate(1, 2)));
        }

        [Test]
        public void HasVisited_AfterAdvancing_ReturnsTrueForNewTile()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));
            var target = new GridCoordinate(1, 2);

            trail.TryAdvanceTo(target);

            Assert.IsTrue(trail.HasVisited(target));
        }

        [Test]
        public void CanAdvanceTo_AdjacentUnvisitedTile_ReturnsTrue()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));

            Assert.IsTrue(trail.CanAdvanceTo(new GridCoordinate(1, 2)));
        }

        [Test]
        public void CanAdvanceTo_NonAdjacentTile_ReturnsFalse()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));

            Assert.IsFalse(trail.CanAdvanceTo(new GridCoordinate(2, 2)));
        }

        [Test]
        public void CanAdvanceTo_OutOfGridBounds_ReturnsFalse()
        {
            var trail = CreateTrail(new GridCoordinate(0, 0));

            Assert.IsFalse(trail.CanAdvanceTo(new GridCoordinate(0, -1)));
            Assert.IsFalse(trail.CanAdvanceTo(new GridCoordinate(-1, 0)));
        }

        [Test]
        public void CanAdvanceTo_AlreadyVisitedNotDestroyedTile_ReturnsTrue()
        {
            // #61: повторный шаг на пройденную, но целую плиту — разрешён
            // (правило из прототипа/старой версии #6 отменено явным запросом).
            var trail = CreateTrail(new GridCoordinate(0, 2));
            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsTrue(trail.CanAdvanceTo(new GridCoordinate(0, 2)));
        }

        [Test]
        public void CanAdvanceTo_AlreadyVisitedDestroyedTile_ReturnsFalse()
        {
            // #61: блокируется только реально разрушенная распадом плита.
            var grid = new TunnelGrid(5);
            var start = new GridCoordinate(0, 2);
            var trail = new GridTraceTrail(grid, start);
            var previous = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(previous);

            var previousTile = grid.GetOrCreateTile(previous);
            previousTile.BeginDecay(1f);
            previousTile.AdvanceDecay(2f); // порог 1с превышен — плита разрушена
            Assert.IsTrue(previousTile.IsDestroyed, "тест некорректен, если плита не разрушилась");

            Assert.IsFalse(trail.CanAdvanceTo(previous));
        }

        [Test]
        public void CanAdvanceTo_UnvisitedNotYetMaterializedTile_ReturnsTrue()
        {
            // Регрессия: плита, до которой ещё никто не дотрагивался, не
            // материализована и не может быть препятствием по определению.
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));

            Assert.IsTrue(trail.CanAdvanceTo(new GridCoordinate(1, 2)));
        }

        [Test]
        public void CanAdvanceTo_UnvisitedBlockedTile_ReturnsFalse()
        {
            // #9: статичное препятствие — видно заранее, обойти нельзя, наступить нельзя.
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var blocked = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(blocked).MarkBlocked();

            Assert.IsFalse(trail.CanAdvanceTo(blocked));
        }

        [Test]
        public void TryAdvanceTo_BlockedTile_DoesNotAdvanceAndDoesNotMutatePath()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var blocked = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(blocked).MarkBlocked();

            var advanced = trail.TryAdvanceTo(blocked);

            Assert.IsFalse(advanced);
            Assert.AreEqual(new GridCoordinate(0, 2), trail.CurrentPosition);
            Assert.AreEqual(1, trail.Path.Count);
        }

        [Test]
        public void PrimeBreach_ThenAdvanceIntoBlockedTile_Succeeds()
        {
            // PRD раздел 12 (Тотем — "Пробой"): одноразовый проход сквозь препятствие.
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var blocked = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(blocked).MarkBlocked();

            trail.PrimeBreach();
            var advanced = trail.TryAdvanceTo(blocked);

            Assert.IsTrue(advanced);
            Assert.AreEqual(blocked, trail.CurrentPosition);
        }

        [Test]
        public void PrimeBreach_ConsumedAfterUse_SecondBlockedTileStillImpassable()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var firstBlocked = new GridCoordinate(1, 2);
            var secondBlocked = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(firstBlocked).MarkBlocked();
            grid.GetOrCreateTile(secondBlocked).MarkBlocked();

            trail.PrimeBreach();
            trail.TryAdvanceTo(firstBlocked);
            var secondAdvance = trail.TryAdvanceTo(secondBlocked);

            Assert.IsFalse(secondAdvance);
            Assert.AreEqual(firstBlocked, trail.CurrentPosition);
        }

        [Test]
        public void PrimeBreach_MoveIntoNonBlockedTileFirst_DoesNotConsumeBreach()
        {
            // Обычный ход на не заблокированную плиту не должен тратить заряд Пробоя.
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var open = new GridCoordinate(1, 2);
            var blocked = new GridCoordinate(2, 2);
            grid.GetOrCreateTile(blocked).MarkBlocked();

            trail.PrimeBreach();
            trail.TryAdvanceTo(open);
            var advancedThroughBlocked = trail.TryAdvanceTo(blocked);

            Assert.IsTrue(advancedThroughBlocked);
            Assert.AreEqual(blocked, trail.CurrentPosition);
        }

        [Test]
        public void CanAdvanceTo_BlockedTileWithoutBreach_StillReturnsFalse()
        {
            // Регрессия: без активного Пробоя препятствие непроходимо как раньше (#9).
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var blocked = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(blocked).MarkBlocked();

            Assert.IsFalse(trail.CanAdvanceTo(blocked));
        }

        [Test]
        public void CanAdvanceTo_ClosedGatedTile_ReturnsFalse()
        {
            // #51: плита бокового прохода закрыта, пока не активирован рычаг.
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var gated = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(gated).MarkGated();

            Assert.IsFalse(trail.CanAdvanceTo(gated));
        }

        [Test]
        public void CanAdvanceTo_OpenedGatedTile_ReturnsTrue()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var gated = new GridCoordinate(1, 2);
            var tile = grid.GetOrCreateTile(gated);
            tile.MarkGated();
            tile.OpenLeverGate();

            Assert.IsTrue(trail.CanAdvanceTo(gated));
        }

        // Issue #258: статичная Лава (LethalTrapType.Lava) перестала быть
        // единственным примером "смертельной ловушки" в этих тестах — теперь
        // она единственная, которая физически проходима (см. блок тестов
        // ниже, "Лава — проходимая, но летальная"). Оставшиеся четыре
        // рантайм-типа (ArrowWave/BombBlast/BladeTact/LavaWave) по-прежнему
        // ведут себя как раньше — жёсткая стена, ход не засчитывается — этот
        // общий механизм тестируется ниже на ArrowWave как представителе.

        [Test]
        public void CanAdvanceTo_UnvisitedLethalTrapTile_ReturnsFalse()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var arrowWaveTile = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(arrowWaveTile).TransitionToLethalTrap(LethalTrapType.ArrowWave);

            Assert.IsFalse(trail.CanAdvanceTo(arrowWaveTile));
        }

        [Test]
        public void TryAdvanceTo_LethalTrapTile_DoesNotAdvanceAndDoesNotMutatePath()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var arrowWaveTile = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(arrowWaveTile).TransitionToLethalTrap(LethalTrapType.ArrowWave);

            var advanced = trail.TryAdvanceTo(arrowWaveTile);

            Assert.IsFalse(advanced);
            Assert.AreEqual(new GridCoordinate(0, 2), trail.CurrentPosition);
            Assert.AreEqual(1, trail.Path.Count);
        }

        [Test]
        public void TryAdvanceTo_LethalTrapTile_FiresLethalTrapTriggeredWithCoordinateAndType()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var arrowWaveTile = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(arrowWaveTile).TransitionToLethalTrap(LethalTrapType.ArrowWave);
            GridCoordinate? firedCoordinate = null;
            LethalTrapType? firedType = null;
            trail.LethalTrapTriggered += (coordinate, type) =>
            {
                firedCoordinate = coordinate;
                firedType = type;
            };

            trail.TryAdvanceTo(arrowWaveTile);

            Assert.AreEqual(arrowWaveTile, firedCoordinate);
            Assert.AreEqual(LethalTrapType.ArrowWave, firedType);
        }

        // Issue #258: «Лава должна быть проходимой, но летальной, а не
        // физической стеной» — единственное исключение из блока выше. В
        // отличие от четырёх РАНТАЙМ-типов, статичная Лава ставится через
        // MarkLethalTrap на этапе генерации (issue #251) — шаг на неё
        // физически разрешается (палец может перетащить трейл на такую
        // плитку), результат — то же событие LethalTrapTriggered, что и у
        // прочих ловушек, но ПОСЛЕ фактического продвижения, не вместо него.
        // Разрешение опасности (d20, RunLifecycle.RunState.ResolveHazard) не
        // меняется — этот класс ничего не знает про d20, только поднимает
        // событие с координатой уже ставшей текущей позицией.

        [Test]
        public void CanAdvanceTo_UnvisitedLavaTile_ReturnsTrue()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var lava = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(lava).MarkLethalTrap(LethalTrapType.Lava);

            Assert.IsTrue(trail.CanAdvanceTo(lava), "Лава должна быть физически проходима (issue #258) — не стена, а риск.");
        }

        [Test]
        public void TryAdvanceTo_LavaTile_AdvancesAndUpdatesCurrentPositionAndPath()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var lava = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(lava).MarkLethalTrap(LethalTrapType.Lava);

            var advanced = trail.TryAdvanceTo(lava);

            Assert.IsTrue(advanced, "Шаг на Лаву не должен отклоняться (issue #258).");
            Assert.AreEqual(lava, trail.CurrentPosition);
            Assert.AreEqual(2, trail.Path.Count);
        }

        [Test]
        public void TryAdvanceTo_LavaTile_FiresLethalTrapTriggeredWithCoordinateAndType()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var lava = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(lava).MarkLethalTrap(LethalTrapType.Lava);
            GridCoordinate? firedCoordinate = null;
            LethalTrapType? firedType = null;
            trail.LethalTrapTriggered += (coordinate, type) =>
            {
                firedCoordinate = coordinate;
                firedType = type;
            };

            trail.TryAdvanceTo(lava);

            Assert.AreEqual(lava, firedCoordinate);
            Assert.AreEqual(LethalTrapType.Lava, firedType);
        }

        [Test]
        public void TryAdvanceTo_LavaTile_FiresAdvancedAndPositionChangedLikeAnyOtherTile()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var lava = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(lava).MarkLethalTrap(LethalTrapType.Lava);
            var advancedFired = false;
            var positionChangedFired = false;
            trail.Advanced += _ => advancedFired = true;
            trail.PositionChanged += _ => positionChangedFired = true;

            trail.TryAdvanceTo(lava);

            Assert.IsTrue(advancedFired, "Лава — обычная (хоть и опасная) плита с точки зрения продвижения трейла.");
            Assert.IsTrue(positionChangedFired);
        }

        [Test]
        public void TryAdvanceTo_RevisitAlreadyVisitedLavaTile_StillFiresLethalTrapTriggered()
        {
            // Символизирует исход Fortune (RunState.ResolveHazard) — игрок
            // выжил на плите Лавы в первый раз и может снова попытаться
            // пройти по тому же трейлу через неё же (#61, повтор пройденной
            // плиты разрешён). Риск не должен исчезать при повторном шаге.
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var lava = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(lava).MarkLethalTrap(LethalTrapType.Lava);
            trail.TryAdvanceTo(lava);
            trail.TryAdvanceTo(new GridCoordinate(0, 2)); // назад на старт
            var fired = false;
            trail.LethalTrapTriggered += (_, _) => fired = true;

            var advanced = trail.TryAdvanceTo(lava);

            Assert.IsTrue(advanced);
            Assert.IsTrue(fired, "Повторный шаг на уже пройденную Лаву должен снова поднимать LethalTrapTriggered.");
        }

        [Test]
        public void TryAdvanceTo_BlockedTile_DoesNotFireLethalTrapTriggered()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var blocked = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(blocked).MarkBlocked();
            var fired = false;
            trail.LethalTrapTriggered += (_, _) => fired = true;

            trail.TryAdvanceTo(blocked);

            Assert.IsFalse(fired, "заблокированная плита — не ловушка, событие смерти не при чём");
        }

        [Test]
        public void TryAdvanceTo_ValidMove_DoesNotFireLethalTrapTriggered()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));
            var fired = false;
            trail.LethalTrapTriggered += (_, _) => fired = true;

            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            Assert.IsFalse(fired);
        }

        // Владелец, 2026-09-05 «оставить только пять новых ловушек»: раньше
        // здесь были 5 тестов на Tile.ArmTimedTrap/DisarmTimedTrap/
        // GridTraceTrail.TimedTrapTriggered — вся эта старая механика
        // реального времени (Movement.TimedTrapSystem, Core.TimedTrapType)
        // удалена целиком. Турн-баседные ловушки (ArrowWave/BombBlast/
        // BladeTact/LavaWave/FallingRock) переключают ту же плиту через
        // MarkLethalTrap/TransitionToLethalTrap и переиспользуют уже
        // покрытые выше LethalTrapTriggered-тесты — отдельного события для
        // них не заводилось.

        [Test]
        public void TryAdvanceTo_ValidMove_AppendsToPathAndUpdatesCurrentPosition()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));
            var target = new GridCoordinate(1, 3);

            var advanced = trail.TryAdvanceTo(target);

            Assert.IsTrue(advanced);
            Assert.AreEqual(target, trail.CurrentPosition);
            Assert.AreEqual(2, trail.Path.Count);
        }

        [Test]
        public void TryAdvanceTo_InvalidMove_ReturnsFalseAndDoesNotMutatePath()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));

            var advanced = trail.TryAdvanceTo(new GridCoordinate(3, 3));

            Assert.IsFalse(advanced);
            Assert.AreEqual(1, trail.Path.Count);
        }

        [Test]
        public void TryAdvanceTo_ZigzagPathOverNeverVisitedTiles_AllStepsSucceed()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));

            Assert.IsTrue(trail.TryAdvanceTo(new GridCoordinate(1, 3)));
            Assert.IsTrue(trail.TryAdvanceTo(new GridCoordinate(2, 2)));
            Assert.IsTrue(trail.TryAdvanceTo(new GridCoordinate(3, 3)));
            Assert.AreEqual(4, trail.Path.Count);
        }

        // #61: повторный проход по не разрушенной плите — CurrentPosition
        // двигается, но Path остаётся списком уникальных плит без дублей
        // (см. обсуждение issue #61 — Decay/DebugVisuals рассчитывают на то,
        // что Path не содержит повторов).

        [Test]
        public void TryAdvanceTo_RevisitNotDestroyedTile_MovesCurrentPositionWithoutDuplicatingPath()
        {
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            var pathCountBeforeRevisit = trail.Path.Count;

            var revisited = trail.TryAdvanceTo(start);

            Assert.IsTrue(revisited);
            Assert.AreEqual(start, trail.CurrentPosition);
            Assert.AreEqual(pathCountBeforeRevisit, trail.Path.Count);
        }

        [Test]
        public void TryAdvanceTo_RevisitDestroyedTile_ReturnsFalseAndDoesNotMoveCurrentPosition()
        {
            var grid = new TunnelGrid(5);
            var start = new GridCoordinate(0, 2);
            var trail = new GridTraceTrail(grid, start);
            var previous = new GridCoordinate(1, 2);
            trail.TryAdvanceTo(previous);
            trail.TryAdvanceTo(new GridCoordinate(2, 2)); // текущая позиция теперь не previous

            var previousTile = grid.GetOrCreateTile(previous);
            previousTile.BeginDecay(1f);
            previousTile.AdvanceDecay(2f);

            var revisited = trail.TryAdvanceTo(previous);

            Assert.IsFalse(revisited);
            Assert.AreEqual(new GridCoordinate(2, 2), trail.CurrentPosition);
        }

        [Test]
        public void TryAdvanceTo_RevisitNotDestroyedTile_DoesNotFireAdvancedEvent()
        {
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            var advancedCoordinates = new System.Collections.Generic.List<GridCoordinate>();
            trail.Advanced += c => advancedCoordinates.Add(c);

            trail.TryAdvanceTo(start);

            Assert.IsEmpty(advancedCoordinates);
        }

        [Test]
        public void TryAdvanceTo_NewTile_FiresAdvancedEventWithTargetCoordinate()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));
            var target = new GridCoordinate(1, 2);
            GridCoordinate? fired = null;
            trail.Advanced += c => fired = c;

            trail.TryAdvanceTo(target);

            Assert.AreEqual(target, fired);
        }

        [Test]
        public void TryAdvanceTo_NewTile_FiresPositionChangedEventWithTargetCoordinate()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));
            var target = new GridCoordinate(1, 2);
            GridCoordinate? fired = null;
            trail.PositionChanged += c => fired = c;

            trail.TryAdvanceTo(target);

            Assert.AreEqual(target, fired);
        }

        [Test]
        public void TryAdvanceTo_RevisitNotDestroyedTile_FiresPositionChangedEvent()
        {
            // В отличие от Advanced (не срабатывает на повтор, #61),
            // PositionChanged должен срабатывать на любой успешный ход —
            // иначе следящие за текущей позицией системы (например, камера)
            // не смогут узнать, что игрок вернулся назад по трейлу.
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            GridCoordinate? fired = null;
            trail.PositionChanged += c => fired = c;

            trail.TryAdvanceTo(start);

            Assert.AreEqual(start, fired);
        }

        [Test]
        public void TryAdvanceTo_InvalidMove_DoesNotFirePositionChangedEvent()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));
            var firedCount = 0;
            trail.PositionChanged += _ => firedCount++;

            trail.TryAdvanceTo(new GridCoordinate(3, 3));

            Assert.AreEqual(0, firedCount);
        }

        [Test]
        public void TryAdvanceTo_AfterRevisit_CanContinueToNewAdjacentTile()
        {
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            trail.TryAdvanceTo(start); // возврат на старт

            var advanced = trail.TryAdvanceTo(new GridCoordinate(1, 3)); // новая плита, соседняя старту

            Assert.IsTrue(advanced);
            Assert.AreEqual(new GridCoordinate(1, 3), trail.CurrentPosition);
            Assert.AreEqual(3, trail.Path.Count); // start, (1,2), (1,3) — без дублей
        }

        [Test]
        public void TeleportTo_SetsCurrentPosition()
        {
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));

            trail.TeleportTo(start);

            Assert.AreEqual(start, trail.CurrentPosition);
        }

        [Test]
        public void TeleportTo_NonAdjacentCoordinate_Succeeds()
        {
            // Не ход игрока — соседство не требуется (#24, откат к Алтарю может быть далеко).
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);

            trail.TeleportTo(new GridCoordinate(4, 0));

            Assert.AreEqual(new GridCoordinate(4, 0), trail.CurrentPosition);
        }

        [Test]
        public void TeleportTo_RaisesPositionChangedButNotAdvanced()
        {
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);
            var advancedFired = false;
            GridCoordinate? positionChanged = null;
            trail.Advanced += _ => advancedFired = true;
            trail.PositionChanged += c => positionChanged = c;

            trail.TeleportTo(new GridCoordinate(3, 3));

            Assert.IsFalse(advancedFired);
            Assert.AreEqual(new GridCoordinate(3, 3), positionChanged);
        }

        [Test]
        public void TeleportTo_DoesNotMutatePathOrVisitedSet()
        {
            var start = new GridCoordinate(0, 2);
            var trail = CreateTrail(start);
            trail.TryAdvanceTo(new GridCoordinate(1, 2));
            var pathCountBefore = trail.Path.Count;

            trail.TeleportTo(new GridCoordinate(4, 4));

            Assert.AreEqual(pathCountBefore, trail.Path.Count);
        }

        [Test]
        public void TeleportTo_OutOfGridBounds_Throws()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));

            Assert.Throws<System.ArgumentOutOfRangeException>(() => trail.TeleportTo(new GridCoordinate(0, 99)));
        }

        // Issue #260 («Бомба взрывается мгновенно...» — заодно закрывает
        // «что если игрок стоит на плите, когда она становится смертельной
        // не по новому ходу», см. doc-комментарий метода).
        [Test]
        public void CheckCurrentPositionForLethalTrap_CurrentTileHasNoLethalTrap_DoesNotFire()
        {
            var trail = CreateTrail(new GridCoordinate(0, 2));
            var fired = false;
            trail.LethalTrapTriggered += (_, _) => fired = true;

            trail.CheckCurrentPositionForLethalTrap();

            Assert.IsFalse(fired);
        }

        [Test]
        public void CheckCurrentPositionForLethalTrap_CurrentTileBecameLethalWithoutAMove_FiresLethalTrapTriggeredWithCoordinateAndType()
        {
            var grid = new TunnelGrid(5);
            var start = new GridCoordinate(0, 2);
            var trail = new GridTraceTrail(grid, start);
            // Симулирует внешнюю систему (BombTrapSystem), которая делает
            // плиту под уже стоящим на ней игроком смертельной ПОСТФАКТУМ,
            // не через TryAdvanceTo.
            grid.GetOrCreateTile(start).TransitionToLethalTrap(LethalTrapType.BombBlast);
            GridCoordinate? firedCoordinate = null;
            LethalTrapType? firedType = null;
            trail.LethalTrapTriggered += (coordinate, type) =>
            {
                firedCoordinate = coordinate;
                firedType = type;
            };

            trail.CheckCurrentPositionForLethalTrap();

            Assert.AreEqual(start, firedCoordinate);
            Assert.AreEqual(LethalTrapType.BombBlast, firedType);
        }

        [Test]
        public void CheckCurrentPositionForLethalTrap_OnlyOtherTileBecameLethal_DoesNotFire()
        {
            var grid = new TunnelGrid(5);
            var trail = new GridTraceTrail(grid, new GridCoordinate(0, 2));
            var otherTile = new GridCoordinate(1, 2);
            grid.GetOrCreateTile(otherTile).TransitionToLethalTrap(LethalTrapType.BombBlast);
            var fired = false;
            trail.LethalTrapTriggered += (_, _) => fired = true;

            trail.CheckCurrentPositionForLethalTrap();

            Assert.IsFalse(fired, "проверяется только CurrentPosition, не произвольная плита");
        }
    }
}
