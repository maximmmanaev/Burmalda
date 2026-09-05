using System;
using System.Collections.Generic;

namespace Burmalda.Core
{
    /// <summary>
    /// Плита сетки тоннеля (PRD 4.1). Хранит координату и состояние распада
    /// (PRD 4.1/16) — само тиканье распада и его тайминг реализует
    /// Burmalda.Decay.TrailDecaySystem, Tile только хранит и применяет результат.
    /// Также хранит статичное препятствие (PRD 4.2, issue #9 — заблокированная
    /// плита, яма/лава) — постоянное, не тикает и не связано с распадом, и
    /// связку триггер→плита-взрыва динамической мгновенной ловушки (PRD 4.2,
    /// issue #10) — в отличие от статичных ловушек, опасность плиты
    /// появляется не сразу, а по факту прохода трейла через её триггер, и
    /// такую же связку для подвижной ловушки с таймингом (PRD v5 4.2, issue
    /// #45) — здесь опасность ещё и временная, включается/выключается по
    /// реальному времени, а не постоянна с момента активации.
    /// Также хранит признак плиты-источника Кристаллов Маны/Ключей (PRD
    /// раздел 5, issue #12) — постоянное состояние, аналогично препятствиям.
    /// Остальной тип плиты (артефакт) добавится сюда же по мере Traps.
    /// </summary>
    public sealed class Tile
    {
        public Tile(GridCoordinate coordinate)
        {
            Coordinate = coordinate;
        }

        public GridCoordinate Coordinate { get; }

        /// <summary>
        /// Задача «двойные флаги на плитах» (плейтест владельца: сигнатура
        /// опасности отрисовывается вместо стены, рычаг/Алтарь иногда
        /// непроходимы, сигнатура вместо источника Маны) — подтверждено
        /// логами/тестом: <c>Generation.SegmentRowProvider</c> и
        /// <c>Core.TunnelObstacleGenerator</c> оба писали в одну и ту же
        /// плиту (см. <c>Generation.Tests.SegmentGenerationCoexistenceTests.
        /// RevealedBeforeClaimed_ObstacleGeneratorWins_TemplateTileTypeSilentlyLost</c>,
        /// заведённый ещё в задаче «партии 1 и 2 + правила отбора» как
        /// диагностика без фикса). Разграничение по рядам (см.
        /// <c>Bootstrap.RunBootstrap</c>) закрывает ПРИЧИНУ — этот метод
        /// закрывает СЛЕДСТВИЕ на случай, если причина всё же случится
        /// снова (регрессия, новый генератор, ручной тест): плита не может
        /// одновременно нести две из перечисленных ниже ролей — попытка
        /// зовёт <see cref="InvalidOperationException"/>, а не молча
        /// перезаписывает. Core не ссылается на UnityEngine
        /// (<c>noEngineReferences</c> в Burmalda.Core.asmdef) — необработанное
        /// исключение Unity сам громко логирует в консоль в Editor/dev-сборке,
        /// отдельный Debug.LogError не нужен и был бы недоступен отсюда.
        /// Повторная пометка ТОЙ ЖЕ роли (что и раньше) остаётся тихим
        /// не-op — конфликт только между РАЗНЫМИ ролями.
        ///
        /// <b>Разделение фаз (владелец, 2026-09-01 — предыдущая формулировка
        /// без разделения фаз была дефектом постановки, не шаблонов
        /// контента):</b> этот страж — ТОЛЬКО про ГЕНЕРАЦИЮ ("плита получает
        /// ровно одну роль при постройке тоннеля, вторая попытка — ошибка
        /// авторинга/гонки генераторов"). Он НЕ применяется к рантайм-
        /// переходам состояния по ходу забега (взрыв делает плиту смертельной,
        /// будущие Лава-волна/Падающий камень, ворота открываются, плита
        /// разрушается распадом, docs/wiki/traps.md) — те идут через
        /// отдельный явный API (см. <see cref="TransitionToLethalTrap"/>,
        /// <see cref="TransitionToBlocked"/>, <see cref="OpenLeverGate"/>,
        /// <see cref="AdvanceDecay"/>), который сознательно не вызывает этот
        /// метод. Список рантайм-переходов растёт вместе с игрой — не
        /// перечислением исключений здесь, а новым явным методом на каждый
        /// случай, как уже сделано для этих пяти.
        /// </summary>
        private void GuardAgainstConflictingRole(bool alreadyThisRole, string incomingRole)
        {
            if (alreadyThisRole) return;

            var existing = ActiveExclusiveRoleName();
            if (existing == null) return;

            throw new InvalidOperationException(
                $"Tile {Coordinate}: попытка пометить роль '{incomingRole}', но плита уже несёт взаимоисключающую роль '{existing}' — два генератора записали в одну плиту (см. docs/wiki/changelog.md, задача «двойные флаги на плитах»).");
        }

