using System;
using System.Collections.Generic;
using System.Linq;
using Burmalda.Artifacts;
using Burmalda.Camp;
using UnityEngine;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Экран Лагеря (PRD v10 §17, §22.2, §22.4): два постамента — Идол и Тотем. Стрелки выбирают активный предмет из собранных
    /// (слот пуст, если нечего), кнопка прокачки показывает цену (L1→L2→L3, Монеты); уровень виден на постаменте — ореол и
    /// лучи. Баланс Монет и «Новая экспедиция» внизу. Без текстовых стен. Временный UI на примитивах, картинки рисуются кодом.
    /// </summary>
    public sealed class CampScreen : MonoBehaviour
    {
        private static CampScreen _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(CampScreen));
            _instance = host.AddComponent<CampScreen>();
            DontDestroyOnLoad(host);
        }

        /// <summary>Открывает экран Лагеря. <paramref name="onStartRun"/> — «Новая экспедиция».</summary>
        public static void Open(CampController camp, Action onStartRun)
        {
            if (_instance != null) _instance.Show(camp, onStartRun);
        }

        private sealed class Column
        {
            public CollectibleKind Kind;
            public RawImage Image;
            public Text Name, Level;
            public Button Upgrade, Prev, Next;
            public Text UpgradeLabel;
        }

        private CampController _campController;
        private Action _onStartRun;
        private GameObject _panel;
        private Text _coins;
        private readonly Dictionary<CollectibleKind, Column> _columns = new Dictionary<CollectibleKind, Column>();

        private Camp.Camp Camp => _campController.Camp;

        private void Start() => BuildPanel();

        private void Show(CampController camp, Action onStartRun)
        {
            _campController = camp;
            _onStartRun = onStartRun;
            if (_panel == null) BuildPanel();
            Camp.Progress.AutoEquipIfEmpty();
            Refresh();
            _panel.SetActive(true);
        }

        private void Refresh()
        {
            _coins.text = "Монеты: " + CoinsOf();
            foreach (var column in _columns.Values)
            {
                var state = CampPedestals.For(Camp.Progress, column.Kind);
                var activeId = column.Kind == CollectibleKind.Idol ? Camp.Progress.ActiveIdolId : Camp.Progress.ActiveTotemId;
                var item = activeId != null ? CollectibleCatalog.Get(activeId) : null;
                var level = item != null ? Camp.Progress.LevelOf(item.Id) : 0;
                column.Image.texture = item != null
                    ? CampArtPainter.SilhouetteTexture(column.Kind, item.PartsRequired, item.PartsRequired, item.Rarity, 0f, level)
                    : CampArtPainter.SilhouetteTexture(column.Kind, 0, 0, RelicRarity.Rare, state.PityGlow, 1);
                column.Name.text = item != null ? item.Name : "—";
                column.Level.text = item != null ? $"L{level}" : "";
                var assembled = Assembled(column.Kind);
                column.Prev.gameObject.SetActive(assembled.Count > 1);
                column.Next.gameObject.SetActive(assembled.Count > 1);

                var canUpgrade = item != null && level < CollectionProgress.MaxLevel;
                column.Upgrade.gameObject.SetActive(canUpgrade);
                if (canUpgrade)
                {
                    var price = CampUpgrades.PriceFor(item, level);
                    column.UpgradeLabel.text = $"L{level + 1}  ·  {price}";
                    column.Upgrade.interactable = CoinsOf() >= price;
                }
            }
        }

        private int CoinsOf() => _campController.Coins;

        private List<CollectibleItem> Assembled(CollectibleKind kind) =>
            CollectibleCatalog.All.Where(i => i.Kind == kind && Camp.Progress.IsAssembled(i)).ToList();

        private void Cycle(CollectibleKind kind, int step)
        {
            var list = Assembled(kind);
            if (list.Count == 0) return;
            var current = kind == CollectibleKind.Idol ? Camp.Progress.ActiveIdolId : Camp.Progress.ActiveTotemId;
            var index = Mathf.Max(0, list.FindIndex(i => i.Id == current));
            Camp.Equip(list[(index + step + list.Count) % list.Count].Id);
            Refresh();
        }

        private void Upgrade(CollectibleKind kind)
        {
            var id = kind == CollectibleKind.Idol ? Camp.Progress.ActiveIdolId : Camp.Progress.ActiveTotemId;
            if (id != null) Camp.TryUpgrade(id);
            Refresh();
        }

        private void StartRun()
        {
            _panel.SetActive(false);
            _onStartRun?.Invoke();
        }

        private void BuildPanel()
        {
            if (_panel != null) return;
            var canvasHost = new GameObject(nameof(CampScreen) + "_Canvas");
            canvasHost.transform.SetParent(transform, false);
            var canvas = canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 112;
            var scaler = canvasHost.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasHost.AddComponent<GraphicRaycaster>();

            _panel = Rect("CampPanel", canvasHost.transform, Vector2.zero, Vector2.zero).gameObject;
            var rect = (RectTransform)_panel.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _panel.AddComponent<Image>().color = new Color(0.06f, 0.055f, 0.05f, 0.98f);

            Label(_panel.transform, "Лагерь", 60, new Vector2(0f, 800f), 900f, 90f, Color.white);
            _coins = Label(_panel.transform, "", 46, new Vector2(0f, 690f), 900f, 70f, new Color(1f, 0.84f, 0f));
            BuildColumn(CollectibleKind.Idol, -250f);
            BuildColumn(CollectibleKind.Totem, 250f);
            var start = Button(_panel.transform, "Новая экспедиция", new Vector2(0f, -760f), new Vector2(620f, 130f), StartRun);
            start.GetComponent<Image>().color = new Color(0.2f, 0.35f, 0.2f, 1f);
            _panel.SetActive(false);
        }

        private void BuildColumn(CollectibleKind kind, float x)
        {
            var column = new Column { Kind = kind };
            column.Image = Rect("Pedestal_" + kind, _panel.transform, new Vector2(420f, 420f), new Vector2(x, 330f)).gameObject.AddComponent<RawImage>();
            column.Image.raycastTarget = false;
            column.Name = Label(_panel.transform, "", 48, new Vector2(x, 80f), 460f, 70f, Color.white);
            column.Level = Label(_panel.transform, "", 56, new Vector2(x, 10f), 200f, 70f, new Color(1f, 0.84f, 0f));
            column.Prev = Button(_panel.transform, "◀", new Vector2(x - 180f, 80f), new Vector2(90f, 90f), () => Cycle(kind, -1));
            column.Next = Button(_panel.transform, "▶", new Vector2(x + 180f, 80f), new Vector2(90f, 90f), () => Cycle(kind, 1));
            column.Upgrade = Button(_panel.transform, "", new Vector2(x, -110f), new Vector2(380f, 110f), () => Upgrade(kind));
            column.UpgradeLabel = column.Upgrade.GetComponentInChildren<Text>();
            _columns[kind] = column;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size;
            r.anchoredPosition = pos;
            return r;
        }

        private static Text Label(Transform parent, string text, int size, Vector2 pos, float width, float height, Color color)
        {
            var t = Rect("Text", parent, new Vector2(width, height), pos).gameObject.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.text = text;
            t.raycastTarget = false;
            return t;
        }

        private static Button Button(Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var r = Rect("Button", parent, size, pos);
            var image = r.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.2f, 0.18f, 1f);
            var button = r.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            Label(r, label, 42, Vector2.zero, size.x, size.y, Color.white);
            return button;
        }
    }
}
