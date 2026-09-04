using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Хранилище текстур плит тоннеля (issue "Завести арт в игру") —
    /// связывает <see cref="TileArtKind"/> с конкретным
    /// <see cref="Texture2D"/> из <c>Assets/Art/Tiles/</c>. ScriptableObject,
    /// а не статический класс с прямыми ссылками — Unity не умеет
    /// сериализовать ссылки на ассеты в чистом C#-классе, а вручную
    /// раскладывать текстуры по инспектору сцены/префаба нельзя (см.
    /// <c>docs/rules/forbidden-actions.md</c> — .unity/.prefab только
    /// руками). Экземпляр (<c>Assets/Resources/Art/TileArtCatalog.asset</c>)
    /// создаётся и заполняется Editor-скриптом
    /// (<c>Burmalda.EditorTools.ArtIntegrationSetup</c>), не руками в
    /// инспекторе — сами ссылки в этом файле остаются пустыми до его
    /// запуска.
    ///
    /// Лежит под <c>Resources/</c>, чтобы быть доступным в билде через
    /// <see cref="Load"/> (<c>Resources.Load</c>) без привязки к сцене —
    /// единственный код здесь, которому вообще нужна папка Resources
    /// (сами PNG остаются на путях, зафиксированных задачей загрузки
    /// ассетов, никуда не переносятся).
    ///
    /// <b>Задача «тёплый набор плит»:</b> обычная плита (<see cref="TileArtKind.Fresh"/>)
    /// — единственная категория с НЕСКОЛЬКИМИ вариантами текстуры
    /// (<see cref="GetFreshVariant"/>, не <see cref="Get"/>) — владелец
    /// прислал два рисунка одной и той же "целой плиты" специально для
    /// визуального разнообразия пола (см. <see cref="DebugVisuals.TunnelDebugVisual"/>,
    /// где вариант и поворот выбираются один раз при материализации).
    /// Третий присланный файл (<c>tile-fresh-c.png</c>) оказался побайтовым
    /// дублем <c>tile-about-to-decay.png</c> — не отдельным вариантом пола, а
    /// стадией распада; удалён, вариантов осталось два.
    /// </summary>
    public sealed class TileArtCatalog : ScriptableObject
    {
        /// <summary>Путь для <c>Resources.Load</c> — без расширения, относительно любой папки <c>Resources/</c>.</summary>
        public const string ResourcesPath = "Art/TileArtCatalog";

        [SerializeField] private Texture2D _fresh;
        [SerializeField] private Texture2D _freshVariantB;
        [SerializeField] private Texture2D _halfDecayed;
        [SerializeField] private Texture2D _aboutToDecay;
        [SerializeField] private Texture2D _destroyed;
        [SerializeField] private Texture2D _start;
        [SerializeField] private Texture2D _blocked;
        [SerializeField] private Texture2D _lava;
        // Задача «тёплый набор плит» (владелец, прямое указание): одна и та
        // же текстура для ОБЕИХ ролей — скрытой опасности (яма/сработавший
        // взрыв) И сигнатуры механизма (оба вида триггера). См.
        // doc-комментарий Get() — намеренно нет отдельного поля под
        // TriggerSignature, чтобы развести их снова стало нельзя без правки
        // этого файла, а не просто подсунуть другой файл в ArtIntegrationSetup.
        [SerializeField] private Texture2D _hiddenTrapSignature;
        [SerializeField] private Texture2D _timedTrapActive;
        // Задача «тёплый набор плит» — новые категории, раньше все были TileArtKind.None.
        [SerializeField] private Texture2D _currentPosition;
        [SerializeField] private Texture2D _manaSource;
        [SerializeField] private Texture2D _keySource;
        [SerializeField] private Texture2D _lever;
        [SerializeField] private Texture2D _gateClosed;
        [SerializeField] private Texture2D _gateOpen;
        [SerializeField] private Texture2D _altar;

        // Задача «разрушение плиты» (2026-09-01): НЕ TileArtKind — маска
        // трещин накладывается оверлеем поверх ЛЮБОЙ базовой текстуры
        // (Fresh-варианты), не заменяет её как отдельное состояние (см.
        // TileArtKindResolver.ResolveDecayGradient). Отдельное поле и
        // отдельный аксессор (CrackMaskTexture), не Get(TileArtKind) —
        // семантически это не "какая плита показана", а "насколько она
        // потрескалась". Сгенерирована как разность пикселей
        // tile-half-decayed.png/tile-about-to-decay.png с tile-fresh.png
        // (см. её doc-комментарий в docs/wiki/roadmap.md).
        [SerializeField] private Texture2D _crackMask;

        private static TileArtCatalog _cached;
        private static bool _loadAttempted;

        /// <summary>
        /// Кэшированная загрузка из <c>Resources</c>. Null, если ассет ещё
        /// не создан Editor-скриптом (или удалён) — вызывающий код
        /// (<see cref="TunnelDebugVisual"/>) обязан считать это "арта нет"
        /// и падать обратно на <see cref="TileDebugColor"/>, не бросать
        /// исключение (та же оборонительная логика, что у отсутствующего
        /// шейдера, см. doc-комментарий <see cref="TunnelDebugVisual"/>).
        /// </summary>
        public static TileArtCatalog Load()
        {
            if (_loadAttempted) return _cached;
            _loadAttempted = true;
            _cached = Resources.Load<TileArtCatalog>(ResourcesPath);
            return _cached;
        }

        public Texture2D Get(TileArtKind kind)
        {
            switch (kind)
            {
                case TileArtKind.Fresh: return _fresh;
                case TileArtKind.HalfDecayed: return _halfDecayed;
                case TileArtKind.AboutToDecay: return _aboutToDecay;
                case TileArtKind.Destroyed: return _destroyed;
                case TileArtKind.Start: return _start;
                case TileArtKind.Blocked: return _blocked;
                case TileArtKind.Lava: return _lava;
                case TileArtKind.HiddenTrapSignature: return _hiddenTrapSignature;
                case TileArtKind.TimedTrapActive: return _timedTrapActive;
                // PRD v9 §4.2: до Идола Чутья игрок не должен уметь отличить
                // ЛОВУШКУ (яма/сработавший взрыв — HiddenTrapSignature) от
                // МЕХАНИЗМА, который вооружает ловушку где-то ещё (оба вида
                // триггера — TriggerSignature) — намеренно одна и та же
                // текстура на оба TileArtKind, не два похожих файла. Разведёт
                // их снова кто-то в будущем перегенерацией одной из них по
                // отдельности — механика разведки сломается молча, без
                // единого падающего теста (тесты проверяют TileArtKind,
                // текстуру за ним TileArtCatalog не различает вовсе).
                case TileArtKind.TriggerSignature: return _hiddenTrapSignature;
                case TileArtKind.CurrentPosition: return _currentPosition;
                case TileArtKind.ManaSource: return _manaSource;
                case TileArtKind.KeySource: return _keySource;
                case TileArtKind.Lever: return _lever;
                case TileArtKind.GateClosed: return _gateClosed;
                case TileArtKind.GateOpen: return _gateOpen;
                case TileArtKind.Altar: return _altar;
                // Баг с устройства (владелец, 2026-09-04): переиспользует
                // ту же текстуру, что Altar — свой тон применяет вызывающая
                // сторона (см. TunnelDebugVisual.BossArtTint), не
                // отдельный файл. См. doc-комментарий TileArtKind.Boss.
                case TileArtKind.Boss: return _altar;
                default: return null;
            }
        }

        /// <summary>
        /// Маска трещин для непрерывного оверлея распада (задача
        /// «разрушение плиты») — см. doc-комментарий поля <c>_crackMask</c>.
        /// Null, если <c>ArtIntegrationSetup</c> ещё не запускался или файл
        /// удалён — вызывающий код (<see cref="TunnelDebugVisual"/>) обязан
        /// считать это "оверлея нет" и не создавать геометрию под него, тот
        /// же оборонительный принцип, что у <see cref="Get"/>.
        /// </summary>
        public Texture2D CrackMaskTexture => _crackMask;

        /// <summary>Число вариантов текстуры для <see cref="TileArtKind.Fresh"/> (см. класс-докстроку).</summary>
        public const int FreshVariantCount = 2;

        /// <summary>
        /// Вариант текстуры обычной плиты, 0..<see cref="FreshVariantCount"/>-1
        /// (0 — <c>tile-fresh</c>, 1 — <c>tile-fresh-b</c>).
        /// Индекс вне диапазона — тот же фолбэк, что <see cref="Get"/> для
        /// пустого поля: возвращает null, вызывающий код падает на цвет.
        /// </summary>
        public Texture2D GetFreshVariant(int index)
        {
            switch (index)
            {
                case 0: return _fresh;
                case 1: return _freshVariantB;
                default: return null;
            }
        }

#if UNITY_EDITOR
        // Только для Editor-скрипта заполнения (ArtIntegrationSetup) —
        // рантайм-код катaлог только читает через Get()/GetFreshVariant().
        public void EditorAssign(Texture2D fresh, Texture2D freshVariantB, Texture2D halfDecayed, Texture2D aboutToDecay, Texture2D destroyed,
            Texture2D start, Texture2D blocked, Texture2D lava, Texture2D hiddenTrapSignature,
            Texture2D timedTrapActive, Texture2D currentPosition, Texture2D manaSource,
            Texture2D keySource, Texture2D lever, Texture2D gateClosed, Texture2D gateOpen, Texture2D altar,
            Texture2D crackMask)
        {
            _fresh = fresh;
            _freshVariantB = freshVariantB;
            _halfDecayed = halfDecayed;
            _aboutToDecay = aboutToDecay;
            _destroyed = destroyed;
            _start = start;
            _blocked = blocked;
            _lava = lava;
            _hiddenTrapSignature = hiddenTrapSignature;
            _timedTrapActive = timedTrapActive;
            _currentPosition = currentPosition;
            _manaSource = manaSource;
            _keySource = keySource;
            _lever = lever;
            _gateClosed = gateClosed;
            _gateOpen = gateOpen;
            _altar = altar;
            _crackMask = crackMask;
        }
#endif
    }
}
