using System;
using System.Collections.Generic;

namespace Burmalda.Generation
{
    /// <summary>
    /// Проверка проходимости шаблона на этапе авторинга (PRD v7 §21, issue
    /// #78): "существует путь вход→выход, не проходящий через заблокированные
    /// плиты; проверяется на этапе авторинга, не в рантайме". Гарантия
    /// сильнее буквальной формулировки: путь должен существовать из КАЖДОЙ
    /// проходимой плиты ряда входа (row 0) — игрок может войти в сегмент в
    /// любом столбце, не только в одном заранее известном.
    ///
    /// Непроходимыми для этой проверки считаются <see cref="SegmentTileType.Blocked"/>,
    /// <see cref="SegmentTileType.LeverGate"/> (закрыта по умолчанию, issue
    /// #51 — "недоступный по основному маршруту": если единственный путь к
    /// выходу идёт через неё, основной маршрут на деле непроходим без
    /// рычага) и <see cref="SegmentTileType.Lava"/> (issue #251 — статичная
    /// Лава становится <c>LethalTrap</c> уже В МОМЕНТ генерации
    /// (<c>SegmentRowProvider.ApplyTileType</c> → <c>Tile.MarkLethalTrap</c>),
    /// а <see cref="Movement.GridTraceTrail.TryAdvanceTo"/> категорически
    /// отклоняет ЛЮБОЙ шаг на <c>LethalTrap.HasValue</c> независимо от исхода
    /// d20 — физическая стена, не риск, которым можно рискнуть, несмотря на
    /// более раннюю формулировку этого комментария). Пять триггеров ловушек
    /// (issues #213-#217) — ДРУГОЙ случай: на этапе генерации это обычный
    /// безопасный пол, опасность наступает ПОЗЖЕ через отдельную систему, не
    /// в момент шага на саму плиту-триггер — они по-прежнему проходимы для
    /// этой проверки заслуженно.
    ///
    /// <b>Отдельная проверка тайника (issue #208, плейтест владельца
    /// 2026-08-31): "рычаги появились но стены по прежнему не пускают к
    /// ключу".</b> <see cref="IsTraversable"/> намеренно не гарантирует
    /// ничего про содержимое ЗА воротами — она про основной маршрут в обход
    /// рычага. Три шаблона партии 2 (<c>двор-с-рычагом</c>,
    /// <c>ворота-склада</c>, <c>рычаг-в-глубине</c>) её проходили, но клетка
    /// ворот была со всех сторон окружена <see cref="SegmentTileType.Blocked"/>
    /// кроме самой награды за ней — "карман" замкнут сам на себя и не
    /// соединён с остальным сегментом даже при открытых воротах. См.
    /// <see cref="LeverVaultIsReachable"/> — это её ловит.
    ///
    /// <b>Правило избегаемости развёрнуто (владелец, задача «Стены вместо
    /// ловушек», Спринт после Яруса 22): "сложность создают стены и
    /// геометрия маршрута, а не количество ловушек".</b> Раньше здесь стояло
    /// обратное правило — "плотность ловушечных плит подняли, плотность
    /// встреч — нет" (2026-09-08), из чего следовало требование, чтобы путь,
    /// избегающий ВСЕХ ловушек, не существовал (метод назывался
    /// <c>IsToothless</c> — история в git). Отменено целиком:
    /// владелец дошёл до Яруса 22, просто нажимая вперёд — ловушка как
    /// обязательный капкан не создавала ощущаемой сложности, а лишала
    /// авторов шаблонов свободы расставлять их как риск, а не барьер. Новое
    /// правило — прямо противоположное: <see cref="HasAvoidableRoute"/>
    /// ОБЯЗАНА возвращать true для ЛЮБОГО шаблона каталога, не только для
    /// части без ловушек. Смерть от ловушки — всегда следствие спешки или
    /// жадности игрока (сошёл с безопасного обхода ради награды под
    /// ловушкой), никогда не следствие того, что обхода не существовало.
    /// </summary>
    public static class SegmentReachabilityValidator
    {
        public static bool IsTraversable(SegmentTemplate template)
        {
            var componentId = ComputeComponents(template, IsPassableGateClosed);

            var exitComponents = new HashSet<int>();
            var exitRow = template.RowCount - 1;
            for (var c = 0; c < template.Width; c++)
                if (IsPassableGateClosed(template, exitRow, c))
                    exitComponents.Add(componentId[exitRow, c]);

            if (exitComponents.Count == 0) return false; // выход целиком заблокирован

            var hasEntry = false;
            for (var c = 0; c < template.Width; c++)
            {
                if (!IsPassableGateClosed(template, 0, c)) continue;
                hasEntry = true;
                if (!exitComponents.Contains(componentId[0, c])) return false; // эта плита входа не соединена ни с одним выходом
            }

            return hasEntry; // вход целиком заблокирован — тоже невалидно
        }

