# Assets/Scripts — структура

Заготовка структуры C#-кода под архитектуру Burmalda. Папки пока пусты
(только `.gitkeep`, чтобы git их отслеживал) — реальные классы появятся
по мере выполнения соответствующих спринтов из [Roadmap](../../docs/wiki/roadmap.md).

Соответствие терминов PRD и C#-идентификаторов — в
[C#-глоссарии](../../docs/wiki/csharp-glossary.md).

## Папки → спринты

| Папка | Что здесь будет | Спринт |
|---|---|---|
| `Core/` | Общие утилиты, интерфейсы, используемые другими системами | — (инфраструктура) |
| `Movement/` | Grid-trace движение (тянешь палец по плитам) | [Спринт 2](https://github.com/maximmmanaev/Burmalda/milestone/2) |
| `Decay/` | Decay-система (обрушение плит позади игрока) | [Спринт 2](https://github.com/maximmmanaev/Burmalda/milestone/2) |
| `Traps/` | Пока не используется — все три вида ловушек (статичные, мгновенные, с таймингом) реализованы в `Core`/`Movement`, см. ниже | [Спринт 3](https://github.com/maximmmanaev/Burmalda/milestone/3) |
| `Currencies/` | Реализовано (issues #12–#14): `RunCurrencyAccumulator`/`PersistentWallet` (общие реализации временных/постоянных валют), `TrailCoinSystem`, `TrailTileCurrencySystem`, `CurrencyController` | [Спринт 4](https://github.com/maximmmanaev/Burmalda/milestone/4) |
| `Artifacts/` | Реализовано (issues #15–18, #28, #79): `Artifact` (общий предок) + `Idol`/`Totem`/`Amulet`/`Talisman`/`Rune`/`Relic`, `ArtifactCollection`, `ArtifactPool`, `IdolLoadout` (2 постоянных слота), `RunArtifactLoadout` (временный билд забега), `ArtifactTag`/`ResonanceType`/`ResonanceCalculator` (Созвучия), `ArtifactCatalog` (5 примеров из issue #17), `TotemChargeSystem`/`TotemAbilityActivationSystem` (заряд и применение Рывка/Пробоя/Неуязвимости, issue #28 — без MonoBehaviour-контроллера и UI-активации, см. глоссарий). Числовые эффекты Амулетов/Талисманов не применяются автоматически — зависят от Алтаря (Спринт 6) и d20 (Спринт 7) | [Спринт 5](https://github.com/maximmmanaev/Burmalda/milestone/5) |
| `Altar/` | Реализовано (issues #19–21, #81): `Ritual`, `Chest` + `RuneChest`/`TalismanChest`/`AmuletChest`/`RelicChest`, `RerollPricing`, `AltarTriggerSystem`/`AltarController`, `ManaToBossIndicator`. Клетка-Алтарь — `Tile.IsAltar` (Core), не отдельный класс. Тёмный товар — вне релиза (issue #103) | [Спринт 6](https://github.com/maximmmanaev/Burmalda/milestone/6) |
| `Boss/` | Реализовано (issues #22, #82): `Boss`, `BossEncounterOutcome` (Перелив энергии), `FirstBossVictoryTracker`, `BossEncounterSystem`/`BossController`. Точка Босса — `Tile.IsBoss` (Core), не отдельный класс. «2 Алтаря перед Боссом», Печати Боссов и Эхо Босса — не подключены/вынесены в Спринт 13 | [Спринт 7](https://github.com/maximmmanaev/Burmalda/milestone/7) |
| `D20/` | Реализовано (issue #24): `D20Trial`, `D20Outcome` | [Спринт 7](https://github.com/maximmmanaev/Burmalda/milestone/7) |
| `Camp/` | Реализовано (issues #25–27): `Camp` (апгрейды/разлок/Реликвии), `CashOutSystem` (чекпоинт на Алтаре), `ReturnJourneySystem` (возврат — не новая механика движения, использует уже существующий `GridTraceTrail`), `CampController` | [Спринт 8](https://github.com/maximmmanaev/Burmalda/milestone/8) |
| `Achievements/` | `Achievement` — условие → оповещение → разлок артефакта | [Спринт 9](https://github.com/maximmmanaev/Burmalda/milestone/9) |
| `Monetization/` | `MonetizationOffer`, `ReviveOffer`, `ArtifactGachaPack`, `RewardedPlacement`, `GachaPityCounter` | [Спринт 11](https://github.com/maximmmanaev/Burmalda/milestone/11) |
| `RunModifiers/` | `Omen` (Знамение Шахты), `RunModifiers` — единая точка применения модификаторов забега (PRD v7 §20) | [Спринт 9](https://github.com/maximmmanaev/Burmalda/milestone/9) |
| `Generation/` | Реализовано (issue #78, #51): `RunSeed`, `SegmentTileType`, `SegmentRewardTag`, `SegmentTemplate`, `SegmentReachabilityValidator`, `SegmentSelector`, `SegmentRowProvider`, `SegmentTemplateCatalog`, `SegmentGenerationController`, `LeverActivationSystem`/`LeverActivationController` — сегментная генерация трасс и рычаги (PRD v7 §21, PRD 4.2). Заменяет `TunnelObstacleGenerator`/`TunnelGridReveal` из `Core`/`Movement` — те помечены устаревшими в коде, но не удалены (уже на `SampleScene.unity`, см. `docs/rules/forbidden-actions.md`); замена компонента на сцене — вручную | [Спринт 3](https://github.com/maximmmanaev/Burmalda/milestone/3) |
| `Progression/` | Реализовано в релизном объёме (issue #83): `RunDepthTier` (Ярус текущего забега), `DepthRecord` (постоянный рекорд). Ярусы 4+/`DepthSeal`/`NewDescent` — вынесены в Спринт 14 (пост-релиз, issue #105) | [Спринт 8](https://github.com/maximmmanaev/Burmalda/milestone/8) |
| `Trials/` | `DailyTrial` (минимальная версия — общий seed + награды по порогу, без лидерборда, §23.1) в Спринте 9; `WeeklyTrial`/`TrialStreak` (§23.2–23.3) — в Спринте 13, вместе с остальным live-ops | [Спринт 9](https://github.com/maximmmanaev/Burmalda/milestone/9) → [Спринт 13](https://github.com/maximmmanaev/Burmalda/milestone/13) |
| `Leaderboards/` | `Leaderboard`, `GhostTrail` — платформенные сервисы (Game Center / Google Play Games), PRD v7 §24 | [Спринт 13](https://github.com/maximmmanaev/Burmalda/milestone/13) |
| `Pass/` | `ExpeditionPass`, `ExpeditionExperience` (PRD v7 §25) | [Спринт 13](https://github.com/maximmmanaev/Burmalda/milestone/13) |
| `DebugVisuals/` | **Debug-инфраструктура, не система из PRD.** Минимальный визуал сетки тоннеля (примитивы в рантайме, без .prefab) для ручного тестирования Core/Movement/Decay глазами | вне очереди спринтов ([issue #58](https://github.com/maximmmanaev/Burmalda/issues/58)) |
| `Persistence/` | **Сквозная инфраструктура, не отдельная система PRD.** `SaveData`, `ProgressSnapshot` (чистые Capture/Apply над уже существующими постоянными объектами), `SaveController` (файл в `Application.persistentDataPath`, JSON). Идолы/Тотем/Руны не сохраняются — нет каталога конкретных экземпляров (честный пробел) | [Спринт 9](https://github.com/maximmmanaev/Burmalda/milestone/9) ([issue #107](https://github.com/maximmmanaev/Burmalda/issues/107)) |
| `RunLifecycle/` | `RunState`/`RunController` — жизнь/смерть забега, теперь с d20-броском (`D20Trial`, issue #24) вместо мгновенной смерти от ловушки/обвала; `SecondWindCharge` (issue #23, не подключён к триггеру — Возврат в лагерь ещё не реализован, Спринт 8); рестарт забега (`GridTraceInputController.Restart()`) | вне очереди спринтов ([issue #9](https://github.com/maximmmanaev/Burmalda/issues/9)), d20 — [Спринт 7](https://github.com/maximmmanaev/Burmalda/milestone/7) |

Активные способности Тотема (`TotemAbilityType`, раздел 12 PRD) и монетизационные
метрики (раздел 14) не выделены в отдельную папку — они реализуются внутри
`Artifacts/` (Тотем) и `Monetization/` соответственно. Рычаги (`Lever`, раздел 4.2
PRD) реализуются внутри `Traps/` — это не ловушка, но логически часть механики
навигации по опасным путям. Все три вида ловушек — статичные (яма/лава,
issue #9), динамические мгновенные (взрыв, issue #10) и с таймингом (стрела/
лезвие, issue #45) — реализованы не в `Traps/`, а прямо в `Core`/`Movement`
(`Tile.LethalTrap`/`LethalTrapType.Pit`/`Lava`/`Explosion` для первых двух,
`Tile.TimedTrapTarget`/`IsTimedTrapActive`/`TimedTrapType` для третьей, по
аналогии с `Tile.IsBlocked`) — новых механик, которые требовали бы отдельной
папки со своим asmdef, не появилось. `Traps/` остаётся пустой: если в
будущем понадобится «настоящая» траектория снаряда (несколько плит подряд,
визуальный полёт) вместо текущей версии (одна плита, окно времени —
упрощение первой версии #45), это, вероятно, и будет тем, что переедет сюда.
Активация взрыва по триггеру (`Movement/ExplosiveTrapArmingSystem`) и тайминг
активации ловушек с таймингом (`Movement/TimedTrapSystem`) по той же причине
тоже не в `Traps/` — они не заменяются `Generation/`, только СПОСОБ
расстановки триггеров на сетке (см. ниже). Процедурная поплиточная
расстановка препятствий (`Core/TunnelObstacleGenerator`,
`Movement/TunnelGridReveal`) заменена сегментной генерацией (`Generation/`,
issue #78) — оба класса помечены устаревшими в коде, но НЕ удалены: уже
привязаны к GameObject на `Assets/Scenes/SampleScene.unity`, а трогать
.unity-файлы автономно запрещено (`docs/rules/forbidden-actions.md`); замену
компонента `Movement/TunnelObstacleController` на новый
`Generation/SegmentGenerationController` в сцене нужно сделать вручную.
Рычаги (issue #51) реализованы внутри `Generation/` как элемент шаблона
сегмента (`SegmentTileType.Lever`/`LeverGate`, `LeverActivationSystem`), а не
отдельно в `Traps/` — PRD v7 §21 прямо делает их частью сегментов.

Множитель добычи (PRD 4.3, issue #11) по той же логике не получил отдельной
папки — `MultiplierCurve` (Core, чистая функция) и `TrailMultiplierSystem`/
`TrailMultiplierController` (Movement) тесно связаны с `GridTraceTrail` и не
образуют системы, которой нужна собственная asmdef-сборка.

PRD v7 (см. [C#-глоссарий](../../docs/wiki/csharp-glossary.md)) добавляет
шесть новых папок по тому же принципу, что Boss/Achievements в v6 — по
одной на систему, которой действительно нужна новая асинхронная сборка. Не
всё новое из v7 получило отдельную папку: теги/Созвучия (`ArtifactTag`,
`Resonance`, раздел 6) реализуются внутри `Artifacts/`, Печати Боссов и
Перелив энергии (`BossSeal`, `ManaOverflow`, раздел 8) — внутри `Boss/`,
Тёмный товар (`HiddenOffer`, раздел 7) — внутри `Altar/`, rewarded-каталог и
pity-счётчик гачи (`RewardedPlacement`, `GachaPityCounter`, раздел 13) —
внутри `Monetization/`: все они расширяют существующую систему, а не вводят
новую.
