using Burmalda.Core;
using Burmalda.Generation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Шесть долей вероятности процедурной генерации препятствий/ловушек
    /// (<see cref="TunnelObstacleGenerator.BlockedShare"/> и далее) —
    /// задача «сделать тоннель играбельным», часть 2 (плейтест владельца,
    /// 2026-08-31): "ловушки редкие, играть неинтересно совсем". Причина —
    /// смена темпа (протяжка пальца в прототипе → дискретный шаг с
    /// примериванием, PRD v9 §4.1), не ошибка в коде — точную плотность
    /// подбирает владелец здесь, на устройстве, а не агент вслепую (см.
    /// doc-комментарий <see cref="TunnelObstacleGenerator"/>).
    ///
    /// <b>Задача «партии 1 и 2 + правила отбора» (плейтест владельца,
    /// 2026-08-31):</b> добавлены два ползунка окна тиров сегментов
    /// (<see cref="SegmentSelector.TierWindowBelow"/>/<see cref="SegmentSelector.TierWindowAbove"/>)
    /// — "рядом с плотностью генерации", как прямо просит задача. Диагноз
    /// был не в плотности ловушек (крутить её дальше бесполезно), а в том,
    /// что селектор сегментов кумулятивно держал самые пустые тир-1
    /// раскладки доступными весь забег — окно вокруг цели вытесняет их по
    /// мере роста Яруса, точные границы подбирает владелец здесь же.
    ///
    /// Тот же принцип, что <see cref="EconomyDebugPanel"/>/<see cref="CameraAnchorDebugPanel"/>:
    /// Canvas/Slider вместо OnGUI, самобутстрап через
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/>, ничего не
    /// трогает в .unity/.prefab (docs/rules/forbidden-actions.md). Свёрнута
    /// по умолчанию (кнопка GEN в углу).
    ///
    /// Позиция — НИЖНИЙ ЛЕВЫЙ угол: верхняя строка занята CAM (левый)/HUD
    /// (центр)/RESTART+ECO (правый), восемь строк этой панели туда физически
    /// не поместились бы без перекрытия.
    ///
    /// <b>Только процедурный генератор и правила отбора шаблонов</b> —
    /// содержимое авторских сегментов (<c>Generation.SegmentTemplateCatalog</c>)
    /// сюда не входит: это контент, не параметр (docs/rules/forbidden-actions.md).
    /// </summary>
    public sealed class TrapDensityDebugPanel : MonoBehaviour
    {
        private const float PanelWidth = 460f;
        private const float Margin = 24f;
        private const float RowHeight = 100f;
        private const float ToggleButtonSize = 72f;
        private const float MaxShare = 0.3f; // 30% на один тип — щедрый запас над стартовыми значениями для ручного подбора
        private const float MaxTierWindow = 4f; // шире некуда — весь диапазон тиров 1..5

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(TrapDensityDebugPanel));
            host.AddComponent<TrapDensityDebugPanel>();
            DontDestroyOnLoad(host);
        }

        private GameObject _panelRoot;
        private bool _expanded;

        private void Start()
        {
            EnsureEventSystemExists();
            BuildUi();
        }

        private static void EnsureEventSystemExists()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var host = new GameObject(nameof(EventSystem));
            DontDestroyOnLoad(host);
            host.AddComponent<EventSystem>();
            host.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildUi()
        {
            var canvasHost = new GameObject(nameof(TrapDensityDebugPanel) + "_Canvas");
            canvasHost.transform.SetParent(transform, worldPositionStays: false);

            var canvas = canvasHost.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasHost.AddComponent<CanvasScaler>();
            canvasHost.AddComponent<GraphicRaycaster>();

            BuildExpandButton(canvasHost.transform);
            BuildPanel(canvasHost.transform);
            _panelRoot.SetActive(_expanded);
        }

        private void BuildExpandButton(Transform parent)
        {
            var host = new GameObject("ToggleButton");
            host.transform.SetParent(parent, worldPositionStays: false);

            var image = host.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

            var rect = host.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(ToggleButtonSize, ToggleButtonSize);
            rect.anchoredPosition = new Vector2(Margin, Margin);

            var button = host.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                _expanded = !_expanded;
                _panelRoot.SetActive(_expanded);
            });

            var text = AddLabel(host.transform, "GEN", 20, TextAnchor.MiddleCenter);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private void BuildPanel(Transform parent)
        {
            _panelRoot = new GameObject("Panel");
            _panelRoot.transform.SetParent(parent, worldPositionStays: false);

            var image = _panelRoot.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            var rect = _panelRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(PanelWidth, RowHeight * 8f + Margin * 2f);
            rect.anchoredPosition = new Vector2(Margin, Margin + ToggleButtonSize + Margin);

            BuildRow(_panelRoot.transform, 0, "Заблокировано", 0f, MaxShare, TunnelObstacleGenerator.BlockedShare,
                v => TunnelObstacleGenerator.BlockedShare = v, FormatPercent);
            BuildRow(_panelRoot.transform, 1, "Яма", 0f, MaxShare, TunnelObstacleGenerator.PitShare,
                v => TunnelObstacleGenerator.PitShare = v, FormatPercent);
            BuildRow(_panelRoot.transform, 2, "Лава", 0f, MaxShare, TunnelObstacleGenerator.LavaShare,
                v => TunnelObstacleGenerator.LavaShare = v, FormatPercent);
            BuildRow(_panelRoot.transform, 3, "Триггер взрыва", 0f, MaxShare, TunnelObstacleGenerator.ExplosiveTriggerShare,
                v => TunnelObstacleGenerator.ExplosiveTriggerShare = v, FormatPercent);
            BuildRow(_panelRoot.transform, 4, "Триггер стрелы", 0f, MaxShare, TunnelObstacleGenerator.TimedTrapArrowShare,
                v => TunnelObstacleGenerator.TimedTrapArrowShare = v, FormatPercent);
            BuildRow(_panelRoot.transform, 5, "Триггер лезвия", 0f, MaxShare, TunnelObstacleGenerator.TimedTrapBladeShare,
                v => TunnelObstacleGenerator.TimedTrapBladeShare = v, FormatPercent);

            // Задача «партии 1 и 2 + правила отбора», часть 3: окно тиров
            // сегментов — "рядом с плотностью генерации".
            BuildRow(_panelRoot.transform, 6, "Окно тиров: ниже", 0f, MaxTierWindow, SegmentSelector.TierWindowBelow,
                v => SegmentSelector.TierWindowBelow = Mathf.RoundToInt(v), FormatTierCount);
            BuildRow(_panelRoot.transform, 7, "Окно тиров: выше", 0f, MaxTierWindow, SegmentSelector.TierWindowAbove,
                v => SegmentSelector.TierWindowAbove = Mathf.RoundToInt(v), FormatTierCount);
        }

        private static string FormatPercent(float v) => (v * 100f).ToString("0.0") + "%";
        private static string FormatTierCount(float v) => Mathf.RoundToInt(v).ToString();

        /// <summary>Строка "подпись + слайдер + текущее значение" — та же разметка, что EconomyDebugPanel.BuildRow/CameraAnchorDebugPanel.BuildRow.</summary>
        private void BuildRow(Transform parent, int rowIndex, string label, float min, float max, float initialValue,
            System.Action<float> onValueChanged, System.Func<float, string> format)
        {
            var rowHost = new GameObject($"Row_{rowIndex}");
            rowHost.transform.SetParent(parent, worldPositionStays: false);
            var rowRect = rowHost.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.sizeDelta = new Vector2(-Margin * 2f, RowHeight);
            rowRect.anchoredPosition = new Vector2(Margin, -Margin - rowIndex * RowHeight);

            var labelText = AddLabel(rowHost.transform, label, 20, TextAnchor.UpperLeft);
            var labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(0.65f, 1f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var valueText = AddLabel(rowHost.transform, format(initialValue), 20, TextAnchor.UpperRight);
            var valueRect = valueText.GetComponent<RectTransform>();
            valueRect.anchorMin = new Vector2(0.65f, 0.5f);
            valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;

            var sliderHost = new GameObject("Slider");
            sliderHost.transform.SetParent(rowHost.transform, worldPositionStays: false);
            var sliderRect = sliderHost.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0f);
            sliderRect.anchorMax = new Vector2(1f, 0.45f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;

            var bgHost = new GameObject("Background");
            bgHost.transform.SetParent(sliderHost.transform, worldPositionStays: false);
            var bgImage = bgHost.AddComponent<Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            var bgRect = bgHost.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.25f);
            bgRect.anchorMax = new Vector2(1f, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var fillAreaHost = new GameObject("Fill Area");
            fillAreaHost.transform.SetParent(sliderHost.transform, worldPositionStays: false);
            var fillAreaRect = fillAreaHost.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            var fillHost = new GameObject("Fill");
            fillHost.transform.SetParent(fillAreaHost.transform, worldPositionStays: false);
            var fillImage = fillHost.AddComponent<Image>();
            fillImage.color = new Color(1f, 0.6f, 0.2f, 1f);
            var fillRect = fillHost.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var handleAreaHost = new GameObject("Handle Slide Area");
            handleAreaHost.transform.SetParent(sliderHost.transform, worldPositionStays: false);
            var handleAreaRect = handleAreaHost.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = Vector2.zero;
            handleAreaRect.offsetMax = Vector2.zero;

            var handleHost = new GameObject("Handle");
            handleHost.transform.SetParent(handleAreaHost.transform, worldPositionStays: false);
            var handleImage = handleHost.AddComponent<Image>();
            handleImage.color = Color.white;
            var handleRect = handleHost.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(24f, 0f);

            var slider = sliderHost.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = initialValue;

            slider.onValueChanged.AddListener(v =>
            {
                onValueChanged(v);
                valueText.text = format(v);
            });
        }

        private Text AddLabel(Transform parent, string content, int fontSize, TextAnchor alignment)
        {
            var host = new GameObject("Label");
            host.transform.SetParent(parent, worldPositionStays: false);
            host.AddComponent<RectTransform>();

            var text = host.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // встроенный шрифт — без арт-ассетов
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }
    }
}