        /// <summary>
        /// Проверяет, что каждая плита <see cref="SegmentTileType.LeverGate"/>
        /// в шаблоне соединена с рядом входа (row 0), если ворота считать
        /// проходимыми ("рычаг уже нажат") — то есть за воротами не
        /// замкнутый на себя карман, отрезанный от остального сегмента
        /// стенами со всех прочих сторон. Шаблоны без ворот проходят
        /// тривиально. Дополняет <see cref="IsTraversable"/>, которая
        /// сознательно не проверяет доступность содержимого за воротами
        /// (issue #208).
        /// </summary>
        public static bool LeverVaultIsReachable(SegmentTemplate template)
        {
            var hasGate = false;
            for (var r = 0; r < template.RowCount && !hasGate; r++)
                for (var c = 0; c < template.Width; c++)
                    if (template.TileAt(r, c) == SegmentTileType.LeverGate) { hasGate = true; break; }
            if (!hasGate) return true; // нечего проверять

            var componentId = ComputeComponents(template, IsPassableGateOpen);

            var entryComponents = new HashSet<int>();
            for (var c = 0; c < template.Width; c++)
                if (IsPassableGateOpen(template, 0, c))
                    entryComponents.Add(componentId[0, c]);

            for (var r = 0; r < template.RowCount; r++)
                for (var c = 0; c < template.Width; c++)
                    if (template.TileAt(r, c) == SegmentTileType.LeverGate && !entryComponents.Contains(componentId[r, c]))
                        return false;

            return true;
        }

        /// <summary>
        /// Issue #193: "проверка возвратного маршрута (открывашка → Ворота),
        /// не только прямого маршрута входа". Математически это СЛЕДСТВИЕ
        /// <see cref="IsTraversable"/> + <see cref="LeverVaultIsReachable"/>
        /// вместе взятых: граф проходимости неориентированный (шаг в игре
        /// не завязан на направление — <c>GridTraceTrail.CanAdvanceTo</c>
        /// разрешает любую соседнюю плиту), поэтому если тайник за
        /// открытыми воротами связан с рядом входа
        /// (<see cref="LeverVaultIsReachable"/>), а вход связан с выходом
        /// хотя бы при ЗАКРЫТЫХ воротах (<see cref="IsTraversable"/>, граф —
        /// подмножество открытого), то тайник связан и с выходом. Задача
        /// прямо просит ОТДЕЛЬНУЮ проверку с явным именем — не полагаться
        /// на то, что кто-то восстановит эту цепочку рассуждений из двух
        /// других методов, когда придётся объяснять, почему возврат
        /// гарантирован. Шаблоны без ворот проходят тривиально.
        /// </summary>
        public static bool ReturnRouteToExitExists(SegmentTemplate template)
        {
            var hasGate = false;
            for (var r = 0; r < template.RowCount && !hasGate; r++)
                for (var c = 0; c < template.Width; c++)
                    if (template.TileAt(r, c) == SegmentTileType.LeverGate) { hasGate = true; break; }
            if (!hasGate) return true; // нечего проверять

            var componentId = ComputeComponents(template, IsPassableGateOpen);

            var exitComponents = new HashSet<int>();
            var exitRow = template.RowCount - 1;
            for (var c = 0; c < template.Width; c++)
                if (IsPassableGateOpen(template, exitRow, c))
                    exitComponents.Add(componentId[exitRow, c]);

            for (var r = 0; r < template.RowCount; r++)
                for (var c = 0; c < template.Width; c++)
                    if (template.TileAt(r, c) == SegmentTileType.LeverGate && !exitComponents.Contains(componentId[r, c]))
                        return false;

            return true;
        }

        /// <summary>
        /// Правило избегаемости (владелец, задача «Стены вместо ловушек»,
        /// после того как забег дошёл до Яруса 22 простым нажатием вперёд):
        /// "сложность создают стены и геометрия маршрута, а не количество
        /// ловушек". Через ЛЮБОЙ шаблон обязан существовать путь вход→выход,
        /// не проходящий НИ ЧЕРЕЗ ОДНУ плиту-ловушку (см. <see cref="IsTrapTile"/>)
        /// — то есть шаблон обязан быть проходим начисто, обходом. Ловушка —
        /// не обязательный капкан, а инструмент, сужающий пространство
        /// маршрута; контакт с ней — всегда выбор игрока (срезал угол ради
        /// награды под ловушкой), не принуждение генерации.
        ///
        /// Тот же граф, что <see cref="IsTraversable"/> (Blocked/LeverGate
        /// закрыты, 8-направленное соседство, ворота закрыты по умолчанию —
        /// открытые ворота открывают ДОПОЛНИТЕЛЬНЫЙ путь, не убирают уже
        /// существующий обходной), плюс плиты-ловушки ТОЖЕ непроходимы для
        /// этой проверки — не потому что игрок физически не может на них
        /// шагнуть (может, там и весь риск), а потому что мы ищем путь,
        /// который их избегает: если такой путь существует в этом графе, он
        /// существует и в игре.
        ///
        /// Тир не учитывается здесь — метод применяется ко ВСЕМУ каталогу
        /// без исключений (тир 1 и так обычно без ловушек вообще, проходит
        /// тривиально) — это решение вызывающей стороны
        /// (см. <c>SegmentTemplateCatalogTests</c>), не этого метода — как и
        /// <see cref="IsTraversable"/>, он — чистая геометрия шаблона.
        ///
        /// <b>История:</b> до этой задачи здесь стоял метод <c>IsToothless</c>
        /// с ОБРАТНЫМ смыслом true/false (true = «шаблон обходится начисто» =
        /// раньше считалось браком). Переименован, а не оставлен под старым
        /// именем с инвертированной семантикой — имя "беззубый" не должно
        /// оставаться в коде как описание того, что теперь является
        /// требованием, а не браком.
        /// </summary>
        public static bool HasAvoidableRoute(SegmentTemplate template)
        {
            var componentId = ComputeComponents(template, IsPassableAvoidingTraps);

            var exitComponents = new HashSet<int>();
            var exitRow = template.RowCount - 1;
            for (var c = 0; c < template.Width; c++)
                if (IsPassableAvoidingTraps(template, exitRow, c))
                    exitComponents.Add(componentId[exitRow, c]);

            if (exitComponents.Count == 0) return false; // выход сам по себе — ловушка/стена на каждом столбце — обходной путь невозможен в принципе

            for (var c = 0; c < template.Width; c++)
            {
                if (!IsPassableAvoidingTraps(template, 0, c)) continue;
                if (exitComponents.Contains(componentId[0, c])) return true; // нашли хотя бы одну плиту входа, безопасно связанную с выходом
            }

            return false; // либо весь вход — ловушки, либо ни одна безопасная плита входа не связана с безопасным выходом
        }

