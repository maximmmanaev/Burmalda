using Burmalda.Artifacts;
using Burmalda.Bootstrap;
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
    /// Гейтится <see cref="RunHudToggles.ShowRunHud"/> (дефолт ВЫКЛ с задачи
    /// «HUD накладывается сам на себя» — см. её doc-комментарий: этот
    /// оверлей и Canvas-based <see cref="HudDesign.RunHudDesignOverlay"/>
    /// рисовали дублирующие числа валют одновременно, оба default-ВКЛ,
    /// владелец продукта увидел на устройстве буквально перечёркнутые друг
    /// другом цифры; панель — <see cref="RunHudTogglePanel"/>). Все значения — СТРОГО чтение уже
    /// существующих публичных свойств (<see cref="CurrencyController"/>/
    /// <see cref="RunCurrencyAccumulator"/>/<see cref="RunHudDataSources"/>),
    /// ничего не пересчитывается и не хранится здесь — задача явно это
    /// запрещает.
    ///
    /// <b>Не занимает нижнюю треть экрана</b> (там палец игрока) — все Rect
    /// ниже лежат в верхних 2/3 (Screen.height*2/3).
    ///
    /// <b>Один из шести запрошенных задачей элементов сейчас не имеет живого
    /// источника данных</b> (расхождение с предпосылкой задачи, установлено
    /// на этой ветке). Состав билда (Амулеты/Талисманы) больше НЕ в этом
    /// списке — задача "композиционный корень RunBootstrap" завела реальный
    /// живой <c>RunArtifactLoadout</c> (см. <see cref="RunBootstrap.Loadout"/>),
    /// строка ниже читает его напрямую. Множитель Комнаты / дистанция до
    /// волны тоже больше не в этом списке — задача «Комната Босса» (владелец,
    /// 2026-09-02) завела <see cref="RunBootstrap.BossRoom"/>
    /// (<c>BossRoom.BossRoomController</c>), <see cref="Update"/> ниже читает
    /// его в <see cref="RunHudDataSources.ActiveBossRoom"/> на каждый кадр.
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

        // Задача «закрыть вертикальный срез — плита Эхо» (владелец,
        // 2026-09-05): «момент взятия Эха — событие... экран реагирует
        // целиком». BossRoom пересобирается на каждый заход (новый
        // экземпляр — см. её doc-комментарий), поэтому подписка живёт по
        // тому же паттерну "отследить смену ссылки", что уже есть в
        // RunFeedbackDebugUI (_wiredBossController) — не переподписываемся
        // каждый кадр, только когда экземпляр реально сменился.
        private Burmalda.BossRoom.BossRoom _wiredBossRoom;
        private const float EchoFlashDurationSeconds = 0.5f;
        private float _echoFlashExpiresAtTime = -1f;

        private void Update()
        {
            // Читаем из RunBootstrap (задача "композиционный корень
            // RunBootstrap") — единственный источник живых игровых систем,
            // не создаём и не ищем собственные экземпляры. Null, пока
            // RunBootstrap ещё не нашёл сцену/не пересобрал системы —
            // OnGUI ниже уже гейтит именно на этот случай.
            _input = RunBootstrap.Instance?.Input;
            _currency = RunBootstrap.Instance?.Currency;
            // Задача «Комната Босса» (владелец, 2026-09-02): единственный
            // писатель RunHudDataSources.ActiveBossRoom — раньше поле не
            // заполнял никто (см. её doc-комментарий), теперь читаем живой
            // экземпляр из RunBootstrap.BossRoom на каждый кадр, как и
            // Input/Currency выше. DrawBossRoomBlock() ниже уже был готов
            // читать это поле — правка ограничена этой одной строкой.
            var activeRoom = RunBootstrap.Instance?.BossRoom?.ActiveRoom;
            RunHudDataSources.ActiveBossRoom = activeRoom;

            if (activeRoom != _wiredBossRoom)
            {
                if (_wiredBossRoom != null) _wiredBossRoom.EchoCollected -= HandleEchoCollected;
                _wiredBossRoom = activeRoom;
                if (_wiredBossRoom != null) _wiredBossRoom.EchoCollected += HandleEchoCollected;
            }
        }

        private void OnDisable()
        {
            if (_wiredBossRoom != null) _wiredBossRoom.EchoCollected -= HandleEchoCollected;
            _wiredBossRoom = null;
        }

        private void HandleEchoCollected() => _echoFlashExpiresAtTime = Time.time + EchoFlashDurationSeconds;

        private void OnGUI()
        {
            if (!RunHudToggles.ShowRunHud) return;
            if (_input == null || _currency == null) return;
            if (_currency.RunManaCrystals == null || _currency.RunKeys == null) return; // ещё не пересобраны на этот забег

            DrawCurrencyBlock();
            DrawLastReturnResultBlock();
            DrawArtifactLoadoutBlock();
            DrawBossRoomBlock();
        }

        // Верхняя треть экрана слева — числа валют, всегда видны в забеге.
        // Только Мана и Ключи (PRD v9 раздел 5, задача по экономике "Мана
        // как доход забега, Монеты только в Лагере") — Монеты в забеге
        // больше не начисляются и не показываются вовсе, строка удалена без
        // замены. Fallback-текст вместо спрайта (issue допускает, см.
        // doc-комментарий класса — на этой ветке спрайтов нет вообще).
        private void DrawCurrencyBlock()
        {
            var style = new GUIStyle(GUI.skin.box) { fontSize = 30, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            var text =
                $"Кристаллы Маны: {_currency.RunManaCrystals.Total}\n" +
                $"Ключи: {_currency.RunKeys.Total}";
            GUI.Box(new Rect(20f, 130f, 420f, 110f), text, style);
        }

        // Разбивка конвертации последнего успешного возврата в Лагерь
        // (PRD v9 раздел 5/10) — "Мана × курс = Монеты" отдельной строкой от
        // "Ключи × курс = Монеты". Пусто, пока ни одного возврата за сессию
        // не было (см. RunHudDataSources.LastReturnResult) — не за текущий
        // забег: значение намеренно переживает рестарт, чтобы результат
        // последнего похода оставался виден.
        private void DrawLastReturnResultBlock()
        {
            var result = RunHudDataSources.LastReturnResult;
            if (result == null) return;

            var style = new GUIStyle(GUI.skin.box) { fontSize = 24, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            var text =
                $"Возврат в Лагерь:\n" +
                $"Мана → Монеты: {result.Value.ManaCoins}\n" +
                $"Ключи → Монеты: {result.Value.KeysCoins}\n" +
                $"Итого: {result.Value.TotalCoins}";
            GUI.Box(new Rect(460f, 130f, 340f, 150f), text, style);
        }

        // Состав билда — читаем RunBootstrap.Loadout (задача "композиционный
        // корень RunBootstrap"), пусто, пока он ещё не создан (лоадаут
        // появляется только после того, как RunBootstrap пересобрал Алтарь).
        private void DrawArtifactLoadoutBlock()
        {
            var loadout = RunBootstrap.Instance?.Loadout;
            if (loadout == null) return;

            var amulets = RunHudFormatting.FormatArtifactIds(loadout.Acquired, ArtifactCategory.Amulet);
            var talismans = RunHudFormatting.FormatArtifactIds(loadout.Acquired, ArtifactCategory.Talisman);

            var style = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.UpperLeft, wordWrap = true };
            var text = $"Амулеты: {amulets}\nТалисманы: {talismans}";
            GUI.Box(new Rect(20f, 300f, 500f, 140f), text, style);
        }

        // Владелец, 2026-09-05 («закрыть вертикальный срез — плита Эхо»):
        // самый крупный элемент экрана в момент взятия Эха увеличивается
        // ещё, и на весь экран накладывается затухающая вспышка в цвет
        // Эха (см. <see cref="TileDebugColor.BossRoomEchoColor"/>) — "экран
        // реагирует целиком", не только число. Уровень исполнения тот же,
        // что у всего класса: функциональная вспышка через GUI.DrawTexture,
        // не финальный VFX.
        private const int MultiplierBaseFontSize = 140;
        private const int MultiplierEchoPopFontSize = 175;

        // Множитель Комнаты — самый крупный элемент экрана, ТОЛЬКО внутри
        // Комнаты (RunHudDataSources.ActiveBossRoom != null И ещё активна).
        // Дистанция до волны — рядом, когда волна есть (у BossRoom она есть
        // всегда с момента создания).
        private void DrawBossRoomBlock()
        {
            var room = RunHudDataSources.ActiveBossRoom;
            if (room == null || !room.IsActive) return;

            var echoFlashRemaining = _echoFlashExpiresAtTime - Time.time;
            var echoFlashProgress01 = echoFlashRemaining > 0f ? echoFlashRemaining / EchoFlashDurationSeconds : 0f;

            if (echoFlashProgress01 > 0f)
            {
                var previousColor = GUI.color;
                var flashColor = TileDebugColor.BossRoomEchoColor;
                flashColor.a = echoFlashProgress01 * 0.45f; // затухает до полностью прозрачного к концу вспышки
                GUI.color = flashColor;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = previousColor;
            }

            var multiplierStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Mathf.Lerp(MultiplierBaseFontSize, MultiplierEchoPopFontSize, echoFlashProgress01)),
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
