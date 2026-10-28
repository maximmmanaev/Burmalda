# ARCHITECTURE — карта кода Burmalda

Срез по состоянию кода на main. Ссылки `файл:строка` проверены чтением файла;
то, что не проверено, помечено «не проверено». Термины — `docs/rules/terminology.md`,
подробности ловушек — `docs/wiki/traps.md`. Пути ниже даны от `Assets/Scripts/`.

## 1. Модули

Один модуль = одна папка = одна asmdef `Burmalda.<Папка>`. `Core` не ссылается ни на кого,
`Movement` → `Core`. Остальные ссылки разные (смотри `Burmalda.<Папка>.asmdef`): например,
`Bootstrap` ссылается на 12 игровых модулей (не на FateRoll, RunModifiers, Persistence, DebugVisuals,
Achievements), а `DebugVisuals` ссылается на `Bootstrap`, а не наоборот.

| Папка | Ответственность | Ключевые классы |
|---|---|---|
| `Core/` | Модель сетки: плиты, роли, генератор препятствий (устаревший) | `Core/TunnelGrid.cs:94` (ClaimRow), `Core/Tile.cs:126` (охрана ролей), `Core/MultiplierCurve.cs:15` |
| `Movement/` | Ввод-след, множитель, камера, 5 систем ловушек | `Movement/GridTraceInputController.cs:217`, `Movement/GridTraceTrail.cs:159`, `Movement/TrapSystemsController.cs:158` |
| `Generation/` | Сегменты-шаблоны, выдача рядов, рычаги | `Generation/SegmentRowProvider.cs:194`, `Generation/SegmentTemplateCatalog.cs:64`, `Generation/SegmentReachabilityValidator.cs:60` |
| `Decay/` | Обрушение плит позади игрока | `Decay/TrailDecaySystem.cs:40`, `Decay/TrailDecayController.cs:57` |
| `RunLifecycle/` | Жизнь/смерть забега, предложение Броска Судьбы | `RunLifecycle/RunController.cs:83`, `RunLifecycle/RunState.cs:126` |
| `FateRoll/` | Бросок Судьбы 2D6: таблица, шансы, оплата-заглушка | `FateRoll/FateRollRules.cs`, `FateRoll/FateRollResult.cs` |
| `Currencies/` | Мана/ключи забега, постоянные монеты | `Currencies/CurrencyController.cs:122`, `Currencies/RunCurrencyAccumulator.cs:20`, `Currencies/PersistentWallet.cs:14` |
| `Altar/` | Алтарь, Ритуал, сундуки | `Altar/AltarController.cs:72`, `Altar/AltarTriggerSystem.cs:19` |
| `Sanctuary/` | Святилище бога: обрушение рядами в Лаву, множитель, Саркофаг; владеет Ярусом забега | `Sanctuary/SanctuaryRunSystem.cs`, `Sanctuary/Sanctuary.cs`, `Sanctuary/SanctuaryController.cs` |
| `Camp/` | Лагерь, вывод добычи, возврат | `Camp/CampController.cs:79`, `Camp/EvacuationSystem.cs` |
| `Artifacts/` | Каталог артефактов, лоадаут, Тотем | `Artifacts/RunArtifactLoadout.cs`, `Artifacts/ArtifactPool.cs:23` |
| `RunModifiers/` | Знамения | `RunModifiers/RunModifiers.cs:47`, `RunModifiers/OmenPool.cs:25` |
| `Progression/` | Ярус забега, рекорд глубины | `Progression/RunDepthTier.cs:17`, `Progression/DepthRecord.cs:15` |
| `Persistence/` | Снимок прогресса, сохранение (облако + локально); `SaveController.Create` — по `RuntimeInitializeOnLoadMethod` | `Persistence/SaveController.cs`, `Persistence/SaveCoordinator.cs`, `Persistence/ProgressSnapshot.cs` |
| `DebugVisuals/` | Весь текущий UI/визуал (примитивы в рантайме) | `DebugVisuals/RunHudOverlay.cs`, `DebugVisuals/TunnelDebugVisual.cs:308` |
| `Achievements/` | Достижения (в рантайме не создаются, §4) | `Achievements/AchievementTracker.cs`, `Achievements/AchievementDefinition.cs` |
| `Bootstrap/` | Композиционный корень | `Bootstrap/RunBootstrap.cs:120` |