        /// <summary>Плиты-ловушки для <see cref="HasAvoidableRoute"/> — пять новых триггеров (issues #213–#217) и статичная Лава. Заблокированные плиты сюда не входят — они уже непроходимы физически, отдельная роль, не ловушка (см. её же комментарий в <see cref="SegmentTileType"/>).</summary>
        private static bool IsTrapTile(SegmentTileType type) => type switch
        {
            SegmentTileType.Lava => true,
            SegmentTileType.ArrowWaveTrigger => true,
            SegmentTileType.BombTrigger => true,
            SegmentTileType.BladeTactTrigger => true,
            SegmentTileType.FallingRockTrigger => true,
            SegmentTileType.LavaWaveTrigger => true,
            _ => false,
        };

        private static bool IsPassableAvoidingTraps(SegmentTemplate template, int row, int column)
        {
            var type = template.TileAt(row, column);
            return type != SegmentTileType.Blocked && type != SegmentTileType.LeverGate && !IsTrapTile(type);
        }

        private static bool IsPassableGateClosed(SegmentTemplate template, int row, int column)
        {
            var type = template.TileAt(row, column);
            return type != SegmentTileType.Blocked && type != SegmentTileType.LeverGate && type != SegmentTileType.Lava;
        }

        private static bool IsPassableGateOpen(SegmentTemplate template, int row, int column)
        {
            var type = template.TileAt(row, column);
            return type != SegmentTileType.Blocked && type != SegmentTileType.Lava;
        }

        // BFS, 8-направленное соседство — тот же принцип, что GridCoordinate.IsAdjacentTo/TunnelGrid.GetNeighbors.
        private static int[,] ComputeComponents(SegmentTemplate template, Func<SegmentTemplate, int, int, bool> isPassable)
        {
            var componentId = new int[template.RowCount, template.Width];
            for (var r = 0; r < template.RowCount; r++)
                for (var c = 0; c < template.Width; c++)
                    componentId[r, c] = -1;

            var nextComponentId = 0;
            for (var r = 0; r < template.RowCount; r++)
                for (var c = 0; c < template.Width; c++)
                {
                    if (!isPassable(template, r, c)) continue;
                    if (componentId[r, c] != -1) continue;

                    FloodFill(template, componentId, r, c, nextComponentId, isPassable);
                    nextComponentId++;
                }

            return componentId;
        }

        private static void FloodFill(SegmentTemplate template, int[,] componentId, int startRow, int startColumn, int id, Func<SegmentTemplate, int, int, bool> isPassable)
        {
            var queue = new Queue<(int row, int column)>();
            queue.Enqueue((startRow, startColumn));
            componentId[startRow, startColumn] = id;

            while (queue.Count > 0)
            {
                var (row, column) = queue.Dequeue();
                for (var deltaRow = -1; deltaRow <= 1; deltaRow++)
                for (var deltaColumn = -1; deltaColumn <= 1; deltaColumn++)
                {
                    if (deltaRow == 0 && deltaColumn == 0) continue;

                    var neighborRow = row + deltaRow;
                    var neighborColumn = column + deltaColumn;
                    if (neighborRow < 0 || neighborRow >= template.RowCount) continue;
                    if (neighborColumn < 0 || neighborColumn >= template.Width) continue;
                    if (componentId[neighborRow, neighborColumn] != -1) continue;
                    if (!isPassable(template, neighborRow, neighborColumn)) continue;

                    componentId[neighborRow, neighborColumn] = id;
                    queue.Enqueue((neighborRow, neighborColumn));
                }
            }
        }
    }
}
