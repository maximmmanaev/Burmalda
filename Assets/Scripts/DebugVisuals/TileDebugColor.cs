using Burmalda.Core;
using Burmalda.DebugVisuals.HudDesign;
using UnityEngine;

namespace Burmalda.DebugVisuals
{
    /// <summary>
    /// Debug-цвет плиты по её состоянию (issue #58) — чистая функция, без
    /// сцены и рендер-API, по аналогии с draw() в legacy/burmolda_demo.html:
    /// старт — один цвет, текущая позиция — другой, обычная активная плита
    /// переходит зелёный→оранжевый→красный по мере распада, разрушенная —
    /// тёмная. Не финальный арт — минимальная индикация для ручного
    /// тестирования механик Core/Movement/Decay.
    /// </summary>
    public static class TileDebugColor
    {
        // legacy/burmolda_demo.html, draw(): 'safe' — rgba(80,200,120,...)
        public static readonly Color StartColor = new Color(80f / 255f, 200f / 255f, 120f / 255f);

        // legacy/burmolda_demo.html, draw(): белый маркер текущей позиции трейла
        public static readonly Color CurrentPositionColor = Color.white;

        // legacy/burmolda_demo.html, draw(): активная плита, pct<.5 — rgba(0,220,160,...)
        // Баг с устройства (владелец, 2026-09-04, «отладочные цвета остались
        // из холодной палитры»): в реальном забеге эта константа почти
        // всегда перекрыта тёплой текстурой tile-fresh.png (issue #191,
        // Спринт 12b) — но остаётся видимым фолбэком там, где текстуры нет
        // (Editor/тесты, любое непокрытое art'ом состояние), и должна
        // держать тон палитры сама по себе. Мятно-бирюзовый (холодный)
        // заменён тёплым оливково-золотым — тот же принцип "безопасно", но
        // в семье золота/факелов (PRD §3), не отдельно от неё.
        public static readonly Color FreshColor = new Color(150f / 255f, 195f / 255f, 60f / 255f);

        // legacy/burmolda_demo.html, draw(): pct .5..0.8 — rgba(255,160,0,...)
        public static readonly Color HalfDecayedColor = new Color(1f, 160f / 255f, 0f);

        // legacy/burmolda_demo.html, draw(): pct>=.8 — rgba(255,40,40,...)
        public static readonly Color AboutToDecayColor = new Color(1f, 40f / 255f, 40f / 255f);

        // legacy/burmolda_demo.html, draw(): t.state==='destroyed' — #0a0a0e
        public static readonly Color DestroyedColor = new Color(10f / 255f, 10f / 255f, 14f / 255f);

        // legacy/burmolda_demo.html, draw(): t.type==='block' — #140a06
        public static readonly Color BlockedColor = new Color(20f / 255f, 10f / 255f, 6f / 255f);

        // legacy/burmolda_demo.html, draw(): t.type==='pit' — #000 (+внутренний круг #300)
        // Issue #163: больше НЕ используется для яма-ловушки в Resolve() —
        // яма скрыта (см. HiddenTrapSignatureColor), сама плита физически
        // непроходима только через игровую логику, не через её debug-цвет.
        // Константа оставлена (не удалена) — пригодится для будущего
        // экрана "точный тип раскрыт Идолом Чутья", который рисует владелец
        // продукта (issue #163 явно не просит финальный арт здесь).
        public static readonly Color PitColor = Color.black;

        // Новый тип из PRD v5 (раздел 4.2) — аналога в прототипе нет (там только 'pit'/'spike').
        // Яркий тревожный оранжево-красный, отличим от градиента распада на глаз.
        public static readonly Color LavaColor = new Color(1f, 90f / 255f, 0f);

        // Issue #10, PRD 4.2 — динамическая мгновенная ловушка после срабатывания
        // триггера. Ярко-жёлтый со вспышкой (условно "взрыв"), не пересекается
        // с остальными тревожными цветами (лава — оранжевый, яма — чёрный).
        // Issue #163: как и PitColor, больше не используется в Resolve() —
        // после активации взрыв тоже скрыт (см. HiddenTrapSignatureColor).
        public static readonly Color ExplosionColor = new Color(1f, 230f / 255f, 0f);

