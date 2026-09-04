using Burmalda.Altar;
using Burmalda.Artifacts;
using Burmalda.Boss;
using Burmalda.BossRoom;
using Burmalda.Camp;
using Burmalda.Currencies;
using Burmalda.Generation;
using Burmalda.Movement;
using Burmalda.Progression;
using UnityEngine;

namespace Burmalda.Bootstrap
{
    /// <summary>
    /// Единственная точка сборки игровых систем забега (задача "композиционный
    /// корень RunBootstrap"). За две недели одна и та же ситуация повторилась
    /// четыре раза: система написана, покрыта тестами — и никем не создаётся
    /// в реальной игре, потому что в проекте не было места, которое явно
    /// собирает системы забега в определённом порядке. Каждый из
    /// <see cref="Currencies.CurrencyController"/>/<see cref="Altar.AltarController"/>/
    /// <see cref="Boss.BossController"/>/<see cref="Camp.CampController"/>/
    /// <see cref="Generation.LeverActivationController"/> уже полностью
    /// рабочий и покрыт тестами сам по себе — этот класс их не переписывает,
    /// только создаёт и связывает.
    ///
    /// <b>Самобутстрап — тем же способом, что и остальное</b> (docs/rules/forbidden-actions.md:
    /// .unity/.prefab не трогаем): единственный компонент проекта, которому
    /// разрешено самобутстрапиться и при этом собирать ИГРОВЫЕ системы — все
    /// остальные game-контроллеры (<see cref="CurrencyController"/> и т.д.)
    /// сами больше не самобутстрапятся, только этот класс явно раскладывает
    /// их по GameObject'у <see cref="GridTraceInputController"/> компонентами
    /// в фиксированном порядке. Раньше у каждой системы был свой отдельный
    /// bootstrap-хак (см. удалённый <c>DebugVisuals.CurrencyControllerBootstrap</c>) —
    /// теперь один явный композиционный корень вместо N скрытых.
    ///
    /// <b>Порядок AddComponent — не косметика, а жёсткая зависимость.</b>
    /// Каждый контроллер читает соседей через <c>GetComponent&lt;T&gt;()`
    /// РОВНО ОДИН РАЗ в своём <c>Awake()</c> (без повторной попытки в
    /// <c>Update()</c>), а <c>GameObject.AddComponent&lt;T&gt;()</c> вызывает
    /// <c>Awake()</c> нового компонента СИНХРОННО в момент добавления. Если
    /// добавить <see cref="AltarController"/> раньше <see cref="CurrencyController"/>,
    /// её приватное поле `_currency` останется null навсегда — не разово,
    /// не до следующего кадра. Порядок здесь ровно тот, что уже
    /// зарекомендовал себя в <c>IntegrationTests.CoreLoopIntegrationTests</c>
    /// (эталон конструирования, см. задачу): генерация сегментов (ни от кого
    /// из перечисленных не зависит, только от уже существующего
    /// <see cref="GridTraceInputController"/>) → валюты → Алтарь (пул/коллекция
    /// нужны и лоадауту, и Боссу) → Босс (Ярус Глубины) → Лагерь → рычаги.
    ///
    /// <b>Что уже было на сцене и не требует вмешательства</b> (проверено по
    /// GUID компонента в YAML сцены, не по имени класса — тот же метод, что
    /// нашёл пробел с CurrencyController в задаче 2): <see cref="GridTraceInputController"/>,
    /// <c>Decay.TrailDecayController</c>, <c>RunLifecycle.RunController</c>,
    /// <c>Movement.TunnelCameraController</c>, <c>Movement.ExplosiveTrapController</c>,
    /// <c>Movement.TimedTrapController</c>, <c>Movement.TunnelObstacleController</c> —
    /// все уже реально размещены и работают, RunBootstrap их не трогает.
    ///
    /// <b>Генерация сегментов подключена наряду с уже размещённым на сцене
    /// TunnelObstacleController (переходное состояние, docs/wiki/roadmap.md).</b>
    /// Раньше здесь была задокументирована сознательная блокировка: собственный
    /// doc-комментарий <see cref="Generation.SegmentGenerationController"/>
    /// предупреждал, что он "конкурирует за одни и те же плиты" с уже
    /// размещённым на сцене <c>Movement.TunnelObstacleController</c>.
    /// Разграничение по рядам (<c>Core.TunnelGrid.ClaimRow</c>) само по себе
    /// корректно, но долго опиралось на ПОРЯДОК, а не на конструкцию: оба
    /// контроллера независимо подписывались на <c>RunStarted</c>, и чей
    /// обработчик сработает раньше — не было гарантировано (плейтест
    /// владельца, задача «двойные флаги на плитах», нашёл плиты сразу с
    /// несколькими взаимоисключающими ролями — <c>Core.TunnelObstacleGenerator</c>
    /// засевал плиту раньше, чем <c>Generation.SegmentRowProvider</c> успевал
    /// её заявить). Теперь порядок задан явно и синхронно этим классом:
    /// <see cref="EnsureControllersWired"/> сначала форсирует первую сборку
    /// <see cref="Segments"/> (<c>Generation.SegmentGenerationController.EnsureBuilt</c>),
    /// затем — <c>Movement.TunnelObstacleController.EnsureBuilt</c> (см.
    /// <c>SyncLegacyObstacleGenerator</c>); на каждом рестарте забега то же
    /// самое повторяет <see cref="HandleRunStarted"/>, уже как безусловную
    /// пересборку. <c>TunnelObstacleController</c> остаётся на сцене и
    /// продолжает засеивать ряды, до которых сегменты ещё не дошли — уберёт
    /// его владелец продукта вручную (.unity не трогаем), когда каталог
    /// шаблонов покроет тоннель целиком (см. docs/wiki/roadmap.md).
    ///
    /// <b>Артефактный лоадаут забега</b> (<see cref="Loadout"/>) — по своей же
    /// доккомментарии <see cref="RunArtifactLoadout"/>, "новый экземпляр
    /// создаётся заново на каждый забег" — пересобирается вместе с остальными
    /// run-системами, читая постоянную <see cref="AltarController.Collection"/>.
    /// Ярус Глубины отдельного экземпляра не имеет — <see cref="DepthTier"/>
    /// просто читает <see cref="BossController.DepthTier"/> (уже публичное
    /// свойство, никакого нового API не потребовалось).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunBootstrap : MonoBehaviour
    {
        /// <summary>Единственный живой экземпляр — потребители (HUD и т.п.) читают отсюда, не создают свои копии систем.</summary>
        public static RunBootstrap Instance { get; private set; }

        private GridTraceInputController _input;
        private bool _controllersWired;
        private bool _subscribedToRunStarted;

        /// <summary>Контроллер ввода/трейла текущего забега — тот же, что уже был на сцене.</summary>
        public GridTraceInputController Input => _input;

        /// <summary>
        /// Целевая генерация содержимого тоннеля целыми сегментами (PRD v7
        /// §21) — сосуществует с уже размещённым на сцене
        /// <c>Movement.TunnelObstacleController</c> по разграничению рядов
        /// (переходное состояние, docs/wiki/roadmap.md), ни от кого из
        /// остальных контроллеров этого класса не зависит.
        /// </summary>
        public SegmentGenerationController Segments { get; private set; }

        /// <summary>Интеграция валют — от неё зависят Алтарь/Босс/Лагерь.</summary>
        public CurrencyController Currency { get; private set; }

        /// <summary>Интеграция Алтаря/Ритуала — постоянные Пул/Коллекция нужны и лоадауту, и первой победе над Боссом.</summary>
        public AltarController Altar { get; private set; }

        /// <summary>Интеграция Босса — владеет живым Ярусом Глубины забега, см. <see cref="DepthTier"/>.</summary>
        public BossController Boss { get; private set; }

        /// <summary>Интеграция Лагеря/Возврата/Кэш-аута.</summary>
        public CampController Camp { get; private set; }

        /// <summary>Интеграция рычагов — секретные боковые проходы к артефактам (issue #51).</summary>
        public LeverActivationController Lever { get; private set; }

        /// <summary>
        /// Тикает пять ловушек Спринта 13a (issues #213–#217) — баг с
        /// устройства (владелец, 2026-09-04, «новых ловушек в игре нет»):
        /// системы были написаны и протестированы, но без этого Controller'а
        /// (и без символов шаблона, отдельный фикс) никогда не тикали в
        /// реальном забеге. Независим от остальных — как и <see cref="Lever"/>.
        /// </summary>
        public TurnBasedTrapSystemsController TurnBasedTraps { get; private set; }

        /// <summary>
        /// Интеграция Комнаты Босса (вертикальный срез, задача «Комната
        /// Босса», владелец, 2026-09-02) — вход по <c>Core.Tile.IsBoss</c>,
        /// генерация содержимого, сбор Жилы/Резонанса/Эха, наступающая
        /// волна, выход с начислением Кристаллов Маны. Зависит от
        /// <see cref="Currency"/> (начисление счёта) и уже размещённого на
        /// сцене <c>RunLifecycle.RunController</c> (сообщить о поражении от
        /// волны) — оба должны существовать к моменту добавления.
        /// </summary>
        public BossRoomController BossRoom { get; private set; }

        /// <summary>
        /// Билд Амулетов/Талисманов текущего забега — новый экземпляр на
        /// каждый забег (см. <see cref="RunArtifactLoadout"/>). Null, пока
        /// <see cref="Altar"/> ещё не создал постоянную Коллекцию.
        /// </summary>
        public RunArtifactLoadout Loadout { get; private set; }

        /// <summary>
        /// Живой Ярус Глубины текущего забега — делегирует к
        /// <see cref="BossController.DepthTier"/>, отдельного экземпляра
        /// здесь нет (не нужен: у Босса это уже публичное свойство). Null,
        /// пока <see cref="Boss"/> не пересобрал свои run-системы (см.
        /// <c>BossController.RebuildEncounter</c>) — потребители обязаны
        /// показывать прочерк на null, не выдуманный ноль.
        /// </summary>
        public RunDepthTier DepthTier => Boss != null ? Boss.DepthTier : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(RunBootstrap));
            host.AddComponent<RunBootstrap>();
            DontDestroyOnLoad(host);
        }

        private void Awake()
        {
            Instance = this;
            // Задача «двойные флаги на плитах»: попытка синхронной сборки
            // прямо здесь, а не только в ленивом Update() ниже — этот
            // GameObject создаётся через RuntimeInitializeLoadType.AfterSceneLoad
            // (см. Bootstrap()), а это гарантированно ПОСЛЕ Awake/OnEnable
            // всех объектов сцены (включая GridTraceInputController) и
            // строго ДО первого Update() любого компонента в этом кадре —
            // единственное надёжное окно, чтобы SyncLegacyObstacleGenerator
            // ниже успел отработать раньше, чем Movement.TunnelObstacleController
            // получит свой собственный первый Update()-тик.
            TrySync();
        }

        private void OnDestroy()
        {
            if (_input != null) _input.RunStarted -= HandleRunStarted;
            if (Instance == this) Instance = null;
        }

        private void Update() => TrySync();

        private void TrySync()
        {
            // Ленивый поиск — тот же принцип, что у остальных debug/game
            // driver'ов проекта: порядок Awake между объектами сцены не
            // гарантирован, GridTraceInputController может ещё не
            // существовать в момент, когда этот компонент запускается.
            // Всё ниже идемпотентно — безопасно вызывать и из Awake(), и
            // из Update() на каждом кадре до полной сборки.
            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
            if (_input == null) return;

            EnsureControllersWired();
            EnsureRunStartedSubscription();
            EnsureLoadoutReady(); // тот же паттерн, что у Decay/Altar/Boss: ловим самый первый RunStarted, поднятый GridTraceInputController.Awake() ДО того, как этот компонент вообще появился
        }

        // Явный порядок (см. doc-комментарий класса) — генерация сегментов
        // первой (ни от кого из перечисленных не зависит), затем валюты (ни
        // от кого не зависят, кроме уже существующего GridTraceInputController),
        // затем Алтарь (нужен Currency), затем Босс и Лагерь (нужны и
        // Currency, и Altar, и уже существующий на сцене RunController),
        // рычаги — независимы, могут быть где угодно, оставлены последними
        // по списку задачи.
        private void EnsureControllersWired()
        {
            if (_controllersWired) return;

            var host = _input.gameObject;
            if (Segments == null) Segments = GetOrAddComponent<SegmentGenerationController>(host);
            // Задача «двойные флаги на плитах»: форсируем первую сборку
            // Generation.SegmentRowProvider СИНХРОННО здесь — до того, как
            // SyncLegacyObstacleGenerator ниже даст уже размещённому на
            // сцене Movement.TunnelObstacleController шанс материализовать
            // те же ряды напрямую. См. Generation.SegmentGenerationController.EnsureBuilt.
            Segments.EnsureBuilt();
            if (Currency == null) Currency = GetOrAddComponent<CurrencyController>(host);
            if (Altar == null) Altar = GetOrAddComponent<AltarController>(host);
            if (Boss == null) Boss = GetOrAddComponent<BossController>(host);
            if (Camp == null) Camp = GetOrAddComponent<CampController>(host);
            if (Lever == null) Lever = GetOrAddComponent<LeverActivationController>(host);
            if (TurnBasedTraps == null) TurnBasedTraps = GetOrAddComponent<TurnBasedTrapSystemsController>(host);
            // Зависит только от Currency (уже создана строкой выше) и
            // RunLifecycle.RunController, который уже размещён на сцене
            // независимо от порядка здесь — см. doc-комментарий BossRoom.
            if (BossRoom == null) BossRoom = GetOrAddComponent<BossRoomController>(host);

            _controllersWired = true;
            SyncLegacyObstacleGenerator(forceRebuild: false);
        }

        private static T GetOrAddComponent<T>(GameObject host) where T : Component =>
            host.GetComponent<T>() != null ? host.GetComponent<T>() : host.AddComponent<T>();

        /// <summary>
        /// Задача «двойные флаги на плитах» (плейтест владельца — сигнатура
        /// опасности вместо стены, рычаг/Алтарь иногда непроходимы,
        /// сигнатура вместо источника Маны): <c>Movement.TunnelObstacleController</c>
        /// уже размещён на сцене отдельно от этого класса и раньше сам
        /// подписывался на <c>RunStarted</c> — из-за более раннего
        /// <c>OnEnable</c> (объект сцены грузится раньше, чем этот
        /// self-bootstrap через AfterSceneLoad) его подписка ВСЕГДА
        /// оказывалась раньше <see cref="Segments"/> в списке подписчиков, и
        /// его <c>Movement.TunnelGridReveal</c> успевал материализовать ряды
        /// раньше, чем <c>Generation.SegmentRowProvider</c> успевал их
        /// заявить (<c>Core.TunnelGrid.ClaimRow</c>) — см.
        /// <c>Generation.Tests.SegmentGenerationCoexistenceTests.
        /// RevealedBeforeClaimed_ObstacleGeneratorWins_TemplateTileTypeSilentlyLost</c>.
        /// Теперь <c>TunnelObstacleController</c> сам на RunStarted не
        /// подписан — этот метод единственный, кто его (пере)собирает,
        /// СТРОГО после того, как <see cref="Segments"/> уже заявил свои
        /// ряды на этот момент (вызывается сразу после
        /// <see cref="Generation.SegmentGenerationController.EnsureBuilt"/>
        /// выше, либо после его же <c>RebuildProvider</c> на рестарте — см.
        /// <see cref="HandleRunStarted"/>).
        /// </summary>
        private void SyncLegacyObstacleGenerator(bool forceRebuild)
        {
            var obstacle = _input.GetComponent<TunnelObstacleController>();
            if (obstacle == null) return;

            if (forceRebuild) obstacle.EnsureRebuilt();
            else obstacle.EnsureBuilt();
        }

        private void EnsureRunStartedSubscription()
        {
            if (_subscribedToRunStarted) return;
            _input.RunStarted += HandleRunStarted;
            _subscribedToRunStarted = true;
        }

        private void HandleRunStarted()
        {
            RebuildLoadout();
            // Segments уже пересобрался сам к этому моменту — его подписка
            // на RunStarted (Generation.SegmentGenerationController.OnEnable)
            // добавлена раньше этой (см. EnsureControllersWired), поэтому её
            // обработчик в списке подписчиков GridTraceInputController.RunStarted
            // вызывается первым. См. doc-комментарий SyncLegacyObstacleGenerator.
            SyncLegacyObstacleGenerator(forceRebuild: true);
        }

        private void EnsureLoadoutReady()
        {
            if (Loadout != null) return;
            RebuildLoadout();
        }

        private void RebuildLoadout()
        {
            if (Altar == null || Altar.Collection == null) return; // Алтарь ещё не отработал свой Awake — попробуем на следующем кадре
            Loadout = new RunArtifactLoadout(Altar.Collection);
        }
    }
}