        private string ActiveExclusiveRoleName()
        {
            if (IsBlocked) return nameof(IsBlocked);
            if (LethalTrap.HasValue) return $"{nameof(LethalTrap)}={LethalTrap.Value}";
            if (IsManaSource) return nameof(IsManaSource);
            if (IsKeySource) return nameof(IsKeySource);
            if (IsAltar) return nameof(IsAltar);
            if (IsBoss) return nameof(IsBoss);
            if (IsLever) return nameof(IsLever);
            if (IsGated) return nameof(IsGated);
            return null;
        }

        /// <summary>Плита разрушена распадом — по ней больше нельзя пройти.</summary>
        public bool IsDestroyed { get; private set; }

        /// <summary>
        /// Плита — статичное препятствие (PRD 4.2: заблокированная плита,
        /// видна заранее, обходится). Постоянное состояние, не связано с
        /// распадом — заблокированная плита никогда не была пройдена, значит
        /// и не может начать распадаться.
        /// </summary>
        public bool IsBlocked { get; private set; }

        /// <summary>Помечает плиту как непроходимое статичное препятствие. Повторные вызовы — не-op.</summary>
        public void MarkBlocked()
        {
            GuardAgainstConflictingRole(IsBlocked, nameof(IsBlocked));
            IsBlocked = true;
        }

        /// <summary>
        /// РАНТАЙМ-переход, симметричный <see cref="TransitionToLethalTrap"/>:
        /// плита становится непроходимой ПРЯМО СЕЙЧАС, по ходу забега —
        /// сознательно БЕЗ <see cref="GuardAgainstConflictingRole"/> (см. её
        /// doc-комментарий). Введён для ловушки «Падающий камень»
        /// (docs/wiki/traps.md, issue #217, <c>Movement.FallingRockTrapSystem</c>):
        /// единственная ловушка, необратимо меняющая геометрию маршрута —
        /// камень падает на плиту-триггер и остаётся там навсегда, как
        /// стена (<see cref="IsBlocked"/>), но появляется ВО ВРЕМЯ забега, а
        /// не на этапе генерации, как <see cref="MarkBlocked"/>.
        /// </summary>
        public void TransitionToBlocked()
        {
            IsBlocked = true;
        }

        /// <summary>
        /// Плита — смертельная статичная ловушка (яма/лава, PRD 4.2). Null —
        /// плита не является такой ловушкой.
        /// </summary>
        public LethalTrapType? LethalTrap { get; private set; }

        /// <summary>
        /// ГЕНЕРАЦИЯ: помечает плиту как статичную смертельную ловушку
        /// (яма/лава) заданного типа. Повторные вызовы сохраняют первый тип.
        /// Строгий страж — см. <see cref="GuardAgainstConflictingRole"/>. Для
        /// рантайм-перехода (плита становится смертельной ПО ХОДУ ЗАБЕГА,
        /// напр. сработавший взрыв) — см. <see cref="TransitionToLethalTrap"/>,
        /// не этот метод.
        /// </summary>
        public void MarkLethalTrap(LethalTrapType trapType)
        {
            if (LethalTrap.HasValue) return;
            GuardAgainstConflictingRole(false, nameof(LethalTrap));
            LethalTrap = trapType;
        }