Пустые папки (только `.gitkeep`/`.meta`, кода нет): `Collection/`, `Leaderboards/`,
`Loadout/`, `Monetization/`, `Pass/`, `Responses/`, `Traps/`, `Trials/`. Ловушки
живут в `Core`+`Movement`, а не в `Traps/`. `Scripts/README.md` частично устарел
(например, пишет про `TurnBasedThreatScheduler`; в коде `Movement/RealTimeThreatScheduler.cs`).

## 2. Поток забега

1. `Bootstrap/RunBootstrap.cs:217` (`RuntimeInitializeOnLoadMethod`, AfterSceneLoad) создаёт
   объект `RunBootstrap`; `Awake` (`:232`) включает строгий режим ролей в редакторе
   (`Tile.ThrowOnRoleConflict`) и зовёт `TrySync` (`:275`), он же вызывается каждый `Update` (`:273`).
2. `TrySync` ищет `GridTraceInputController` на сцене. Нашёл → `EnsureControllersWired`
   (`:300`) один раз добавляет на тот же GameObject контроллеры строго в порядке:
   Decay, Run, Segments (+`EnsureBuilt`), Currency, Altar, Camp, Lever, Traps, Sanctuary (`:311-349`).
3. Каждый контроллер подписывается на `RunStarted` в `OnEnable`, т.е. в момент `AddComponent`;
   порядок вызова = порядок подписки. На каждый `RunStarted` он пересобирает свои системы
   (`RebuildRunState`, `RebuildEncounter`, `Rebuild` и т.д.).
4. `GridTraceInputController.Restart` (`:217`) вызывает `InitializeRun` (`:222`; его же зовёт `Awake`),
   тот создаёт НОВЫЕ `TunnelGrid` и `GridTraceTrail` (`:224-226`) и поднимает `RunStarted` (`:232`). Поэтому все системы привязаны к конкретному
   grid/trail и обязаны отписываться в `Dispose`.
5. Игрок тянет палец → `GridTraceTrail.TryAdvanceTo` (`Movement/GridTraceTrail.cs:159`) поднимает
   `LethalTrapTriggered`, `Advanced` (новая плита), `PositionChanged`; от них работают множитель,
   валюты, распад, босс, алтарь, ловушки (таблица §3).
6. Опасность → `RunState.ResolveHazard`: пока есть Броски Судьбы — `FateRollOffered`, игрок выбирает `RollFate` (2D6: на месте / к последнему Алтарю / смерть) или `DeclineFate`; после третьего броска смерть окончательна → `RunState.Died` → `RunController.OnDied` → `MarkDead` у ввода; `CampController` теряет всё несомое (`EvacuationSystem.LoseAll`).
7. Святилище (`Sanctuary/SanctuaryRunSystem`) сообщает об обрушении пола под игроком через `RunState.ReportSanctuaryCollapse` (обычная опасность); эвакуация — `EvacuationSystem.Evacuate` на любом Алтаре.

Исключение из шагов 2-3: `RunBootstrap.cs:418-428` лениво собирает `Loadout` (ждёт `Altar.Collection`).

## 3. События (издатель → подписчики, по grep `+=` вне `Tests/`)

