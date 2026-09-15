using System;
using System.Collections.Generic;
using Burmalda.Core;
using Burmalda.Movement;

namespace Burmalda.Generation
{
    /// <summary>
    /// Применяет выбранные <see cref="SegmentSelector"/> шаблоны к
    /// <see cref="TunnelGrid"/> целыми сегментами вместо поплиточного броска
    /// (PRD v7 §21, issue #78) — заменяет связку
    /// <c>Core.TunnelObstacleGenerator</c> + <c>Movement.TunnelGridReveal</c>.
    /// Материализует плиты на <see cref="RowsAheadOfPlayer"/> рядов вперёд
    /// игрока (тот же запас и тот же смысл, что у <c>TunnelGridReveal</c> —
    /// препятствия должны быть видны заранее, PRD 4.2), но целыми
    /// сегментами, а не построчно: если запрошенный ряд оказывается внутри
    /// уже частично покрытого сегмента, покрытие продвигается до конца
    /// текущего сегмента, а не останавливается посередине.
    ///
    /// Стартовый ряд трейла (обычно 0) уже безопасен по построению
    /// (<see cref="GridTraceTrail"/> материализует его как обычную плиту) —
    /// первый применённый здесь шаблон всегда начинается со следующего ряда,
    /// см. <see cref="_appliedThroughRow"/>.
    ///
    /// <b>Сосуществование с Core.TunnelObstacleGenerator (переходное
    /// состояние, docs/wiki/roadmap.md)</b>: каждый применённый ряд шаблона
    /// заявляется через <see cref="TunnelGrid.ClaimRow"/> ДО материализации
    /// его плит (см. <see cref="ApplyTemplate"/>) — это единственный
    /// источник claim в проекте, и именно порядок "заявить, потом
    /// материализовать" делает двойную запись в одну плиту невозможной по
    /// конструкции, а не по факту, что оба генератора "договорились" не
    /// мешать друг другу.
    /// </summary>
    public sealed class SegmentRowProvider : IDisposable
    {
        // Тот же смысл и тот же запас, что TunnelGridReveal.RowsAheadOfPlayer.
        public const int RowsAheadOfPlayer = 8;

        // Задача «Алтари на 1/3 и 2/3 Яруса» (владелец, Спринт «Стены вместо
        // ловушек», задача 5): гарантированный минимум реального наполнения
        // между любыми двумя фиксированными landmark-сегментами (Алтарь→
        // Алтарь, Алтарь→Комната) — наименьший возможный сегмент-заполнитель
        // (SegmentTemplate.MinRowCount) ровно один раз. Не 0 (было бы снова
        // тем самым багом — слипание) и не искусственно большое число (не
        // авторский баланс, просто "не ноль"). Используется только как
        // fallback для коротких Ярусов, где 1/3/2/3 сами по себе оказались
        // бы слишком близко — см. doc-комментарий EnsureCoveredThrough.
        private const int MinRowsBetweenLandmarks = SegmentTemplate.MinRowCount;

        // Порядок фиксированных landmark-сегментов внутри одного Яруса —
        // см. doc-комментарий EnsureCoveredThrough.
        private enum Landmark { AltarOne, AltarTwo, Boss }

        private readonly TunnelGrid _grid;
        private readonly GridTraceTrail _trail;
        private readonly SegmentSelector _selector;
        private readonly Func<int, int> _maxDifficultyForRow;
        private readonly int _rowsPerTier;
        private int _appliedThroughRow;
        // Фиксированная периодическая сетка границ Яруса — начало ТЕКУЩЕГО
        // Яруса (не "докуда реально докатилось содержимое", оно может
        // немного перехлёстывать из-за квантования сегментов по 5-8 рядов;
        // именно так уже вела себя старая _nextCapstoneRow — тот же принцип
        // сохранён, не перепридуман).
        private int _tierStartRow;
        private int _nextCapstoneRow;
        private Landmark _nextLandmark;
        private int _nextLandmarkRow;
        private bool _disposed;