        /// <summary>
        /// РАНТАЙМ-переход (владелец, задача «две задачи — спецификация
        /// ловушек и разрушение плиты», продолжение 2026-09-01): плита
        /// становится смертельной ловушкой заданного типа ПРЯМО СЕЙЧАС, по
        /// ходу забега — поверх ЛЮБОЙ прежней роли, сознательно БЕЗ
        /// <see cref="GuardAgainstConflictingRole"/>. В отличие от
        /// <see cref="MarkLethalTrap"/> (генерация, строгий страж "два
        /// генератора не пишут в одну плиту при постройке тоннеля") — это
        /// реальный игровой момент, вызванный действием игрока/ходом
        /// времени, не авторская ошибка. Явный пример из задачи: шаблоны
        /// «выкуп»/«последний-рывок» (<c>Generation.SegmentTemplateCatalog</c>)
        /// намеренно ставят взрывной триггер над <see cref="IsManaSource"/>/
        /// <see cref="IsKeySource"/> — "триггер уничтожает собственную
        /// награду" — срабатывание обязано превратить плиту в смертельную,
        /// не бросить исключение.
        ///
        /// Используется всеми пятью ловушками Спринта 13a (docs/wiki/traps.md,
        /// <c>Movement.ArrowWaveTrapSystem</c>/<c>BombTrapSystem</c>/
        /// <c>BladeTactTrapSystem</c>/<c>LavaWaveTrapSystem</c>) — список
        /// рантайм-переходов растёт РАЗДЕЛЕНИЕМ ФАЗ (генерация/рантайм), а не
        /// перечислением исключений в страже, см. его doc-комментарий.
        /// </summary>
        public void TransitionToLethalTrap(LethalTrapType trapType)
        {
            LethalTrap = trapType;
        }

        /// <summary>
        /// РАНТАЙМ-переход, симметричный <see cref="TransitionToLethalTrap"/>:
        /// снимает опасность, поставленную им же — плита возвращается к
        /// обычному полу. Введён для ловушки «Стрела» (docs/wiki/traps.md,
        /// issue #213, <c>Movement.ArrowWaveTrapSystem</c>): «каждая плита
        /// опасна короткий момент, пока волна проходит, затем снова
        /// безопасна» — в отличие от <see cref="LethalTrapType.Lava"/>
        /// (постоянна с генерации) и <see cref="LethalTrapType.LavaWave"/>
        /// (постоянна с момента активации), опасность волны Стрелы
        /// временная НА КАЖДОЙ отдельной плите ряда.
        ///
        /// Вызывать ТОЛЬКО на плите, которую сама вызывающая система же и
        /// сделала смертельной через <see cref="TransitionToLethalTrap"/> в
        /// рамках этой же волны — метод не проверяет источник и снимет
        /// ЛЮБОЙ <see cref="LethalTrap"/>, включая статичный
        /// <see cref="MarkLethalTrap"/> с генерации, если вызвать не на той
        /// плите (дисциплина вызывающей стороны, не защита этим методом).
        /// </summary>
        public void ClearLethalTrap()
        {
            LethalTrap = null;
        }