| Событие (издатель) | Подписчики |
|---|---|
| `GridTraceInputController.RunStarted` (`:165`) | 14 контроллеров (Decay, Run, Segments, Currency, Altar, Camp, Lever, Traps, Sanctuary, TrailMultiplier, TunnelCamera, 2 DebugVisuals) + `RunBootstrap.cs:403` |
| `GridTraceInputController.PressStarted` / `RunConfirmed` (`:177`/`:191`) | только `Movement/TunnelCameraController.cs:215-216` |
| `GridTraceTrail.Advanced` (`:68`) | `TrailDecaySystem`, `TotemChargeSystem`, `TrailTileCurrencySystem`, `TrailManaIncomeSystem`, `SanctuaryRunSystem`, `TrailMultiplierSystem`, `AltarTriggerSystem`, `PickupFeedback` |
| `GridTraceTrail.PositionChanged` (`:78`) | `SanctuaryRunSystem`, `TunnelCameraFollow`, 5 систем ловушек, `TunnelGridReveal`, `RunState`, `LeverActivationSystem`, `SegmentRowProvider`, `TrapEncounterTracker`, `StepClickController` |
| `GridTraceTrail.LethalTrapTriggered` (`:99`) | `RunState.cs:43` |
| `TrailDecaySystem.TileDestroyed` | `RunState.cs:44` |
| `TunnelGrid.TileMaterialized` (`:31`) | `TunnelObstacleGenerator.cs:109`, `TunnelDebugVisual.cs:308` |
| `RealTimeThreatScheduler.TileDue` (`:57`) | `ArrowWave`/`Bomb`/`BladeTact`/`FallingRock`/`MovingWall` (по одной подписке) |
| `TrapRevealSystem.SignatureRevealed` (`:51`) | `DebugVisuals/TrapRevealController.cs:76` |
| `FallingRockTrapSystem.PlayerCrushed` → `TrapSystemsController.FallingRockPlayerCrushed` | `TrapSystemsController.cs:186` → `RunBootstrap.cs:345` → `RunState.ReportFallingRockCrush` |
| `ArrowWaveTrapSystem.WaveStarted` → `TrapSystemsController.ArrowWaveStarted` | `TrapSystemsController.cs:180` → `ArrowProjectileVisual.cs:96` |
| `RunState.Died` (`:52`) | `RunController.cs:93`, `CampController.cs:105`, `DeathNotificationOverlay.cs:85` |
| `RunState.FateRollOffered` / `FateRolled` | `DebugVisuals/FateRollOverlay.cs`, `RunFeedbackDebugUI.cs` |
| `RunDepthTier.Advanced` | `SanctuaryController` (`DepthRecord.ReportTier`) |
| `AltarTriggerSystem.RitualOpened` → `AltarController.RitualOpened` | `AltarController.cs:82`; внешних подписчиков в рантайме нет |
| `EvacuationSystem.Returned` | `DebugVisuals/ReturnConversionHudBridge.cs:59` |
| `Sanctuary.EchoCollected` | `DebugVisuals/RunHudOverlay.cs:103` |
| `Tile.RoleConflictRejected` (static, `:289`) | `RunBootstrap.cs:247` (лог-предупреждение) |

Без подписчиков в рантайме (только тесты): `PersistentWallet.Changed`, `RunCurrencyAccumulator.Changed`,
`TrailMultiplierSystem.MultiplierChanged`, `ArtifactCollection.Recorded`, `ArtifactPool.Unlocked`,
`OmenPool.Unlocked`, `AchievementTracker.Granted`, 
`DepthRecord.RecordBroken`, `SecondWindCharge.Consumed`, `TotemChargeSystem.ChargeChanged/Activated`.

## 4. Системы

- **Правила плит.** У `Tile` (`Core/Tile.cs`) взаимоисключающие роли (Алтарь, рычаг, босс, триггеры
  ловушек и т.д.); запись роли проходит `GuardAgainstConflictingRole` (`:126`). Конфликт `Authored`
  (шаблон) против `Generated` (случайный генератор) решается приоритетом; в редакторе иначе бросает
  исключение (`Tile.ThrowOnRoleConflict`), на устройстве логирует `RoleConflictRejected`.
- **Сегменты.** `SegmentRowProvider` (`Generation/SegmentRowProvider.cs:194`) на каждом
  `PositionChanged` достраивает ряды вперёд шаблонами из `SegmentTemplateCatalog`, заявляя ряд
  `TunnelGrid.ClaimRow` (`Core/TunnelGrid.cs:94`) до материализации. Проходимость шаблонов
  проверяет `SegmentReachabilityValidator` (`:60`). Выбор шаблона по Ярусу — `SegmentSelector`.
- **Ловушки.** `TrapSystemsController.Rebuild` (`Movement/TrapSystemsController.cs:158`) строит 5
  систем: Стрела, Бомба, Лезвия, Падающий камень, `MovingWallTrap` (`:177-187`), каждая со своим
  `RealTimeThreatScheduler`, тикают от `Update` с `Time.deltaTime` (`:138`), не от шагов.
  Статичная Лава — `LethalTrapType.Lava` (`Core/LethalTrapType.cs:35`). Волновой Лавы в коде нет.
