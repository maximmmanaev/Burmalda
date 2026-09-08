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
| `Currencies/` | Реализовано (issues #12–#14, задача по экономике v9): `RunCurrencyAccumulator`/`PersistentWallet` (общие реализации временных/постоянных валют — Кристаллы удалены), `TrailManaIncomeSystem` (переименован из `TrailCoinSystem`), `TrailTileCurrencySystem`, `CurrencyController` | [Спринт 4](https://github.com/maximmmanaev/Burmalda/milestone/4) |
| `Artifacts/` | ⚠️ **Затронуто PRD v8**: `ArtifactTag`/`ResonanceType`/`ResonanceCalculator` и `RunArtifactLoadout.ActiveResonances()` удаляются — Созвучия отменены (PRD v8 §6.2, §27), заменяются `Loadout.RunLoadout`/`ArtifactAxis`. Идолы получают роли информации — Чутьё/Жадность/Следы (PRD v8 §6.4). Реализовано (issues #15–18, #28, #79): `Artifact` (общий предок) + `Idol`/`Totem`/`Amulet`/`Talisman`/`Rune`/`Relic`, `ArtifactCollection`, `ArtifactPool`, `IdolLoadout` (2 постоянных слота), `ArtifactCatalog` (5 примеров из issue #17), `TotemChargeSystem`/`TotemAbilityActivationSystem` (заряд и применение Рывка/Пробоя/Неуязвимости, issue #28 — без MonoBehaviour-контроллера и UI-активации, см. глоссарий). Числовые эффекты Амулетов/Талисманов не применяются автоматически — зависят от Алтаря (Спринт 6) и d20 (Спринт 7) | [Спринт 5](https://github.com/maximmmanaev/Burmalda/milestone/5) → [Спринт 15](https://github.com/maximmmanaev/Burmalda/milestone/16) |
| `Altar/` | ⚠️ **Затронуто PRD v8**: `ManaToBossIndicator` удаляется, заменяется `BossRoom.BossBillboard` (Афиша Босса, PRD v8 §7). Реализовано (issues #19–21, #81): `Ritual`, `Chest` + `RuneChest`/`TalismanChest`/`AmuletChest`/`RelicChest`, `RerollPricing`, `AltarTriggerSystem`/`AltarController`. Клетка-Алтарь — `Tile.IsAltar` (Core), не отдельный класс. Тёмный товар — вне релиза (issue #103) | [Спринт 6](https://github.com/maximmmanaev/Burmalda/milestone/6) → [Спринт 14](https://github.com/maximmmanaev/Burmalda/milestone/17) |
| `Boss/` | ⚠️ **Затронуто PRD v8** (см. [Roadmap, «Миграция кода»](../../docs/wiki/roadmap.md#миграция-кода-боссы-v7--комната-босса-v8)): `Boss`/`Boss.Resolve`/`BossEncounterOutcome`/`BossController` удаляются вместе с автобоем лучом энергии, `BossEncounterSystem` переиспользуется (крючки Реликвии/пула/Яруса переносятся на выход из Комнаты), `Tile.IsBoss` сохраняется как вход в Комнату. Реализовано на момент v6/v7 (issues #22, #82): `Boss`, `BossEncounterOutcome`, `FirstBossVictoryTracker`, `BossEncounterSystem`/`BossController`. Печати Боссов отменены v8, Эхо Босса сохраняется — оба вынесены в пост-релизный апдейт | [Спринт 7](https://github.com/maximmmanaev/Burmalda/milestone/7) → [Спринт 13](https://github.com/maximmmanaev/Burmalda/milestone/15) |
| `BossRoom/` | Новая папка (задел, PRD v8 §8): `BossRoom`, `BossRoomTileKind` (Жила/Резонанс/Эхо/Разлом), `RoomMultiplier`, `BossWave`, `BossType`, `BossBillboard` (заменяет `Altar.ManaToBossIndicator`) | [Спринт 13](https://github.com/maximmmanaev/Burmalda/milestone/15) → [Спринт 16](https://github.com/maximmmanaev/Burmalda/milestone/18) |
| `Loadout/` | Новая папка (задел, PRD v8 §6.1–6.2): `RunLoadout` (2 Талисмана + 2 Амулета), `ArtifactAxis` — заменяет теги/Созвучия (`ArtifactTag`/`ResonanceType`/`ResonanceCalculator` в `Artifacts/`, удаляются) | [Спринт 15](https://github.com/maximmmanaev/Burmalda/milestone/16) |
| `Responses/` | Новая папка (задел, PRD v8 §28): `MineResponse`, `MineResponseType` (Отклик Шахты) | [Спринт 17](https://github.com/maximmmanaev/Burmalda/milestone/19) |
| `Collection/` | Новая папка (задел, PRD v8 §29): `CollectionSet` (витрина Наборов, прогресс «N из M»), `DailyFind` (бесплатная ежедневная выдача) — перестраивает существующую Коллекцию (список использованных артефактов, `Artifacts/ArtifactCollection`, с v4) в витрину | [Спринт 18](https://github.com/maximmmanaev/Burmalda/milestone/27) |
| `D20/` | Реализовано (issue #24): `D20Trial`, `D20Outcome` | [Спринт 7](https://github.com/maximmmanaev/Burmalda/milestone/7) |
| `Camp/` | Реализовано (issues #25–27): `Camp` (апгрейды/разлок/Реликвии), `CashOutSystem` (чекпоинт на Алтаре), `ReturnJourneySystem` (возврат — не новая механика движения, использует уже существующий `GridTraceTrail`), `CampController` | [Спринт 8](https://github.com/maximmmanaev/Burmalda/milestone/8) |
| `Achievements/` | Реализован механизм (issue #50): `AchievementDefinition` (условие-предикат + артефакт-награда), `AchievementTracker` (опрос, разлок в `ArtifactPool`, событие `Granted` — хук под "яркое визуальное оповещение"). Каталог из 10 конкретных Достижений (объём релиза) НЕ создан — PRD не называет ни одного конкретного условия/названия; не подключён к `Persistence` — нет Controller'а, создающего трекер в реальной сцене (нечего наполнять без каталога) | [Спринт 9](https://github.com/maximmmanaev/Burmalda/milestone/9) |
| `Monetization/` | `MonetizationOffer`, `ReviveOffer`, `ArtifactGachaPack`, `RewardedPlacement`, `GachaPityCounter`. Релизный минимум (PRD v8 §13) — Спринт 22; гача-паки/полноценный revive-оффер — пост-релизный апдейт | [Спринт 22](https://github.com/maximmmanaev/Burmalda/milestone/23) |
| `RunModifiers/` | Реализовано (issue #84): `OmenId` (6 Знамений, дословно из issue), `RunModifiers` — единая точка применения (все 12 эффектов, свойства), `OmenPool`/`OmenSelectionSystem` (предложение 3 из разблокированных). Подключено к `TrailDecaySystem`/`TrailTileCurrencySystem`/`Altar.Ritual`/`Camp.ReturnJourneySystem` через опциональные конструкторные параметры (нейтральны по умолчанию, ни один существующий вызов/тест не задет). Требуемая энергия Босса — тривиально подключаемо через уже существующий `requiredEnergyForTier` в `Boss.BossEncounterSystem`, без изменений там. НЕ подключено: плотность ловушек/плиток-Маны (Generation), "1 Алтарь вместо 2"/гарантированный Идол (базовой механики "2 Алтаря" нет), подсветка ловушек (нет UI), сам live-Controller выбора в Лагере — см. doc-комментарий `RunModifiers.RunModifiers` | [Спринт 9](https://github.com/maximmmanaev/Burmalda/milestone/9) |
| `Generation/` | Реализовано (issue #78, #51): `RunSeed`, `SegmentTileType`, `SegmentRewardTag`, `SegmentTemplate`, `SegmentReachabilityValidator`, `SegmentSelector`, `SegmentRowProvider`, `SegmentTemplateCatalog`, `SegmentGenerationController`, `LeverActivationSystem`/`LeverActivationController` — сегментная генерация трасс и рычаги (PRD v7 §21, PRD 4.2). Целевая замена `TunnelObstacleGenerator`/`TunnelGridReveal` из `Core`/`Movement`, но сейчас (переходное состояние, `docs/wiki/roadmap.md`) работает **одновременно** с ними на одной сцене: `SegmentRowProvider` заявляет ряды своих шаблонов через `Core.TunnelGrid.ClaimRow` до их материализации, `TunnelObstacleGenerator` пропускает уже заявленные ряды — двойная запись в одну плиту исключена по конструкции. `TunnelObstacleController` уберут со сцены вручную, когда каталог (сейчас 13 шаблонов) закроет тоннель целиком | [Спринт 3](https://github.com/maximmmanaev/Burmalda/milestone/3) |
| `Progression/` | Реализовано в релизном объёме (issue #83): `RunDepthTier` (Ярус текущего забега), `DepthRecord` (постоянный рекорд). Ярусы 4+/`DepthSeal`/`NewDescent` — вынесены в Спринт 14 (пост-релиз, issue #105) | [Спринт 8](https://github.com/maximmmanaev/Burmalda/milestone/8) |
| `Trials/` | ⚠️ **Затронуто PRD v8-пересборкой**: `DailyTrial` (минимальная версия — общий seed дня от даты/Яруса + фиксированное Знамение + награда по порогу, без лидерборда, §23.1, issue #86) — перенесено из Спринта 9 в Спринт 18 (тот же крючок на возврат, что и Коллекция/Находка дня, issue #167); `WeeklyTrial`/`TrialStreak` (§23.2–23.3, накопительные цели/заморозка/топ-10%) — пост-релизным апдейтом, вместе с остальным live-ops | [Спринт 18](https://github.com/maximmmanaev/Burmalda/milestone/27) → [Апдейт: Live-ops](https://github.com/maximmmanaev/Burmalda/milestone/13) |
| `Leaderboards/` | `Leaderboard`, `GhostTrail` — платформенные сервисы (Game Center / Google Play Games), PRD v7 §24. ⚠️ **Затронуто PRD v8 §27**: отложено — у множителя Комнаты нет потолка, счета несопоставимы между забегами, вернуться к вопросу после Спринта баланса (21) | [Апдейт: Live-ops](https://github.com/maximmmanaev/Burmalda/milestone/13) |
| `Pass/` | `ExpeditionPass`, `ExpeditionExperience` (PRD v7 §25) | [Апдейт: Live-ops](https://github.com/maximmmanaev/Burmalda/milestone/13) |
| `DebugVisuals/` | **Debug-инфраструктура, не система из PRD.** Минимальный визуал сетки тоннеля (примитивы в рантайме, без .prefab) для ручного тестирования Core/Movement/Decay глазами | вне очереди спринтов ([issue #58](https://github.com/maximmmanaev/Burmalda/issues/58)) |
| `Persistence/` | **Сквозная инфраструктура, не отдельная система PRD.** `SaveData`, `ProgressSnapshot` (чистые Capture/Apply над уже существующими постоянными объектами), `SaveController` (файл в `Application.persistentDataPath`, JSON). Идолы/Тотем/Руны не сохраняются — нет каталога конкретных экземпляров (честный пробел) | [Спринт 9](https://github.com/maximmmanaev/Burmalda/milestone/9) ([issue #107](https://github.com/maximmmanaev/Burmalda/issues/107)) |
| `IntegrationTests/` | **Тестовая инфраструктура, не система PRD.** `CoreLoopIntegrationTests` (issue #29) — e2e-сборка core loop одним прогоном (тоннель → ловушки → множитель → Алтарь/Ритуал → Босс → возврат/кэш-аут → Лагерь). После задачи "композиционный корень RunBootstrap" собирает GameObject ровно так, как в реальной сцене (`GridTraceInputController` + `RunBootstrap`, донабирающий Currency/Altar/Boss/Camp/Lever), и взаимодействует через публичный API реальных Controller'ов, а не конструирует системы напрямую — раньше эталон порядка сборки был только в этом тесте, теперь тест сам проверяет, что `RunBootstrap` этот порядок не сломал. Заодно поймала и починила реальный баг компиляции: `Persistence.ProgressSnapshot` ссылался на несуществовавший `ArtifactCollection.RecordedIds` | [Спринт 9](https://github.com/maximmmanaev/Burmalda/milestone/9) ([issue #29](https://github.com/maximmmanaev/Burmalda/issues/29)) |
| `RunLifecycle/` | `RunState`/`RunController` — жизнь/смерть забега, теперь с d20-броском (`D20Trial`, issue #24) вместо мгновенной смерти от ловушки/обвала; `SecondWindCharge` (issue #23) — заряд реализован, но не подключён к реальному триггеру: с появлением `Camp.ReturnJourneySystem` (Спринт 8) механика "провала возврата" уже есть, но `SecondWindCharge.TryConsume()` пока не вызывается из `ReturnJourneySystem.HandleDeathDuringReturn`; рестарт забега (`GridTraceInputController.Restart()`) | вне очереди спринтов ([issue #9](https://github.com/maximmmanaev/Burmalda/issues/9)), d20 — [Спринт 7](https://github.com/maximmmanaev/Burmalda/milestone/7) |
| `Bootstrap/` | **Композиционный корень, не система PRD.** `RunBootstrap` — единственная точка, которая при старте забега собирает игровые системы в явном порядке: самобутстрапится (как отладочные компоненты), находит уже размещённый на сцене `GridTraceInputController` и донабирает на его GameObject `SegmentGenerationController` → `CurrencyController` → `AltarController` → `BossController` → `CampController` → `LeverActivationController` (порядок жёсткий — каждый следующий контроллер читает предыдущий через `GetComponent` ровно один раз в своём `Awake()`), плюс пересобирает `Artifacts.RunArtifactLoadout` на каждый забег. До этой задачи все четыре — `CurrencyController`/`RunArtifactLoadout`/`RunDepthTier`/`BossController` — обнаруживались НЕ созданными в реальной игре только когда кто-то пытался показать значение на экране (см. `DebugVisuals/`); теперь `RunBootstrap.Instance` — единственный источник, потребители (HUD) читают оттуда, не заводят своих экземпляров. `Generation.SegmentGenerationController` подключён наряду с уже размещённым на сцене `Movement.TunnelObstacleController` — разграничены по рядам через `Core.TunnelGrid.ClaimRow` (переходное состояние, `docs/wiki/roadmap.md`), а не взаимоисключающая замена | вне очереди спринтов |

Активные способности Тотема (`TotemAbilityType`, раздел 12 PRD) и монетизационные
метрики (раздел 14) не выделены в отдельную папку — они реализуются внутри
`Artifacts/` (Тотем) и `Monetization/` соответственно. Рычаги (`Lever`, раздел 4.2
PRD) реализуются внутри `Traps/` — это не ловушка, но логически часть механики
навигации по опасным путям. Владелец, 2026-09-05 («оставить только пять новых
ловушек»): единственные ловушки в игре теперь — Стрела/Бомба/Лезвия/Падающий
камень/Лава-волна (`Core.LethalTrapType.ArrowWave`/`BombBlast`/`BladeTact`/
`LavaWave`, триггеры — `Tile.ArrowWaveTargetRow`/`IsBombTrigger`/
`BladeTactTargetRow`/`IsFallingRockTrigger`/`IsLavaTrigger`, все — ходовая
механика на `Movement.TurnBasedThreatScheduler`) плюс статичная Лава
(`LethalTrapType.Lava`, рельеф, не ловушка-механизм). Прежние яма (Pit),
мгновенный взрыв по триггеру (`Movement/ExplosiveTrapArmingSystem`,
`LethalTrapType.Explosion`) и ловушки с таймингом на реальном времени
(`Movement/TimedTrapSystem`, `Core.TimedTrapType`, `Tile.TimedTrapTarget`/
`IsTimedTrapActive`) удалены из игры целиком вместе с тестами и `Core.TrapSignature`
— не переименованы, а именно убраны, символы каталога шаблонов (`p`/`e`/`a`/`b`)
заменены соответствующими новыми (`r`/`x`/`w`/`t`) механической правкой
раскладок на месте. Реализованы не в `Traps/`, а прямо в `Core`/`Movement`
(`Tile.LethalTrap`/`Tile.Mark*Trigger`, по аналогии с `Tile.IsBlocked`) —
новых механик, которые требовали бы отдельной папки со своим asmdef, не
появилось. `Traps/` остаётся пустой. Процедурная поплиточная
расстановка препятствий (`Core/TunnelObstacleGenerator`,
`Movement/TunnelGridReveal`) — целевая замена сегментной генерацией
(`Generation/`, issue #78), но сейчас оба работают одновременно на одной
сцене (переходное состояние, `docs/wiki/roadmap.md`): `SegmentRowProvider`
заявляет ряды своих шаблонов (`Core.TunnelGrid.ClaimRow`) раньше, чем
`TunnelObstacleGenerator` успевает откликнуться на материализацию их плит —
двойная запись в одну плиту исключена по конструкции, не по договорённости.
Оба класса помечены устаревшими в коде, но НЕ удалены: `Movement/TunnelObstacleController`
уже привязан к GameObject на `Assets/Scenes/SampleScene.unity`, а трогать
.unity-файлы автономно запрещено (`docs/rules/forbidden-actions.md`); уберёт
его со сцены владелец продукта вручную, когда каталог `Generation/SegmentTemplateCatalog`
(сейчас 13 шаблонов, нужно ~30 на Ярус — issue #78) закроет тоннель целиком.
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