        // Issue #163: единая сигнатура опасности для ВСЕХ скрытых ловушек
        // (яма и активированный взрыв, см. Core.TrapSignature) — намеренно
        // ОДИН цвет на оба типа, не два разных: сигнатура обязана не
        // выдавать точный тип ("видно, что не так, не видно, что именно").
        // Мутный сизо-фиолетовый — тревожный, но не пересекается ни с одним
        // уже занятым цветом палитры (зелёный/оранжевый/красный — распад,
        // жёлтый/магента — уже активные/раскрытые ловушки, чёрный/тёмный —
        // препятствия/лава). НЕ финальный арт — сигнатуру (цвет/форму/звук)
        // дорисует владелец продукта (issue #163 явно это исключает из
        // скоупа), это функциональная заглушка "плита выглядит иначе".
        public static readonly Color HiddenTrapSignatureColor = new Color(90f / 255f, 70f / 255f, 110f / 255f);

        /// <summary>
        /// Задача «сделать тоннель играбельным», часть 3: плейтест владельца
        /// показал, что <see cref="HiddenTrapSignatureColor"/> в текстуре
        /// (<c>tile-pit.png</c> — кислотно-зелёная мозаика с чёрной дырой)
        /// читается как однозначная ловушка, а не как "с плитой что-то не
        /// так" (PRD v9 §4.2 — сигнатура обязана оставлять сомнение).
        /// Перерисовать саму текстуру было не в скоупе задачи «сделать
        /// тоннель играбельным» — вместо этого <c>TunnelDebugVisual.ApplyVisual</c>
        /// умножал текстуру на этот приглушающий тон. Палитра перегенерирована
        /// задачей «тёплый набор плит» (issue #191, Спринт 12b) —
        /// <c>tile-hidden-trap-signature.png</c> нарисован специально под эту
        /// роль (уже приглушённый, в общей тёплой палитре, используется и
        /// для сигнатуры триггера — см. <c>TileArtCatalog.Get</c>), тон
        /// больше не применяется (<c>ApplyVisual</c> показывает текстуру как
        /// есть). Константа оставлена — вернуть тон тривиально, если
        /// плейтест на устройстве покажет, что текстура всё равно заметнее источников.
        /// </summary>
        public static readonly Color HiddenTrapSignatureTextureTint = new Color(0.55f, 0.5f, 0.62f);

        // Issue #10/#45, задача «сделать тоннель играбельным» часть 3:
        // раньше ExplosiveTriggerColor (жёлтый) и TimedTrapTriggerColor
        // (магента) были РАЗНЫМИ цветами — игрок читал тип ловушки прямо с
        // пола, без примеривания и без Идола Чутья (см. TilePreviewController).
        // Единая сигнатура: факт "здесь механизм" виден всем (см. Trigger*
        // ниже — плита-триггер сама по себе безопасна), какой именно —
        // уровень Идола Чутья (TrapInsight.HasTrapTypeInsight), как и для
        // HiddenTrapSignatureColor. Старые константы оставлены (не удалены,
        // как и PitColor/ExplosionColor выше) — в Resolve() больше не
        // используются.
        public static readonly Color TriggerSignatureColor = new Color(200f / 255f, 180f / 255f, 40f / 255f);

        // Не финальный арт: плита-триггер (issue #10) сама по себе безопасна,
        // цвет — только чтобы при ручном тестировании видеть, где она есть.
        // УСТАРЕЛО (задача «сделать тоннель играбельным» часть 3) — заменён
        // единой TriggerSignatureColor, см. её комментарий. Оставлена для
        // истории/возможного будущего экрана "точный тип раскрыт Идолом".
        public static readonly Color ExplosiveTriggerColor = new Color(200f / 255f, 180f / 255f, 40f / 255f);

        // Issue #45, PRD v5 4.2 — плита-цель ловушки с таймингом, пока снаряд/
        // лезвие физически проходит через неё (Tile.IsTimedTrapActive). Единый
        // цвет для обоих видов (стрела/лезвие) — яркая "тревожная" магента, не
        // пересекается ни с одним из уже занятых цветов.
        public static readonly Color TimedTrapActiveColor = new Color(1f, 0f, 200f / 255f);

        // Не финальный арт: плита-триггер (issue #45), как и ExplosiveTriggerColor,
        // сама по себе безопасна. УСТАРЕЛО (задача «сделать тоннель играбельным»
        // часть 3) — заменён единой TriggerSignatureColor. Оставлена для истории.
        public static readonly Color TimedTrapTriggerColor = new Color(160f / 255f, 60f / 255f, 140f / 255f);