        /// <summary>
        /// Плита — триггер ловушки «Стрела» (docs/wiki/traps.md, issue #213):
        /// номер ряда, который через 1 ход начнёт становиться опасным
        /// столбец за столбцом (см. <see cref="ArrowWaveDirection"/>,
        /// <c>Movement.ArrowWaveTrapSystem</c>). Null — плита не триггер.
        /// Открытый вопрос владельца («в ряду триггера или в следующем?») не
        /// решён — этот класс хранит ЯВНО заданный ряд, а не вычисляет его
        /// из <see cref="Coordinate"/> самого триггера, поэтому не зависит
        /// от того, как вопрос решится: вызывающая сторона (генерация)
        /// передаёт номер ряда сама, по умолчанию — ряд самого триггера
        /// (docs/wiki/traps.md). Сама плита-триггер всегда безопасна для
        /// прохода — опасна плиты заявленного ряда, не она сама.
        /// </summary>
        public int? ArrowWaveTargetRow { get; private set; }

        /// <summary>Направление волны — актуально только вместе с <see cref="ArrowWaveTargetRow"/>. Задаётся на генерации, не выбирается заново при активации (см. <see cref="RowWaveDirection"/>).</summary>
        public RowWaveDirection? ArrowWaveDirection { get; private set; }

        /// <summary>Помечает плиту как триггер волны Стрелы по заданному ряду и направлению. Повторные вызовы сохраняют первые значения.</summary>
        public void MarkArrowWaveTrigger(int targetRow, RowWaveDirection direction)
        {
            if (ArrowWaveTargetRow.HasValue) return;
            ArrowWaveTargetRow = targetRow;
            ArrowWaveDirection = direction;
        }

        /// <summary>
        /// Плита — триггер ловушки «Бомба» (docs/wiki/traps.md, issue #214):
        /// в отличие от <see cref="ArrowWaveTargetRow"/> не хранит отдельную
        /// координату цели — площадь взрыва (радиус <c>Movement.BombTrapSystem.RadiusTiles</c>)
        /// вычисляется вокруг координаты САМОЙ этой плиты (владелец: «восемь
        /// соседей плюс сама плита-триггер», взрыв центрирован на триггере,
        /// не смещён на отдельную цель). Сама плита-триггер входит в площадь
        /// взрыва — в отличие от прочих триггеров она НЕ остаётся безопасной.
        /// </summary>
        public bool IsBombTrigger { get; private set; }

        /// <summary>Помечает плиту как триггер Бомбы. Повторные вызовы — не-op.</summary>
        public void MarkBombTrigger()
        {
            IsBombTrigger = true;
        }

        /// <summary>
        /// Плита — триггер ловушки «Лезвия» (docs/wiki/traps.md, issue
        /// #215): номер ряда, который через несколько ходов начнёт
        /// становиться опасным по пятитактовому паттерну (см.
        /// <c>Movement.BladeTactTrapSystem</c>). Null — плита не триггер.
        /// Тот же приём, что у <see cref="ArrowWaveTargetRow"/> — открытый
        /// вопрос владельца («ряд триггера или следующий?») не решён, класс
        /// хранит явно заданный ряд, не вычисляет его из <see cref="Coordinate"/>
        /// самого триггера.
        /// </summary>
        public int? BladeTactTargetRow { get; private set; }

        /// <summary>Помечает плиту как триггер такта Лезвий по заданному ряду. Повторные вызовы сохраняют первое значение.</summary>
        public void MarkBladeTactTrigger(int targetRow)
        {
            if (BladeTactTargetRow.HasValue) return;
            BladeTactTargetRow = targetRow;
        }

        /// <summary>
        /// Плита — триггер ловушки «Падающий камень» (docs/wiki/traps.md,
        /// issue #217): в отличие от прочих триггеров не хранит отдельную
        /// координату/ряд цели — камень падает на САМУ эту плиту (владелец:
        /// «на плиту-триггер падает камень»), см. <c>Movement.FallingRockTrapSystem</c>.
        /// </summary>
        public bool IsFallingRockTrigger { get; private set; }

        /// <summary>Помечает плиту как триггер Падающего камня. Повторные вызовы — не-op.</summary>
        public void MarkFallingRockTrigger()
        {
            IsFallingRockTrigger = true;
        }

