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
    /// </summary>
    public sealed class TileArtCatalog : ScriptableObject
    {
        /// <summary>Путь для <c>Resources.Load</c> — без расширения, относительно любой папки <c>Resources/</c>.</summary>
        public const string ResourcesPath = "Art/TileArtCatalog";

        [SerializeField] private Texture2D _fresh;
        [SerializeField] private Texture2D _halfDecayed;
        [SerializeField] private Texture2D _aboutToDecay;
        [SerializeField] private Texture2D _destroyed;
        [SerializeField] private Texture2D _start;
        [SerializeField] private Texture2D _blocked;
        [SerializeField] private Texture2D _lava;
        [SerializeField] private Texture2D _hiddenTrapSignature;
        [SerializeField] private Texture2D _timedTrapActive;
        // Задача «сделать тоннель играбельным», часть 3: единое поле вместо
        // прежних _explosiveTrigger/_timedTrapTrigger — см. TileArtKind.TriggerSignature.
        [SerializeField] private Texture2D _triggerSignature;

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
                case TileArtKind.TriggerSignature: return _triggerSignature;
                default: return null;
            }
        }

#if UNITY_EDITOR
        // Только для Editor-скрипта заполнения (ArtIntegrationSetup) —
        // рантайм-код катaлог только читает через Get().
        public void EditorAssign(Texture2D fresh, Texture2D halfDecayed, Texture2D aboutToDecay, Texture2D destroyed,
            Texture2D start, Texture2D blocked, Texture2D lava, Texture2D hiddenTrapSignature,
            Texture2D timedTrapActive, Texture2D triggerSignature)
        {
            _fresh = fresh;
            _halfDecayed = halfDecayed;
            _aboutToDecay = aboutToDecay;
            _destroyed = destroyed;
            _start = start;
            _blocked = blocked;
            _lava = lava;
            _hiddenTrapSignature = hiddenTrapSignature;
            _timedTrapActive = timedTrapActive;
            _triggerSignature = triggerSignature;
        }
#endif
    }
}