        /// <summary>
        /// Задача «сделать тоннель играбельным», часть 1: источник Кристаллов
        /// Маны (<c>Core.Tile.IsManaSource</c>) — видим ВСЕГДА, это награда, не
        /// опасность (см. doc-комментарий <see cref="TileVisualState.IsManaSource"/>).
        /// То же значение, что фиолетовый ромб Маны в HUD
        /// (<see cref="BurmaldaHudPalette.ManaPurple"/>) — единый цвет "это
        /// Мана" на полу и в счётчике, не два независимо подобранных.
        /// Светлый/яркий фиолетовый — не пересекается с тёмным сизо-фиолетовым
        /// HiddenTrapSignatureColor (см. соответствующий тест).
        /// </summary>
        public static readonly Color ManaSourceColor = BurmaldaHudPalette.ManaPurple;

        /// <summary>Источник Ключей — то же значение, что медный кружок Ключей в HUD (<see cref="BurmaldaHudPalette.CopperCoin"/>), тот же принцип, что <see cref="ManaSourceColor"/>.</summary>
        public static readonly Color KeySourceColor = BurmaldaHudPalette.CopperCoin;

        /// <summary>
        /// Рычаг (<c>Tile.IsLever</c>). Видим ВСЕГДА (владелец, 2026-09-04,
        /// отменяет issue #193: "рычаг — механизм, а не опасность", не
        /// смешивается с сигнатурой опасности) — насыщенный зелёный,
        /// однозначно "на это можно нажать". Между issue #193 (2026-09-01)
        /// и этим решением рычаг ненадолго был скрыт той же сигнатурой, что
        /// ловушки — константа тогда была помечена неиспользуемой, теперь
        /// снова активна в <c>Resolve()</c>.
        /// </summary>
        public static readonly Color LeverColor = new Color(60f / 255f, 235f / 255f, 90f / 255f);

        /// <summary>
        /// Закрытые ворота бокового прохода (<c>Tile.IsGated</c>, issue #51).
        /// Тёмный бронзово-янтарный — читается как преграда (тёмный, как
        /// BlockedColor), но другой ХОД (не почти-чёрный, а с явным тёплым
        /// оттенком) — задача прямо требует отличить «никогда» (Blocked) от
        /// «пока нет» (ворота): цвет похож по тону, но не совпадает.
        /// </summary>
        public static readonly Color LeverGateClosedColor = new Color(110f / 255f, 75f / 255f, 25f / 255f);

        /// <summary>
        /// Открытые ворота (<c>Tile.IsLeverGateOpen</c>) — тот же янтарный
        /// оттенок, что <see cref="LeverGateClosedColor"/>, но яркий и
        /// светлый, а не тёмный: момент открытия должен быть заметен
        /// (задача) — тон один и тот же ("это те же ворота"), яркость
        /// меняется кардинально ("но теперь они открыты").
        /// </summary>
        public static readonly Color LeverGateOpenColor = new Color(1f, 210f / 255f, 90f / 255f);

        // Вход в Комнату Босса (Tile.IsBoss, PRD v8 §8.1) — единственная
        // строка debug-визуала под механику Босса, которая переживает v8
        // (см. doc-комментарий TileVisualState.IsBoss).
        // Баг с устройства (владелец, 2026-09-04): прежний насыщенно-синий
        // читался как "непонятная чужеродная плита" именно потому, что был
        // единственным холодным цветом на тёплом полу (PRD §3: золото,
        // факелы, тёсаный камень) — плита маршрута ОБЯЗАНА читаться как
        // часть той же палитры, не как баг рендера. Тёплая медь/пламя —
        // та же приоритетная группа, что AltarColor (структурное состояние,
        // не подверженное распаду), но другой оттенок внутри тёплой семьи
        // (медно-оранжевый, не чистое золото) — Босс и Алтарь не должны
        // визуально сливаться, они стоят рядом на маршруте (2 Алтаря перед
        // Боссом, PRD v9 §8.1). См. также TileArtKind.Boss — теперь под
        // этот же вход заведена текстура (переиспользует tile-altar.png с
        // другим тоном, см. TileArtCatalog.Get), это цвет — фолбэк на
        // случай, если текстуры почему-то нет (Editor/тесты).
        public static readonly Color BossColor = new Color(210f / 255f, 95f / 255f, 20f / 255f);