        /// <summary>
        /// Плита — триггер ловушки «Лава» (docs/wiki/traps.md, issue #216):
        /// сама эта плита — начало волны, отдельной координаты/ряда цели не
        /// хранит (владелец: «волна идёт от ряда триггера назад» — ряд
        /// вычисляется из <see cref="Coordinate"/> самого триггера, не
        /// задаётся отдельно, в отличие от <see cref="ArrowWaveTargetRow"/>/
        /// <see cref="BladeTactTargetRow"/>), см. <c>Movement.LavaWaveTrapSystem</c>.
        /// </summary>
        public bool IsLavaTrigger { get; private set; }

        /// <summary>Помечает плиту как триггер волны Лавы. Повторные вызовы — не-op.</summary>
        public void MarkLavaTrigger()
        {
            IsLavaTrigger = true;
        }

        /// <summary>
        /// Задача «раскрытие опасности при примеривании» (PRD v9 §4.2
        /// заменяется — сигнатура опасности видна не всегда, а только после
        /// примеривания): истинно, если игрок хотя бы раз навёл на эту
        /// плиту (<c>Movement.TrapRevealSystem</c>) и её скрытая опасность
        /// (триггер одной из пяти ловушек — Стрела/Бомба/Лезвия/Падающий
        /// камень/Лава, см. <c>Movement.TrapRevealSystem.HasHiddenDanger</c>)
        /// была раскрыта. НЕ про точный тип — это по-прежнему
        /// <c>Movement.TrapInsight.HasTrapTypeInsight</c> (Идол Чутья), а про
        /// сам факт "здесь что-то не так". Раз раскрыто — навсегда до конца
        /// забега (нет метода, который бы это снимал) — разведка не должна
        /// превращаться в проверку памяти. Статичная Лава/уже активная
        /// (сработавшая) ловушка сюда не относятся — они видимы всегда, вне
        /// зависимости от этого флага.
        /// </summary>
        public bool IsDangerSignatureRevealed { get; private set; }

        /// <summary>Раскрывает сигнатуру опасности этой плиты — см. <see cref="IsDangerSignatureRevealed"/>. Повторные вызовы — не-op.</summary>
        public void RevealDangerSignature()
        {
            IsDangerSignatureRevealed = true;
        }

        /// <summary>
        /// Плита — рычаг (PRD 4.2, раздел 21, issue #51): не ловушка, не
        /// наносит вреда. Активация (проход трейла через эту плиту) должна
        /// открыть все плиты <see cref="LeverGateTargets"/> — см.
        /// <c>Burmalda.Generation.LeverActivationSystem</c>.
        /// </summary>
        public bool IsLever { get; private set; }

        /// <summary>Координаты плит короткого бокового прохода, которые открывает этот рычаг. Null, пока не помечена рычагом.</summary>
        public IReadOnlyList<GridCoordinate> LeverGateTargets { get; private set; }

        /// <summary>Помечает плиту как рычаг с заданным набором целей-ворот. Повторные вызовы сохраняют первый набор.</summary>
        public void MarkLever(IReadOnlyList<GridCoordinate> gateTargets)
        {
            if (IsLever) return;
            GuardAgainstConflictingRole(false, nameof(IsLever));
            IsLever = true;
            LeverGateTargets = gateTargets;
        }

        /// <summary>
        /// Плита — часть бокового прохода, закрытого до активации связанного
        /// рычага (issue #51). В отличие от <see cref="IsBlocked"/>
        /// (постоянно непроходима), становится проходимой после
        /// <see cref="OpenLeverGate"/>.
        /// </summary>
        public bool IsGated { get; private set; }

        /// <summary>Плита-ворота открыта — рычаг уже активирован.</summary>
        public bool IsLeverGateOpen { get; private set; }