        /// <param name="maxDifficultyForRow">
        /// Верхняя граница сложности сегмента для ряда, с которого он
        /// начинается — прокси "кривой сложности текущего Яруса" (PRD v7
        /// §21) до тех пор, пока в проекте нет Ярусов Глубины (Спринт 8);
        /// внедряется явно, чтобы не создавать зависимость на ещё не
        /// реализованную систему.
        /// </param>
        /// <param name="rowsPerTier">
        /// Правка по итогам ручной проверки владельца (2026-09-04, «Алтари и
        /// вход в Комнату — убрать из случайного пула»): период, с которым
        /// детерминированно ставится связка Алтарь→Алтарь→вход в Комнату
        /// Босса (см. <see cref="EnsureCoveredThrough"/>) — тот же прокси
        /// "границы Яруса", что уже использует <paramref name="maxDifficultyForRow"/>
        /// (на практике оба берут одно значение,
        /// <c>SegmentGenerationController.RowsPerDifficultyStep</c>, но
        /// провайдер не завязан на конкретного вызывающего).
        /// </param>
        public SegmentRowProvider(TunnelGrid grid, GridTraceTrail trail, SegmentSelector selector, Func<int, int> maxDifficultyForRow, int rowsPerTier)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _trail = trail ?? throw new ArgumentNullException(nameof(trail));
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _maxDifficultyForRow = maxDifficultyForRow ?? throw new ArgumentNullException(nameof(maxDifficultyForRow));
            if (rowsPerTier <= 0)
                throw new ArgumentOutOfRangeException(nameof(rowsPerTier), rowsPerTier, "Период границы Яруса должен быть положительным.");
            _rowsPerTier = rowsPerTier;

            _appliedThroughRow = trail.CurrentPosition.Row;
            BeginTier(_appliedThroughRow);
            EnsureCoveredThrough(_appliedThroughRow + RowsAheadOfPlayer);
            _trail.PositionChanged += OnPositionChanged;
        }

        // Задача «Алтари на 1/3 и 2/3 Яруса»: (пере)выставляет фиксированную
        // сетку границ текущего Яруса и цель первого landmark'а (Алтарь #1
        // на 1/3 Яруса) — дистанция от начала Яруса до первого Алтаря
        // прямым текстом НЕ менялась этой задачей (владелец: "она корректна
        // и не завязана на этот баг") — в обычном случае это по-прежнему
        // просто rowsPerTier/3 заполнителей от того же tierStartRow, что и
        // раньше определял капстоун целиком. Math.Max с "не раньше уже
        // применённого содержимого + минимальный зазор" — тот же fallback
        // для коротких/вырожденных Ярусов, что и у переходов Алтарь→Алтарь/
        // Алтарь→Комната ниже (см. doc-комментарий EnsureCoveredThrough):
        // без него короткий предыдущий Ярус мог бы дотянуть содержимое
        // (после собственного fallback'а на границе Алтарь #2→Комната) до
        // точки ПОЗЖЕ номинального начала следующего Яруса — тогда первый
        // Алтарь следующего Яруса слип бы с Комнатой предыдущего вплотную.
        // В обычном (не вырожденном) случае этот Math.Max — не-op: контент
        // никогда не обгоняет фиксированную сетку границ настолько.
        private void BeginTier(int tierStartRow)
        {
            _tierStartRow = tierStartRow;
            _nextCapstoneRow = _tierStartRow + _rowsPerTier;
            _nextLandmark = Landmark.AltarOne;
            _nextLandmarkRow = Math.Max(
                _tierStartRow + _rowsPerTier / 3,
                _appliedThroughRow + 1 + MinRowsBetweenLandmarks);
        }