        /// <summary>
        /// Алтарь (<c>Tile.IsAltar</c>) — та же приоритетная группа, что
        /// <see cref="BossColor"/> (фиксированное структурное состояние, не
        /// подверженное распаду). Насыщенный чистый золотой — отличим от
        /// более приглушённого оливково-жёлтого <see cref="TriggerSignatureColor"/>
        /// (совпадающего по тону, но не по насыщенности/яркости) и не
        /// пересекается ни с одним другим цветом палитры.
        /// </summary>
        public static readonly Color AltarColor = new Color(1f, 215f / 255f, 0f);

        /// <summary>
        /// Задача «Комната Босса» (владелец, 2026-09-02): содержимое Комнаты
        /// (Жила/Резонанс/Эхо, PRD v9 §8.2) — награда, видна всегда, тот же
        /// принцип, что <see cref="ManaSourceColor"/>/<see cref="KeySourceColor"/>.
        /// Три РАЗЛИЧИМЫХ цвета в этой группе (не один общий) — вертикальный
        /// срез должен быть проходим на глаз без Идола: игрок должен видеть
        /// разницу между "+Мана", "+0.5 к множителю" и "×2 к множителю" на
        /// полу, не только по числу на HUD. Голубой — самый частый тип
        /// (Жила), не пересекается ни с одним занятым цветом палитры.
        /// </summary>
        public static readonly Color BossRoomVeinColor = new Color(110f / 255f, 190f / 255f, 1f);

        /// <summary>Резонанс — мятно-бирюзовый, см. <see cref="BossRoomVeinColor"/>.</summary>
        public static readonly Color BossRoomResonanceColor = new Color(60f / 255f, 235f / 255f, 190f / 255f);

        /// <summary>Эхо — самый редкий и самый "заряженный" тип (×2, PRD v9 §8.3), яркая магента, см. <see cref="BossRoomVeinColor"/>.</summary>
        public static readonly Color BossRoomEchoColor = new Color(1f, 70f / 255f, 235f / 255f);

        /// <summary>Разлом Комнаты — вне скоупа вертикального среза (не генерируется), цвет заведён для полноты enum на случай будущей интеграции.</summary>
        public static readonly Color BossRoomRiftColor = new Color(190f / 255f, 130f / 255f, 1f);