        /// <summary>
        /// Координата рычага, который открывает ЭТИ ворота (issue #193):
        /// "подсветка Ворот указывает направление на открывашку" — рычаг
        /// теперь скрыт (та же сигнатура опасности, что и ловушки, см.
        /// <see cref="IsDangerSignatureRevealed"/>/<see cref="TrapInsight"/>
        /// в вызывающем коде), без подсказки его искали бы наугад, а
        /// каждое примеривание стоит игроку распада — подсказка делает
        /// поиск ограниченным. Null, пока плита не помечена воротами.
        /// </summary>
        public GridCoordinate? LeverCoordinate { get; private set; }

        /// <summary>
        /// Помечает плиту как закрытые ворота бокового прохода (закрыта по
        /// умолчанию). <paramref name="leverCoordinate"/> — опционально
        /// (default null, обратная совместимость с существующими вызовами/
        /// тестами, которым координата рычага не нужна) — задаёт
        /// <see cref="LeverCoordinate"/> для подсказки направления.
        /// </summary>
        public void MarkGated(GridCoordinate? leverCoordinate = null)
        {
            GuardAgainstConflictingRole(IsGated, nameof(IsGated));
            IsGated = true;
            LeverCoordinate = leverCoordinate;
        }

        /// <summary>Открывает ворота бокового прохода — вызывается системой активации рычага.</summary>
        public void OpenLeverGate()
        {
            IsLeverGateOpen = true;
        }

        /// <summary>
        /// Плита — источник Кристаллов Маны (PRD раздел 5, issue #12):
        /// "плитки-источники маны на пути". Сбор — см.
        /// <c>Burmalda.Currencies.TrailTileCurrencySystem</c>.
        /// </summary>
        public bool IsManaSource { get; private set; }

        /// <summary>Помечает плиту как источник Кристаллов Маны. Повторные вызовы — не-op.</summary>
        public void MarkManaSource()
        {
            GuardAgainstConflictingRole(IsManaSource, nameof(IsManaSource));
            IsManaSource = true;
        }

        /// <summary>
        /// Плита — источник Ключей (PRD раздел 5, issue #12): "отдельные
        /// плитки-источники ключей на пути".
        /// </summary>
        public bool IsKeySource { get; private set; }

        /// <summary>
        /// Явно заданное число Ключей ЭТОЙ конкретной плиты-источника, если
        /// задано (задача «видимые рычаги, инвариант лавы, размер награды
        /// за Воротами», владелец, 2026-09-04) — тайник за Воротами получает
        /// свою сумму, вычисленную из <c>Generation.GateVaultPricing</c> по
        /// числу покупок конкретного шаблона, а не общий
        /// <c>Currencies.CurrencyController.KeysPerSource</c>. Null — плита
        /// обычный источник на пути, сумму решает вызывающая сторона
        /// (<c>Currencies.TrailTileCurrencySystem</c>) по умолчанию.
        /// </summary>
        public int? KeySourceAmount { get; private set; }

        /// <summary>
        /// Помечает плиту как источник Ключей. Повторные вызовы сохраняют
        /// первую сумму — тихий не-op, как и у прочих <c>Mark*</c>.
        /// <paramref name="amount"/> — явная сумма ЭТОЙ плиты (см.
        /// <see cref="KeySourceAmount"/>); null (по умолчанию) — обычный
        /// источник на пути, сумму решает вызывающая сторона.
        /// </summary>
        public void MarkKeySource(int? amount = null)
        {
            if (IsKeySource) return;
            GuardAgainstConflictingRole(false, nameof(IsKeySource));
            IsKeySource = true;
            KeySourceAmount = amount;
        }

        /// <summary>
        /// Плита — клетка-Алтарь (PRD раздел 7, issue #19): достижение
        /// запускает Ритуал. Не ловушка, всегда проходима.
        /// </summary>
        public bool IsAltar { get; private set; }

        /// <summary>Помечает плиту как Алтарь. Повторные вызовы — не-op.</summary>
        public void MarkAltar()
        {
            GuardAgainstConflictingRole(IsAltar, nameof(IsAltar));
            IsAltar = true;
        }

