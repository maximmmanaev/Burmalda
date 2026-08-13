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
| `Currencies/` | `ManaCrystals`, `Keys`, `Coins`, `Crystals` | [Спринт 4](https://github.com/maximmmanaev/Burmalda/milestone/4) |
| `Artifacts/` | `Idol`, `Totem`, `Amulet`, `Talisman`, `Rune`, `Relic` и связанные `ArtifactCollection`/`ArtifactPool` | [Спринт 5](https://github.com/maximmmanaev/Burmalda/milestone/5) |
| `Altar/` | `Altar`, `Ritual`, `Chest` и подтипы сундуков, `SellArtifact` | [Спринт 6](https://github.com/maximmmanaev/Burmalda/milestone/6) |
| `Boss/` | Обязательный Босс (`Boss`): 2 Алтаря перед ним, автобой лучом энергии | [Спринт 7](https://github.com/maximmmanaev/Burmalda/milestone/7) |
| `D20/` | `D20Trial` и `D20Outcome` (Испытание Шахты) | [Спринт 7](https://github.com/maximmmanaev/Burmalda/milestone/7) |
| `Camp/` | `Camp`, `CashOut`, `ReturnToCamp` | [Спринт 8](https://github.com/maximmmanaev/Burmalda/milestone/8) |
| `Achievements/` | `Achievement` — условие → оповещение → разлок артефакта | [Спринт 9](https://github.com/maximmmanaev/Burmalda/milestone/9) |
| `Monetization/` | `MonetizationOffer`, `ReviveOffer`, `ArtifactGachaPack`, `RewardedPlacement`, `GachaPityCounter` | [Спринт 11](https://github.com/maximmmanaev/Burmalda/milestone/11) |
| `RunModifiers/` | `Omen` (Знамение Шахты), `RunModifiers` — единая точка применения модификаторов забега (PRD v7 §20) | [Спринт 9](https://github.com/maximmmanaev/Burmalda/milestone/9) |
| `Generation/` | `RunSeed`, `TunnelSegment`, `SegmentTemplate`, `SegmentRowProvider` — сегментная генерация трасс (PRD v7 §21), заменяет `TunnelObstacleGenerator`/`TunnelGridReveal` из `Core`/`Movement` | [Спринт 3](https://github.com/maximmmanaev/Burmalda/milestone/3) |
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
Процедурная расстановка препятствий по сетке (`Core/TunnelObstacleGenerator`,
`Movement/TunnelGridReveal`), активация взрыва по триггеру
(`Movement/ExplosiveTrapArmingSystem`) и тайминг активации ловушек с таймингом
(`Movement/TimedTrapSystem`) по той же причине тоже не в `Traps/`; PRD v7 §21
заменяет всё это сегментной генерацией (`Generation/`) — при реализации эти
классы ожидаемо будут переписаны или поглощены новыми.

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