- **Святилище.** Раскладка (`SegmentTemplateCatalog.SanctuaryLeshyTemplate`, 15 рядов: вход, 13 рядов, Алтарь)
  выкладывается `SegmentRowProvider` как последний landmark Яруса. `SanctuaryController` →
  `SanctuaryRunSystem`: вход по `Tile.IsSanctuaryEntry`, обрушение рядов в Лаву (`CollapseFront`, ускоряется
  взятым Саркофагом), плиты множителя, зачисление Маны на выходе; прохождение продвигает `RunDepthTier`.
- **Валюты.** `CurrencyController.RebuildRunSystems` (`Currencies/CurrencyController.cs:129-140`):
  мана и ключи забега (`RunCurrencyAccumulator`), множитель `TrailMultiplierSystem`
  (`MultiplierCurve`, максимум ×20, `Core/MultiplierCurve.cs:15`), доход маны за плиту
  `TrailManaIncomeSystem` (`BaseManaPerTile = 10`, `:50`), источники на плитах `TrailTileCurrencySystem`.
  Постоянные монеты — `PersistentWallet` (`CurrencyController.cs:99`).
- **Не подключено в рантайме** (нет `new`/`AddComponent` вне тестов, по grep):
  `TotemChargeSystem`, `TotemAbilityActivationSystem`, `AchievementTracker`, `SecondWindCharge`,
  `OmenSelectionSystem`, `TrapInsight`.

## 5. Хрупкие места

| Место | Суть | Ссылка |
|---|---|---|
| Порядок подписки на события | C# вызывает подписчиков в порядке подписки. `TrailMultiplierSystem` должен быть создан раньше `TrailManaIncomeSystem` (`Currencies/TrailManaIncomeSystem.cs:21-27`); `Decay`/`Run` первыми в `RunBootstrap.cs:311` | `RunBootstrap.cs:300` |
| Пропущенный первый `RunStarted` | `GridTraceInputController.Awake` поднимает `RunStarted` раньше, чем контроллеры подписаны; лечится ленивой сборкой в `Update` (`TrapSystemsController.cs:138`, `Sanctuary/SanctuaryController.cs`, `SegmentGenerationController.cs:70`) | #256 |
| Ловушки: реальное время, не шаги | Планировщик тикает от `Update`; `RealTimeThreatScheduler` заменил пошаговый. Тело #254 касалось только Стрелы/Лезвий/Лавы, Бомба и Камень переведены позже (владелец, 2026-09-14, второй раунд — комментарий `TrapSystemsController.cs:28`) | #254 |
| Перезапись Алтаря рантайм-переходом | Knockback телепортирует на последний Алтарь (`RunState.cs:136`); рантайм-переходы `Tile.TransitionToBlocked` (`Core/Tile.cs:348`, вызов `FallingRockTrapSystem.cs:192`) и `TransitionToLethalTrap` (`Core/Tile.cs:396`) без стража (`Tile.cs:348-351`, `:396-399`) пишут поверх роли; фикс #249 (страж Алтаря) относился к волновой Лаве, которой в коде уже нет — у оставшихся систем стража нет | #249 |
| Шаблоны без выхода | Два шаблона каталога давали сплошной ряд Лавы (`Generation/SegmentTemplateCatalog.cs:385`, `:540`); страж — `SegmentReachabilityValidator` | #251 |
| Два генератора плит одновременно | Устаревший `TunnelObstacleController` на сцене может материализовать ряд до `ClaimRow`; сейчас разведены порядком в `RunBootstrap.cs:391-398` и проверкой `IsRowClaimed` (`Core/TunnelObstacleGenerator.cs:132`); в строгом режиме гонка бросает исключение | #288 |
| .unity/.prefab | Править только вручную; `TunnelObstacleController` привязан к `SampleScene.unity` и `RunLevel.unity` | `docs/rules/forbidden-actions.md` |

Не проверено: внутреннее устройство `DebugVisuals/*` (кроме перечисленных подписок),
`Artifacts/*` за пределами `RunArtifactLoadout`/`ArtifactPool`, содержимое тестов.
