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
| `Artifacts/` | Реализовано (issues #15–18, #79): `Artifact` (общий предок) + `Idol`/`Totem`/`Amulet`/`Talisman`/`Rune`/`Relic`, `ArtifactCollection`, `ArtifactPool`, `IdolLoadout` (2 постоянных слота), `RunArtifactLoadout` (временный билд забега), `ArtifactTag`/`ResonanceType`/`ResonanceCalculator` (Созвучия), `ArtifactCatalog` (5 примеров из issue #17). Числовые эффекты Амулетов/Талисманов не применяются автоматически — зависят от Алтаря (Спринт 6) и d20 (Спринт 7) | [Спринт 5](https://github.com/maximmmanaev/Burmalda/milestone/5) |
| `Altar/` | `Altar`, `Ritual`, `Chest` и подтипы сундуков, `SellArtifact` | [Спринт 6](https://github.com/maximmmanaev/Burmalda/milestone/6) |
| `Boss/` | Обязательный Босс (`Boss`): 2 Алтаря перед ним, автобой лучом энергии | [Спринт 7](https://github.com/maximmmanaev/Burmalda/milestone/7) |
| `D20/` | `D20Trial` и `D20Outcome` (Испытание Шахты) | [Спринт 7](https://github.com/maximmmanaev/Burmalda/milestone/7) |
| `Camp/` | `Camp`, `CashOut`, `ReturnToCamp` | [Спринт 8](https://github.com/maximmmanaev/Burmalda/milestone/8) |
| `Achievements/` | `Achievement` — условие → оповещение → разлок артефакта | [Спринт 9](https://github.com/maximmmanaev/Burmalda/milestone/9) |
| `Monetization/` | `MonetizationOffer`, `ReviveOffer`, `ArtifactGachaPack`, `RewardedPlacement`, `GachaPityCounter` | [Спринт 11](https://github.com/maximmmanaev/Burmalda/milestone/11) |
| `RunModifiers/` | `Omen` (Знамение Шахты), `RunModifiers` — единая точка применения модификаторов забега (PRD v7 §20) | [Спринт 9](https://github.com/maximmmanaev/Burmalda/milestone/9) |
| `Generation/` | Реализовано (issue #78, #51): `RunSeed`, `SegmentTileType`, `SegmentRewardTag`, `SegmentTemplate`, `SegmentReachabilityValidator`, `SegmentSelector`, `SegmentRowProvider`, `SegmentTemplateCatalog`, `SegmentGenerationController`, `LeverActivationSystem`/`LeverActivationController` — сегментная генерация трасс и рычаги (PRD v7 §21, PRD 4.2). Заменяет `TunnelObstacleGenerator`/`TunnelGridReveal` из `Core`/`Movement` — те помечены устаревшими в коде, но не удалены (уже на `SampleScene.unity`, см. `docs/rules/forbidden-actions.md`); замена компонента на сцене — вручную | [Спринт 3](https://github.com/maximmmanaev/Burmalda/milestone/3) |
| `Progression/` | `DepthTier`, `DepthSeal`, `NewDescent` — Ярусы Глубины и мягкий prestige (PRD v7 §22) | [Спринт 8](https://github.com/maximmmanaev/Burmalda/milestone/8) |
| `Trials/` | `DailyTrial` (минимальная версия — общий seed + награды по порогу, без лидерборда, §23.1) в Спринте 9; `WeeklyTrial`/`TrialStreak` (§23.2–23.3) — в Спринте 13, вместе с остальным live-ops | [Спринт 9](https://github.com/maximmmanaev/Burmalda/milestone/9) → [Спринт 13](https://github.com/maximmmanaev/Burmalda/milestone/13) |
| `Leaderboards/` | `Leaderboard`, `GhostTrail` — платформенные сервисы (Game Center / Google Play Games), PRD v7 §24 | [Спринт 13](https://github.com/maximmmanaev/Burmalda/milestone/13) |
| `Pass/` | `ExpeditionPass`, `ExpeditionExperience` (PRD v7 §25) | [Спринт 13](https://github.com/maximmmanaev/Burmalda/milestone/13) |
| `DebugVisuals/` | **Debug-инфраструктура, не система из PRD.** Минимальный визуал сетки тоннеля (примитивы в рантайме, без .prefab) для ручного тестирования Core/Movement/Decay глазами | вне очереди спринтов ([issue #58](https://github.com/maximmmanaev/Burmalda/issues/58)) |
| `RunLifecycle/` | **Временная инфраструктура, не финальная система из PRD.** `RunState`/`RunController` — упрощённая смерть (яма/лава/обрушение плиты под ногами) без d20-броска и рестарт забега (`GridTraceInputController.Restart()`), пока нет `D20Trial` (Спринт 7); ожидаемо будет переписана/поглощена при реализации D20 | вне очереди спринтов ([issue #9](https://github.com/maximmmanaev/Burmalda/issues/9)) |

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
