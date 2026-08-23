using Burmalda.Artifacts;
using Burmalda.Currencies;
using Burmalda.Movement;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Отладочный HUD забега (issue "Задача 2 — отладочный HUD", критический
    /// путь перед Комнатой Босса, PRD v8 §8: без HUD её нельзя ни отладить,
    /// ни оценить в руке). Уровень исполнения — как <see cref="TunnelDebugVisual"/>:
    /// разборчиво, крупно, функционально, не финальный арт/анимации.
    /// <c>OnGUI</c> — тот же уровень, что <see cref="RunFeedbackDebugUI"/>/
    /// <see cref="TilePreviewController"/>, не Canvas (чистый текст, слайдеры
    /// не нужны). Самобутстрапится через
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/>, не трогает
    /// .unity/.prefab (docs/rules/forbidden-actions.md).
    ///
    /// Гейтится <see cref="RunHudToggles.ShowRunHud"/> (дефолт ВКЛ, панель —
    /// <see cref="RunHudTogglePanel"/>). Все значения — СТРОГО чтение уже
    /// существующих публичных свойств (<see cref="CurrencyController"/>/
    /// <see cref="RunCurrencyAccumulator"/>/<see cref="RunHudDataSources"/>),
    /// ничего не пересчитывается и не хранится здесь — задача явно это
    /// запрещает.
    ///
    /// <b>Не занимает нижнюю треть экрана</b> (там палец игрока) — все Rect
    /// ниже лежат в верхних 2/3 (Screen.height*2/3).
    ///
    /// <b>Три из шести запрошенных задачей элементов сейчас не имеют живого
    /// источника данных</b> (расхождение с предпосылкой задачи, установлено
    /// на этой ветке, а не только для явно оговорённой Комнаты Босса):
    /// - Множитель Комнаты / дистанция до волны — см. <see cref="RunHudDataSources.ActiveBossRoom"/>,
    ///   задача сама предупреждает про это ("элементов Комнаты Босса в коде
    ///   ещё нет").
    /// - Состав билда (Амулеты/Талисманы) — <c>RunArtifactLoadout</c>
    ///   существует и полностью рабочий, но НИ ОДИН Controller в проекте не
    ///   создаёт его экземпляр на забег (см. doc-комментарий
    ///   <see cref="RunHudDataSources"/>) — задача не предупреждала про этот
    ///   конкретный пробел, но тот же принцип применён: пусто, без заглушки.
    /// - <b>Несомые Реликвии и суммарный Вес НЕ реализованы вообще</b> — в
    ///   коде нет ни одного поля/класса под "Вес" (PRD v8 §6.3 описывает
    ///   механику, ничего из неё не построено, даже Relic.cs без Weight).
    ///   Заводить тип-заглушку под неё значило бы создавать новую систему
    ///   (задача явно запрещает) — эта строка HUD не реализована совсем, не
    ///   просто скрыта. См. отчёт по задаче.
    ///
    /// Спрайты валют/артефактов (issue допускает подставить, Id файлов =
    /// Id <c>ArtifactCatalog</c>) недоступны на этой ветке — пакет арта
    /// (<c>feat/scenario-art-assets-batch1</c>) ещё не смержен в <c>main</c>,
    /// <c>Assets/Art/</c> здесь просто нет. Везде ниже — текстовый фолбэк,
    /// который задача явно разрешает на случай отсутствия спрайта.
    /// </summary>
    public sealed class RunHudOverlay : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject(nameof(RunHudOverlay));
            host.AddComponent<RunHudOverlay>();
            DontDestroyOnLoad(host);
        }

        private GridTraceInputController _input;
        private CurrencyController _currency;

        private void Update()
        {
            // Ленивый поиск — тот же паттерн, что у остальных debug-driver'ов
            // проекта (порядок Awake между объектами сцены не гарантирован).
            if (_input == null) _input = FindFirstObjectByType<GridTraceInputController>();
            if (_currency == null) _currency = FindFirstObjectByType<CurrencyController>();
        }

        private void OnGUI()
        {
            if (!RunHudToggles.ShowRunHud) return;
            if (_input == null || _currency == null) return;
            if (_currency.RunManaCrystals == null || _currency.RunKeys == null || _currency.RunCoins == null) return; // ещё не пересобраны на этот забег

            DrawCurrencyBlock();
            DrawArtifactLoadoutBlock();
            DrawBossRoomBlock();
        }

        // Верхняя треть экрана слева — числа валют, всегда видны в забеге.
        // Fallback-текст вместо спрайта (issue допускает, см. doc-комментарий
        // класса — на этой ветке спрайтов нет вообще).
        private void DrawCurrencyBlock()
        {
            var style = new GUIStyle(GUI.skin.box) { fontSize = 30, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            var text =
                $"Кристаллы Маны: {_currency.RunManaCrystals.Total}\n" +
                $"Ключи: {_currency.RunKeys.Total}\n" +
                $"Монеты: {_currency.RunCoins.Total}";
            GUI.Box(new Rect(20f, 130f, 420f, 150f), text, style);
        }

        // Состав билда — пусто, пока RunHudDataSources.ActiveArtifactLoadout
        // не присвоен (сейчас никогда, см. doc-комментарий класса).
        private void DrawArtifactLoadoutBlock()
        {
            var loadout = RunHudDataSources.ActiveArtifactLoadout;
            if (loadout == null) return;

            var amulets = RunHudFormatting.FormatArtifactIds(loadout.Acquired, ArtifactCategory.Amulet);
            var talismans = RunHudFormatting.FormatArtifactIds(loadout.Acquired, ArtifactCategory.Talisman);

            var style = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.UpperLeft, wordWrap = true };
            var text = $"Амулеты: {amulets}\nТалисманы: {talismans}";
            GUI.Box(new Rect(20f, 300f, 500f, 140f), text, style);
        }

        // Множитель Комнаты — самый крупный элемент экрана, ТОЛЬКО внутри
        // Комнаты (RunHudDataSources.ActiveBossRoom != null И ещё активна).
        // Дистанция до волны — рядом, когда волна есть (у BossRoom она есть
        // всегда с момента создания). Оба пусты сейчас, см. doc-комментарий
        // класса — Комната Босса ни разу не создаётся в реальной игре.
        private void DrawBossRoomBlock()
        {
            var room = RunHudDataSources.ActiveBossRoom;
            if (room == null || !room.IsActive) return;

            var multiplierStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 140,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            // Верхние 2/3 экрана — центр по горизонтали, треть по вертикали
            // (самый крупный элемент, но не в нижней трети, где палец).
            GUI.Label(new Rect(0f, Screen.height * 0.2f, Screen.width, 220f), $"×{room.Multiplier.CurrentMultiplier:0.0}", multiplierStyle);

            var distanceRows = _input.Trail.CurrentPosition.Row - room.Wave.RowPosition;
            var distanceStyle = new GUIStyle(GUI.skin.box) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(Screen.width / 2f - 200f, Screen.height * 0.2f + 230f, 400f, 90f), $"До волны: {distanceRows:0.0} ряд.", distanceStyle);
        }
    }
}