        /// <summary>Отписывается от трейла. Вызывать при завершении забега/уничтожении системы.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _trail.PositionChanged -= OnPositionChanged;
            _disposed = true;
        }

        private void OnPositionChanged(GridCoordinate coordinate) => EnsureCoveredThrough(coordinate.Row + RowsAheadOfPlayer);

        /// <summary>
        /// Обеспечивает, что все ряды до <paramref name="row"/> включительно
        /// покрыты применённым сегментом — расширяет вперёд целыми
        /// сегментами (см. класс-докстроку).
        ///
        /// <b>Правка по итогам ручной проверки владельца (2026-09-04):</b>
        /// внутри Яруса детерминированно (не лотереей <see cref="SegmentSelector"/>
        /// — PRD v9 требует ровно два Алтаря перед каждой Комнатой Босса
        /// структурой маршрута, не случайным отбором) ставятся три
        /// фиксированных landmark-сегмента —
        /// <see cref="SegmentTemplateCatalog.AltarTemplate"/> ×2 →
        /// <see cref="SegmentTemplateCatalog.BossTemplate"/>. Между ними
        /// по-прежнему <see cref="SegmentSelector"/>.
        ///
        /// <b>Задача «Алтари на 1/3 и 2/3 Яруса» (владелец, Спринт «Стены
        /// вместо ловушек», задача 5):</b> раньше все три landmark'а
        /// ставились ПОДРЯД, вплотную друг к другу, ровно в конце Яруса
        /// (владелец увидел на устройстве: 5–6 плит между вторым Алтарём и
        /// Комнатой — ровно длина одного `AltarTemplate`, то есть НОЛЬ
        /// заполнителя между ними). Теперь: Алтарь #1 на 1/3 Яруса
        /// (<c>_tierStartRow + _rowsPerTier / 3</c>), Алтарь #2 на 2/3
        /// (<c>_tierStartRow + _rowsPerTier * 2 / 3</c>), Комната — в конце
        /// (<c>_nextCapstoneRow</c>, как и раньше). Дистанция от начала
        /// Яруса до Алтаря #1 не менялась — это тот же
        /// <c>_rowsPerTier</c>-заполнитель через <see cref="SegmentSelector"/>,
        /// что раньше вёл к капстоуну целиком, просто короче в 3 раза, не
        /// новая механика.
        ///
        /// <b>Короткие Ярусы — landmark'ы не должны слипаться.</b> Если
        /// <c>_rowsPerTier</c> достаточно мал, что 1/3 и 2/3 попадают в один
        /// и тот же уже применённый сегмент (Алтарь #1 своими 5 рядами сам
        /// перекрывает номинальную отметку 2/3), НИ второй Алтарь, НИ
        /// Комната не пропускаются (PRD v9 требует ровно два Алтаря и вход
        /// в Комнату каждый Ярус безусловно) и не сдвигаются на границу
        /// СЛЕДУЮЩЕГО Яруса (тогда Ярусы начали бы съезжать друг на друга).
        /// Вместо этого следующий landmark сдвигается вперёд относительно
        /// своей номинальной отметки — ровно настолько, чтобы между
        /// предыдущим landmark'ом и им гарантированно поместился хотя бы
        /// один сегмент-заполнитель через <see cref="SegmentSelector"/>
        /// (<see cref="MinRowsBetweenLandmarks"/> — наименьший возможный
        /// размер сегмента, не авторское число баланса). Тот же приём для
        /// пары Алтарь #2 → Комната, и — симметрично, через
        /// <see cref="BeginTier"/> — для перехода Комната этого Яруса →
        /// Алтарь #1 следующего: без него короткий Ярус, чьё содержимое
        /// fallback'ом растянулся ЗА номинальный капстоун, слепил бы свою
        /// Комнату с Алтарём #1 следующего Яруса той же болезнью с другой
        /// стороны границы. Побочный эффект — Ярус в этом вырожденном
        /// случае становится немного ДЛИННЕЕ номинального
        /// <c>_rowsPerTier</c> (сетка НОМИНАЛЬНЫХ границ следующего Яруса не
        /// сжимается вслед за этим, см. <see cref="BeginTier"/> — растягивается
        /// только фактическое содержимое), это уже приемлемо и раньше:
        /// квантование сегментов по 5-8 рядов и без того не даёт капстоуну
        /// приходиться РОВНО на <c>_rowsPerTier</c>.
        /// </summary>
        public void EnsureCoveredThrough(int row)
        {
            while (_appliedThroughRow < row)
            {
                var baseRow = _appliedThroughRow + 1;

                if (baseRow >= _nextLandmarkRow)
                {
                    switch (_nextLandmark)
                    {
                        case Landmark.AltarOne:
                            ApplyTemplate(SegmentTemplateCatalog.AltarTemplate, baseRow);
                            _appliedThroughRow += SegmentTemplateCatalog.AltarTemplate.RowCount;
                            _nextLandmark = Landmark.AltarTwo;
                            _nextLandmarkRow = Math.Max(
                                _tierStartRow + _rowsPerTier * 2 / 3,
                                _appliedThroughRow + 1 + MinRowsBetweenLandmarks);
                            break;

                        case Landmark.AltarTwo:
                            ApplyTemplate(SegmentTemplateCatalog.AltarTemplate, baseRow);
                            _appliedThroughRow += SegmentTemplateCatalog.AltarTemplate.RowCount;
                            _nextLandmark = Landmark.Boss;
                            _nextLandmarkRow = Math.Max(
                                _nextCapstoneRow,
                                _appliedThroughRow + 1 + MinRowsBetweenLandmarks);
                            break;

                        case Landmark.Boss:
                        default:
                            ApplyTemplate(SegmentTemplateCatalog.BossTemplate, baseRow);
                            _appliedThroughRow += SegmentTemplateCatalog.BossTemplate.RowCount;
                            // Следующий Ярус НОМИНАЛЬНО начинается на
                            // фиксированной сетке границ (_nextCapstoneRow до
                            // сброса), тот же принцип, что был у старой
                            // _nextCapstoneRow += _rowsPerTier — сетка сама
                            // не сжимается и не растягивается вслед за
                            // фактическим переразбегом контента. Но
                            // BeginTier всё равно клэмпит фактический старт
                            // Алтаря #1 следующего Яруса тем же Math.Max, что
                            // и остальные переходы ниже — без него короткий
                            // вырожденный Ярус (собственный fallback уже
                            // растянул его содержимое ЗА номинальный
                            // капстоун) слепил бы Комнату этого Яруса с
                            // Алтарём #1 следующего вплотную, той же болезнью
                            // с другой стороны границы.
                            BeginTier(_nextCapstoneRow);
                            break;
                    }

                    continue;
                }

                var template = _selector.SelectNext(_maxDifficultyForRow(baseRow));
                ApplyTemplate(template, baseRow);
                _appliedThroughRow += template.RowCount;
            }
        }

        private void ApplyTemplate(SegmentTemplate template, int baseRow)
        {
            if (template.Width != _grid.Width)
                throw new InvalidOperationException(
                    $"Ширина шаблона '{template.Name}' ({template.Width}) не совпадает с шириной тоннеля ({_grid.Width}).");

            // Заявляем ВСЕ ряды шаблона ДО первого GetOrCreateTile ниже —
            // TunnelGrid.TileMaterialized стреляет синхронно внутри
            // GetOrCreateTile, и если Core.TunnelObstacleGenerator тоже
            // подписан (переходное состояние сосуществования двух
            // генераторов, docs/wiki/roadmap.md), он должен УЖЕ увидеть ряд
            // заявленным в момент своего обработчика — иначе успел бы
            // откликнуться первым и записать в плиту случайный тип раньше,
            // чем этот метод применит настоящий тип шаблона.
            for (var localRow = 0; localRow < template.RowCount; localRow++)
                _grid.ClaimRow(baseRow + localRow);

            // Первый проход — собрать координату рычага и все координаты его
            // ворот (в абсолютных координатах сетки): Tile.MarkLever нужен
            // весь список целей сразу, а расположение Lever/LeverGate в
            // шаблоне заранее не упорядочено относительно друг друга.
            GridCoordinate? leverCoordinate = null;
            var gateTargets = new List<GridCoordinate>();
            for (var localRow = 0; localRow < template.RowCount; localRow++)
            for (var column = 0; column < template.Width; column++)
            {
                var type = template.TileAt(localRow, column);
                if (type == SegmentTileType.Lever) leverCoordinate = new GridCoordinate(baseRow + localRow, column);
                else if (type == SegmentTileType.LeverGate) gateTargets.Add(new GridCoordinate(baseRow + localRow, column));
            }

            for (var localRow = 0; localRow < template.RowCount; localRow++)
            for (var column = 0; column < template.Width; column++)
            {
                var coordinate = new GridCoordinate(baseRow + localRow, column);
                var tile = _grid.GetOrCreateTile(coordinate);
                ApplyTileType(tile, coordinate, template.TileAt(localRow, column), gateTargets, leverCoordinate, template.GateVaultPurchases, template, localRow, column);
            }
        }

        private static void ApplyTileType(Tile tile, GridCoordinate coordinate, SegmentTileType type, List<GridCoordinate> leverGateTargets, GridCoordinate? leverCoordinate, double? gateVaultPurchases, SegmentTemplate template, int localRow, int column)
        {
            switch (type)
            {
                case SegmentTileType.Blocked:
                    tile.MarkBlocked();
                    break;
                case SegmentTileType.Lava:
                    tile.MarkLethalTrap(LethalTrapType.Lava);
                    break;
                case SegmentTileType.Lever:
                    tile.MarkLever(leverGateTargets);
                    break;
                case SegmentTileType.LeverGate:
                    // Issue #193: координата рычага — для подсказки
                    // направления на закрытых воротах (TunnelDebugVisual).
                    // SegmentTemplate.ValidateLeverGates уже гарантирует,
                    // что LeverGate не бывает без Lever в том же шаблоне —
                    // leverCoordinate здесь не null.
                    tile.MarkGated(leverCoordinate);
                    break;
                case SegmentTileType.ManaSource:
                    tile.MarkManaSource();
                    break;
                case SegmentTileType.KeySource:
                    tile.MarkKeySource();
                    break;
                case SegmentTileType.GateVaultKeySource:
                    // SegmentTemplate.ValidateGateVault уже гарантирует, что
                    // gateVaultPurchases не null, когда в шаблоне есть эта
                    // роль — см. её doc-комментарий.
                    tile.MarkKeySource(GateVaultPricing.ComputeVaultKeys(gateVaultPurchases.Value));
                    break;
                case SegmentTileType.Altar:
                    tile.MarkAltar();
                    break;
                case SegmentTileType.Boss:
                    tile.MarkBoss();
                    break;
                case SegmentTileType.ArrowWaveTrigger:
                    // Дефолт "ряд самого триггера/слева-направо" — см.
                    // doc-комментарий SegmentTileType.ArrowWaveTrigger.
                    tile.MarkArrowWaveTrigger(coordinate.Row, RowWaveDirection.LeftToRight);
                    break;
                case SegmentTileType.BombTrigger:
                    tile.MarkBombTrigger();
                    break;
                case SegmentTileType.BladeTactTrigger:
                    tile.MarkBladeTactTrigger(coordinate.Row);
                    break;
                case SegmentTileType.FallingRockTrigger:
                    // Задача «падающий камень: новая спецификация» —
                    // камень падает на плиту ВПЕРЕДИ, дефолт "ряд+1, тот же
                    // столбец" (тот же приём, что Core.TunnelObstacleGenerator.NextRowTarget
                    // уже использовал для ArrowWave/BladeTact).
                    tile.MarkFallingRockTrigger(new GridCoordinate(coordinate.Row + 1, coordinate.Column));
                    break;
                case SegmentTileType.LavaWaveTrigger:
                    tile.MarkLavaTrigger();
                    break;
                case SegmentTileType.Open:
                    ApplyExtraTrapDensity(tile, coordinate, template, localRow, column);
                    break;
                default:
                    break;
            }
        }

        // Задача «параметр плотности» (владелец, 2026-09-08) — см.
        // doc-комментарий ExtraTrapDensity. Проверка > 0f первой строкой —
        // не только отсечка "нечего делать", а гарантия, что при дефолтном
        // (нейтральном) значении UnityEngine.Random вообще не вызывается:
        // EditMode-тесты каталога (SegmentRowProviderTests и другие,
        // ExtraTrapDensity.Chance никогда не трогают) остаются полностью
        // детерминированными.
        //
        // Задача «награда никогда не лежит на ловушке»: ExtraTrapDensity
        // роняет триггер СЛУЧАЙНО на любую Open-плиту шаблона — без проверки
        // он мог бы попасть в область поражения (весь ряд/квадрат Бомбы/
        // ряды назад Лавы, см. RewardTrapConflictValidator) уже существующей
        // в этом же шаблоне награды, даже если сам авторский шаблон целиком
        // безопасен. Прогон по каталогу (2026-09-15, ПОСЛЕ починки всех 22
        // статических конфликтов из 8 шаблонов) нашёл 967 таких потенциально
        // опасных комбинаций (Open-плита × тип триггера) из 988 Open-плит
        // каталога — на два порядка больше уже починенных статических
        // конфликтов, то есть основной риск инварианта был именно
        // рантайм-плотностью, не авторингом. Fallback при обнаруженной
        // угрозе — тихо НЕ ставить триггер (плита остаётся обычной Open) —
        // тот же приём, что уже применяет Core.TunnelObstacleGenerator.
        // CanReserveTarget для похожего риска.
        private static void ApplyExtraTrapDensity(Tile tile, GridCoordinate coordinate, SegmentTemplate template, int localRow, int column)
        {
            var chance = ExtraTrapDensity.Chance;
            if (chance <= 0f) return;
            if (UnityEngine.Random.value >= chance) return;

            // Равновероятный выбор среди всех пяти — дебаг-стресс-тест
            // плотности, не авторский подбор конкретного типа под конкретную
            // плиту (это и есть отличие от авторских шаблонов, которые этот
            // рычаг намеренно дополняет, а не заменяет).
            var candidateType = UnityEngine.Random.Range(0, 5) switch
            {
                0 => SegmentTileType.ArrowWaveTrigger,
                1 => SegmentTileType.BombTrigger,
                2 => SegmentTileType.BladeTactTrigger,
                3 => SegmentTileType.FallingRockTrigger,
                _ => SegmentTileType.LavaWaveTrigger,
            };

            if (RewardTrapConflictValidator.WouldEndangerAnyReward(template, localRow, column, candidateType))
                return; // угроза существующей награде шаблона — плита остаётся обычной, не молчаливая перезапись роли

            switch (candidateType)
            {
                case SegmentTileType.ArrowWaveTrigger:
                    tile.MarkArrowWaveTrigger(coordinate.Row, RowWaveDirection.LeftToRight);
                    break;
                case SegmentTileType.BombTrigger:
                    tile.MarkBombTrigger();
                    break;
                case SegmentTileType.BladeTactTrigger:
                    tile.MarkBladeTactTrigger(coordinate.Row);
                    break;
                case SegmentTileType.FallingRockTrigger:
                    tile.MarkFallingRockTrigger(new GridCoordinate(coordinate.Row + 1, coordinate.Column));
                    break;
                case SegmentTileType.LavaWaveTrigger:
                    tile.MarkLavaTrigger();
                    break;
            }
        }
    }
}