        /// <summary>
        /// Задача «двойные флаги на плитах» (плейтест владельца: сигнатура
        /// опасности отрисовывается вместо стены, рычаг/Алтарь иногда
        /// непроходимы, сигнатура вместо источника Маны): причина — два
        /// генератора писали в одну плиту (см. <c>Core.Tile.GuardAgainstConflictingRole</c>,
        /// теперь бросает исключение вместо молчаливой перезаписи).
        /// ПОРЯДОК ВЕТОК ниже — вторая линия обороны на случай, если
        /// конфликт всё же случится: состояния, определяющие РЕАЛЬНОЕ
        /// поведение (проходимость — Destroyed/Blocked/Lava/LethalTrap/
        /// TimedTrapActive/триггер) проверяются РАНЬШЕ ролевых и декоративных
        /// (Boss/Altar/Gated/Lever/источники) — если на одной плите всё же
        /// окажутся обе роли, игрок увидит то, что реально его останавливает
        /// или убивает, а не безобидный на вид Алтарь/рычаг/источник поверх
        /// стены. Раньше порядок был обратным (Boss/Altar/Gated/Lever шли
        /// первыми) — намеренно исправлено этой задачей.
        /// </summary>
        public static Color Resolve(TileVisualState state)
        {
            if (state.IsDestroyed) return DestroyedColor;
            if (state.IsCurrentPosition) return CurrentPositionColor;
            if (state.IsStart) return StartColor;
            if (state.IsBlocked) return BlockedColor;
            // Issue #163: лава остаётся видимой как раньше (PRD v8 §4.2)
            // — единственный LethalTrapType, для которого Resolve() всё ещё
            // возвращает его собственный цвет напрямую.
            if (state.LethalTrap == LethalTrapType.Lava) return LavaColor;
            // Задача «разрушение плиты», продолжение (владелец, 2026-09-01):
            // сработавший взрыв (Tile.TransitionToLethalTrap) — угроза
            // ПРЯМО СЕЙЧАС, тот же принцип, что ActiveTimedTrap ниже (см.
            // Core.TrapSignature) — виден ВСЕГДА, без гейта
            // IsDangerSignatureRevealed. Реюз TimedTrapActiveColor — тот же
            // смысл "активная угроза", отдельного цвета под "уже взорвалось"
            // придумывать не в скоупе агента.
            if (state.LethalTrap == LethalTrapType.Explosion) return TimedTrapActiveColor;
            // Задача «раскрытие опасности при примеривании» (PRD v9 §4.2
            // заменяется): яма — единственный оставшийся скрытый тип (см.
            // Core.TrapSignature), только ПОСЛЕ того, как игрок навёл на
            // плиту (state.IsDangerSignatureRevealed, см.
            // Movement.TrapRevealSystem). Пока не раскрыта — плита
            // неотличима от обычного пола, ветка ничего не возвращает и
            // проваливается ниже, до градиента распада в самом конце.
            if (state.LethalTrap == LethalTrapType.Pit && state.IsDangerSignatureRevealed)
                return HiddenTrapSignatureColor;
            if (state.ActiveTimedTrap.HasValue) return TimedTrapActiveColor;
            // Единая сигнатура для обоих видов триггера (см. TriggerSignatureColor) —
            // факт "здесь механизм" виден всем, тип — не отсюда. Та же
            // гейт-логика раскрытия, что и у ямы/взрыва выше.
            if ((state.IsExplosiveTrapTrigger || state.IsTimedTrapTrigger) && state.IsDangerSignatureRevealed)
                return TriggerSignatureColor;

            if (state.IsBoss) return BossColor;
            if (state.BossRoomTile.HasValue) return ResolveBossRoomTileColor(state.BossRoomTile.Value);
            if (state.IsAltar) return AltarColor;
            // Ворота — структурное состояние (задача «рычаг и ворота
            // невидимы»), фиксировано на плите, не связано с распадом,
            // должно быть видно ВСЕГДА (issue #193: "награды и преграды
            // видны всегда, опасность и механизмы — нет" — ворота преграда).
            // Проверяется РАНЬШЕ рычага ниже (та же "поведение важнее
            // декоративного" логика), хотя вживую вместе не бывают
            // (взаимоисключающие роли). Открытые/закрытые — один тон,
            // разная яркость (см. LeverGateOpenColor/LeverGateClosedColor).
            if (state.IsGated) return state.IsLeverGateOpen ? LeverGateOpenColor : LeverGateClosedColor;
            // Владелец, 2026-09-04 (отменяет issue #193): рычаг — механизм,
            // не опасность, видим ВСЕГДА, не гейтится IsDangerSignatureRevealed
            // и не делит сигнатуру с триггерами ловушек выше.
            if (state.IsLever) return LeverColor;

            // Источники валюты — награда, видна всегда (см. doc-комментарий
            // TileVisualState.IsManaSource), проверяются после
            // опасностей/механизмов и до градиента распада: ни один
            // генератор сейчас не совмещает источник с ловушкой на одной
            // плите, но если бы совместил — опасность всё равно важнее для
            // выживания, чем то, что под ней ценного.
            if (state.IsManaSource) return ManaSourceColor;
            if (state.IsKeySource) return KeySourceColor;

            // Остальной тип плиты (артефакт) добавит сюда же ветвление до
            // градиента распада — препятствие/ловушка/валюта уже не
            // участвуют в градиенте, т.к. никогда не начинают распад.
            return ResolveDecayGradient(state.DecayProgress01);
        }

        private static Color ResolveBossRoomTileColor(BossRoomTileKind kind)
        {
            switch (kind)
            {
                case BossRoomTileKind.Resonance: return BossRoomResonanceColor;
                case BossRoomTileKind.Echo: return BossRoomEchoColor;
                case BossRoomTileKind.Rift: return BossRoomRiftColor;
                default: return BossRoomVeinColor;
            }
        }

        private static Color ResolveDecayGradient(float progress01)
        {
            var clamped = Mathf.Clamp01(progress01);
            return clamped < 0.5f
                ? Color.Lerp(FreshColor, HalfDecayedColor, clamped / 0.5f)
                : Color.Lerp(HalfDecayedColor, AboutToDecayColor, (clamped - 0.5f) / 0.5f);
        }
    }
}