        /// <summary>
        /// Плита — точка встречи с Боссом (PRD раздел 8, issue #22):
        /// обязательная, без победы дальше не пройти. Не ловушка, всегда
        /// проходима — встреча разрешается автоматически при достижении.
        /// </summary>
        public bool IsBoss { get; private set; }

        /// <summary>Помечает плиту как точку Босса. Повторные вызовы — не-op.</summary>
        public void MarkBoss()
        {
            GuardAgainstConflictingRole(IsBoss, nameof(IsBoss));
            IsBoss = true;
        }

        /// <summary>
        /// Плита существует только внутри Комнаты Босса (PRD v8 §8.2) — null
        /// вне Комнаты. Аналогично <see cref="LethalTrap"/>: постоянное
        /// состояние плиты, не связано с распадом.
        /// </summary>
        public BossRoomTileKind? BossRoomTile { get; private set; }

        /// <summary>
        /// Осмысленно только когда <see cref="BossRoomTile"/> ==
        /// <see cref="BossRoomTileKind.Rift"/> — какое тиканье даёт эта
        /// плита-Разлом, пока на ней стоишь (PRD v8 §8.2).
        /// </summary>
        public BossRoomRiftSubtype? RiftSubtype { get; private set; }

        /// <summary>Помечает плиту как Жилу/Резонанс Комнаты. Повторные вызовы — не-op.</summary>
        public void MarkBossRoomTile(BossRoomTileKind kind)
        {
            if (BossRoomTile.HasValue) return;
            BossRoomTile = kind;
        }

        /// <summary>Помечает плиту как Разлом Комнаты нужного подтипа. Повторные вызовы — не-op.</summary>
        public void MarkBossRoomRift(BossRoomRiftSubtype subtype)
        {
            if (BossRoomTile.HasValue) return;
            BossRoomTile = BossRoomTileKind.Rift;
            RiftSubtype = subtype;
        }

        /// <summary>Накопленное время распада плиты в секундах.</summary>
        public float DecayElapsedSeconds { get; private set; }

        /// <summary>
        /// Порог распада в секундах для этой плиты. Задаётся один раз, когда
        /// плита начинает распадаться (см. <see cref="BeginDecay"/>); null —
        /// плита распаду не подвержена (например, стартовая плита трейла).
        /// </summary>
        public float? DecayThresholdSeconds { get; private set; }

        /// <summary>
        /// Доля пройденного времени распада, 0..1 (для визуальной обратной
        /// связи — напр. цвет плиты, как в legacy/burmolda_demo.html, draw()).
        /// 0, пока распад не начат.
        /// </summary>
        public float DecayProgress01
        {
            get
            {
                if (!DecayThresholdSeconds.HasValue || DecayThresholdSeconds.Value <= 0f) return 0f;
                var ratio = DecayElapsedSeconds / DecayThresholdSeconds.Value;
                if (ratio < 0f) return 0f;
                return ratio > 1f ? 1f : ratio;
            }
        }

        /// <summary>Запускает распад плиты с заданным порогом. Повторные вызовы игнорируются.</summary>
        public void BeginDecay(float thresholdSeconds)
        {
            if (DecayThresholdSeconds.HasValue) return;
            DecayThresholdSeconds = thresholdSeconds;
        }

        /// <summary>
        /// Продвигает накопленное время распада на <paramref name="deltaSeconds"/>;
        /// разрушает плиту при достижении порога. Не действует, пока распад не
        /// начат (<see cref="BeginDecay"/>) и после того, как плита уже разрушена.
        /// </summary>
        public void AdvanceDecay(float deltaSeconds)
        {
            if (IsDestroyed || !DecayThresholdSeconds.HasValue || deltaSeconds <= 0f) return;

            DecayElapsedSeconds += deltaSeconds;
            if (DecayElapsedSeconds >= DecayThresholdSeconds.Value)
            {
                IsDestroyed = true;
            }
        }
    }
}
