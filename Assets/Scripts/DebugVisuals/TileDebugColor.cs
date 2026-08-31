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
        public static readonly Color FreshColor = new Color(0f, 220f / 255f, 160f / 255f);

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
        /// Перерисовать саму текстуру не в скоупе этой задачи (палитра
        /// целиком — issue #191, Спринт 12b) — вместо этого
        /// <see cref="TunnelDebugVisual.ApplyVisual"/> умножает текстуру на
        /// этот приглушающий тон (снижает яркость и насыщенность), а не
        /// показывает её как есть (<c>material.color = Color.white</c>, как
        /// для остальных текстур). Временная мера до перегенерации палитры.
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
        /// Задача «рычаг и ворота невидимы»: плейтест владельца — «в игре
        /// замечены недоступные ключи, окружённые стеной, рычагов поблизости
        /// не обнаружил», потому что <c>Resolve()</c> раньше вообще не знал
        /// про <see cref="TileVisualState.IsLever"/>. Рычаг — механизм-
        /// приглашение («на это можно нажать»), не опасность — насыщенный
        /// зелёный, не пересекается ни с приглушёнными зелёно-тёплыми
        /// StartColor/FreshColor (те гораздо темнее/desaturated), ни с одним
        /// из цветов ловушек/сигнатур в этой палитре.
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
        // (см. doc-комментарий TileVisualState.IsBoss). Синий — единственный
        // не занятый цвет в этой палитре (зелёный/оранжевый/красный —
        // распад, жёлтый/магента — ловушки, чёрный/тёмный — препятствия).
        public static readonly Color BossColor = new Color(40f / 255f, 40f / 255f, 220f / 255f);

        public static Color Resolve(TileVisualState state)
        {
            if (state.IsDestroyed) return DestroyedColor;
            if (state.IsCurrentPosition) return CurrentPositionColor;
            if (state.IsStart) return StartColor;
            if (state.IsBoss) return BossColor;
            // Ворота и рычаг — новые структурные состояния (задача «рычаг и
            // ворота невидимы»), той же приоритетной группы, что Boss/Blocked:
            // фиксированы на плите, не связаны с распадом, должны быть видны
            // всегда. Открытые/закрытые ворота — один тон, разная яркость
            // (см. LeverGateOpenColor/LeverGateClosedColor).
            if (state.IsGated) return state.IsLeverGateOpen ? LeverGateOpenColor : LeverGateClosedColor;
            if (state.IsLever) return LeverColor;
            if (state.IsBlocked) return BlockedColor;
            // Issue #163: лава остаётся видимой как раньше (PRD v8 §4.2)
            // — единственный LethalTrapType, для которого Resolve() всё ещё
            // возвращает его собственный цвет напрямую.
            if (state.LethalTrap == LethalTrapType.Lava) return LavaColor;
            // Яма и активированный взрыв — скрыты (см. Core.TrapSignature):
            // ОДИН общий цвет на оба типа, не выдающий, какой именно.
            if (state.LethalTrap == LethalTrapType.Pit || state.LethalTrap == LethalTrapType.Explosion) return HiddenTrapSignatureColor;
            if (state.ActiveTimedTrap.HasValue) return TimedTrapActiveColor;
            // Единая сигнатура для обоих видов триггера (см. TriggerSignatureColor) —
            // факт "здесь механизм" виден всем, тип — не отсюда.
            if (state.IsExplosiveTrapTrigger || state.IsTimedTrapTrigger) return TriggerSignatureColor;

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

        private static Color ResolveDecayGradient(float progress01)
        {
            var clamped = Mathf.Clamp01(progress01);
            return clamped < 0.5f
                ? Color.Lerp(FreshColor, HalfDecayedColor, clamped / 0.5f)
                : Color.Lerp(HalfDecayedColor, AboutToDecayColor, (clamped - 0.5f) / 0.5f);
        }
    }
}
